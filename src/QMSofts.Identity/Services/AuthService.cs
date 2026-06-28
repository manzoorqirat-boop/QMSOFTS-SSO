using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Data;
using QMSofts.Identity.Models;
using QMSofts.Shared.Audit;
using QMSofts.Shared.Contracts;

namespace QMSofts.Identity.Services;

public sealed class AuthResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public TokenResponse? Tokens { get; init; }

    // ERES-parity signals the client may act on:
    public bool RequiresSessionDecision { get; init; } // concurrent login
    public string? ActiveSessionIp { get; init; }
    public bool MustChangePassword { get; init; }
    public string? Reason { get; init; }               // e.g. PASSWORD_EXPIRED
    public bool LoggedOutAll { get; init; }            // user chose "log out all"

    public static AuthResult Fail(string error) => new() { Succeeded = false, Error = error };
    public static AuthResult Ok(TokenResponse tokens, bool mustChange = false, string? reason = null)
        => new() { Succeeded = true, Tokens = tokens, MustChangePassword = mustChange, Reason = reason };
    public static AuthResult NeedSessionDecision(string? activeIp) => new()
    {
        Succeeded = false, RequiresSessionDecision = true,
        Error = "You are already logged in on another session.", ActiveSessionIp = activeIp
    };
    public static AuthResult LoggedOut() => new() { Succeeded = false, LoggedOutAll = true };
}

public sealed class AuthService
{
    private readonly IdentityDbContext _db;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly IConfiguration _config;

