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

    public static AuthResult Fail(string error) => new() { Succeeded = false, Error = error };
    public static AuthResult Ok(TokenResponse tokens) => new() { Succeeded = true, Tokens = tokens };
}

public sealed class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IdentityDbContext _db;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;
    private readonly IConfiguration _config;

    public AuthService(
        IdentityDbContext db, TokenService tokens, AuditService audit, IConfiguration config)
    {
        _db = db;
        _tokens = tokens;
        _audit = audit;
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

        if (user is null)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                identifier: req.Email, detail: "unknown user", ct: ct);
            return AuthResult.Fail("Invalid email or password.");
        }

        if (user.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                user.Id, req.Email, detail: "account locked", ct: ct);
            return AuthResult.Fail("Account is temporarily locked. Try again later.");
        }

        if (!user.IsActive)
        {
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                user.Id, req.Email, detail: "inactive account", ct: ct);
            return AuthResult.Fail("Invalid email or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTimeOffset.UtcNow.Add(LockoutDuration);
                await _audit.RecordAsync(AuthAuditEventType.AccountLocked,
                    user.Id, req.Email, detail: "max failed attempts", ct: ct);
            }
            await _db.SaveChangesAsync(ct);
            await _audit.RecordAsync(AuthAuditEventType.LoginFailed,
                user.Id, req.Email, detail: "bad password", ct: ct);
            return AuthResult.Fail("Invalid email or password.");
        }

        // Success: reset counters, issue tokens.
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var apps = user.AppEntitlements.Select(a => a.AppKey).ToList();

        var (access, expiresAt) = _tokens.CreateAccessToken(user, roles, apps);
        var (rawRefresh, refreshHash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshDays)
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.LoginSucceeded, user.Id, req.Email, ct: ct);

        var profile = new UserProfile(user.Id, user.Email, user.Name, roles, apps);
        return AuthResult.Ok(new TokenResponse(access, rawRefresh, expiresAt, profile));
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
        if (!user.IsActive)
            return AuthResult.Fail("Account is inactive.");

        // Rotate: revoke old, issue new.
        stored.RevokedAt = DateTimeOffset.UtcNow;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var apps = user.AppEntitlements.Select(a => a.AppKey).ToList();

        var (access, expiresAt) = _tokens.CreateAccessToken(user, roles, apps);
        var (rawRefresh, refreshHash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshDays)
        });
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.TokenRefreshed, user.Id, user.Email, ct: ct);

        var profile = new UserProfile(user.Id, user.Email, user.Name, roles, apps);
        return AuthResult.Ok(new TokenResponse(access, rawRefresh, expiresAt, profile));
    }
}