    public AuthService(
        IdentityDbContext db, TokenService tokens, AuditService audit,
        SettingsService settings, IConfiguration config)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
        _settings = settings;
        _config = config;
    }

    private int RefreshDays => _config.GetValue<int?>("QmsAuth:RefreshTokenDays") ?? 7;

    public async Task<AuthResult> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var normalized = req.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        var (maxAttempts, lockoutDuration) = await _settings.GetLockoutConfigAsync(ct);

        // Unknown user: audit and return generic error (no enumeration).
        if (user is null)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                identifier: req.Email, detail: "unknown user", ct: ct);
            return AuthResult.Fail("Invalid email or password.");
        }

        // Locked?
        if (user.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            var minsLeft = (int)Math.Ceiling((until - DateTimeOffset.UtcNow).TotalMinutes);
            await _audit.RecordAsync(AuthAuditEventType.LoginBlocked,
                user.Id, req.Email, detail: $"account locked ({minsLeft} min remaining)", ct: ct);
            return AuthResult.Fail($"Account is temporarily locked. Try again in {minsLeft} minute(s).");
        }

        // Verify password.
        var passwordOk = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);

        if (!passwordOk)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= maxAttempts)
            {
                user.LockedUntil = DateTimeOffset.UtcNow.Add(lockoutDuration);
                await _db.SaveChangesAsync(ct);
                await _audit.RecordAsync(AuthAuditEventType.AccountLocked,
                    user.Id, req.Email, detail: $"locked after {maxAttempts} failed attempts", ct: ct);
                var mins = (int)Math.Round(lockoutDuration.TotalMinutes);
                return AuthResult.Fail($"Account locked due to too many failed attempts. Try again in {mins} minute(s).");
            }
            await _db.SaveChangesAsync(ct);
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                user.Id, req.Email, detail: $"failed attempt {user.FailedLoginCount} of {maxAttempts}", ct: ct);
            return AuthResult.Fail("Invalid email or password.");
        }

        // Status gates (after password check, matching ERES).
        if (user.Status == UserStatus.Inactive)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginBlocked, user.Id, req.Email, detail: "account inactive", ct: ct);
            return AuthResult.Fail("Account is inactive. Contact your administrator.");
        }
        if (user.Status == UserStatus.Disabled)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginBlocked, user.Id, req.Email, detail: "account disabled", ct: ct);
            return AuthResult.Fail("Account has been disabled. Contact your administrator.");
        }

        // Password expiry → force change (short-lived token, mustChange flag).
        var expiryDays = await _settings.GetIntAsync("passwordExpiry", 60, ct);
        if (expiryDays > 0)
        {
            var daysSince = (DateTimeOffset.UtcNow - user.PasswordLastChanged).TotalDays;
            if (daysSince >= expiryDays || user.MustChangePassword)
            {
                await _audit.RecordAsync(AuthAuditEventType.PasswordExpired, user.Id, req.Email,
                    detail: $"password age {Math.Floor(daysSince)}d, policy {expiryDays}d", ct: ct);
                // Issue a token so the client can call change-password, but flag it.
                var (shortTok, shortExp) = IssueToken(user, sid: null);
                await _db.SaveChangesAsync(ct);
                var prof0 = ToProfile(user);
                return AuthResult.Ok(new TokenResponse(shortTok, "", shortExp, prof0),
                    mustChange: true, reason: "PASSWORD_EXPIRED");
            }
        }

        // Successful credentials: reset counters.
        if (user.LockedUntil is not null)
            await _audit.RecordAsync(AuthAuditEventType.AccountAutoUnlocked, user.Id, req.Email,
                detail: "lockout window elapsed", ct: ct);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        // ── Single active session: a new login always wins ──
        // If another session is active, we automatically log it out and open a
        // fresh one. The previous session's token is invalidated (its sid stops
        // matching, and ForceLogoutAt predates only older tokens). No prompt.
        var now = DateTimeOffset.UtcNow;
        var hadActive = user.ActiveSessionId is not null
            && user.SessionExpiresAt is { } exp && exp > now;

        if (hadActive)
        {
            await _audit.RecordAsync(AuthAuditEventType.SessionReplaced, user.Id, req.Email,
                detail: $"previous session from {user.SessionIp ?? "unknown"} logged out by new login", ct: ct);
        }

        // Open a fresh session (this replaces any previous one).
        var timeoutMins = Math.Max(1, await _settings.GetIntAsync("sessionTimeout", 480, ct));
        var sid = Guid.NewGuid().ToString("N");
        user.ForceLogoutAt = null;
        user.ActiveSessionId = sid;
        user.SessionIssuedAt = now;
        user.SessionExpiresAt = now.AddMinutes(timeoutMins);
        user.SessionIp = _audit.CurrentIp();

        var (access, expiresAt) = IssueToken(user, sid, timeoutMins);
        var (rawRefresh, refreshHash) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = now.AddDays(RefreshDays)
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.LoginSucceeded, user.Id, req.Email, ct: ct);

        return AuthResult.Ok(new TokenResponse(access, rawRefresh, expiresAt, ToProfile(user)),
            mustChange: user.MustChangePassword);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest req, CancellationToken ct = default)
    {
        var hash = TokenService.HashRefreshToken(req.RefreshToken);
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(rt => rt.User).ThenInclude(u => u.AppEntitlements)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive)
            return AuthResult.Fail("Invalid or expired refresh token.");

        var user = stored.User;
        if (user.Status != UserStatus.Active)
            return AuthResult.Fail("Account is not active.");

        stored.RevokedAt = DateTimeOffset.UtcNow;

        // Reuse the current active session id so the rotated token stays valid.
        var timeoutMins = Math.Max(1, await _settings.GetIntAsync("sessionTimeout", 480, ct));
        var (access, expiresAt) = IssueToken(user, user.ActiveSessionId, timeoutMins);
        var (rawRefresh, refreshHash) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshDays)
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.TokenRefreshed, user.Id, user.Email, ct: ct);
        return AuthResult.Ok(new TokenResponse(access, rawRefresh, expiresAt, ToProfile(user)));
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;
        user.ActiveSessionId = null;
        user.SessionExpiresAt = null;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(AuthAuditEventType.Logout, user.Id, user.Email, ct: ct);
    }

    // ── Change password (history + reuse + complexity, mirrors ERES) ──
    public async Task<AuthResult> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return AuthResult.Fail("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            await _audit.RecordAsync(AuthAuditEventType.PasswordChangeFailed, user.Id, user.Email,
                detail: "current password incorrect", ct: ct);
            return AuthResult.Fail("Current password is incorrect.");
        }

        var policyError = await _settings.ValidatePasswordAsync(newPassword, ct);
        if (policyError is not null)
        {
            await _audit.RecordAsync(AuthAuditEventType.PasswordChangeFailed, user.Id, user.Email,
                detail: $"policy: {policyError}", ct: ct);
            return AuthResult.Fail(policyError);
        }

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
        {
            await _audit.RecordAsync(AuthAuditEventType.PasswordChangeFailed, user.Id, user.Email,
                detail: "new password same as current", ct: ct);
            return AuthResult.Fail("New password must be different from your current password.");
        }

        var historyLimit = Math.Max(1, await _settings.GetIntAsync("passwordHistory", 3, ct));
        foreach (var oldHash in user.PasswordHistory.TakeLast(historyLimit))
        {
            if (BCrypt.Net.BCrypt.Verify(newPassword, oldHash))
            {
                await _audit.RecordAsync(AuthAuditEventType.PasswordChangeFailed, user.Id, user.Email,
                    detail: $"reuse of last {historyLimit}", ct: ct);
                return AuthResult.Fail($"Password was used recently. You cannot reuse your last {historyLimit} password(s).");
            }
        }

        // Push current hash into history (cap 10), set new hash.
        user.PasswordHistory.Add(user.PasswordHash);
        if (user.PasswordHistory.Count > 10)
            user.PasswordHistory = user.PasswordHistory.TakeLast(10).ToList();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.PasswordLastChanged = DateTimeOffset.UtcNow;
        user.MustChangePassword = false;
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.PasswordChanged, user.Id, user.Email,
            detail: $"history limit {historyLimit}", ct: ct);
        return AuthResult.Ok(null!);
    }

    // ── Helpers ──
    private (string token, DateTimeOffset expiresAt) IssueToken(User user, string? sid, int? timeoutMins = null)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var apps = user.AppEntitlements.Select(a => a.AppKey).ToList();
        var appRoles = user.AppEntitlements
            .Where(a => !string.IsNullOrEmpty(a.Role))
            .ToDictionary(a => a.AppKey, a => a.Role!);
        return _tokens.CreateAccessToken(user, roles, apps, appRoles, sid, timeoutMins);
    }

    private static UserProfile ToProfile(User user) => new(
        user.Id, user.Email, user.Name,
        user.UserRoles.Select(ur => ur.Role.Name).ToList(),
        user.AppEntitlements.Select(a => a.AppKey).ToList());
}
