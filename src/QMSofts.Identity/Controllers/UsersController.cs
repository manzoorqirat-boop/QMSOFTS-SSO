using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Data;
using QMSofts.Identity.Models;
using QMSofts.Identity.Services;
using QMSofts.Shared.Audit;
using QMSofts.Shared.Auth;
using QMSofts.Shared.Contracts;

namespace QMSofts.Identity.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;

    public UsersController(IdentityDbContext db, AuditService audit, SettingsService settings)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
    }

    private static UserProfile ToProfile(User u) => new(
        u.Id, u.Email, u.Name,
        u.UserRoles.Select(r => r.Role.Name).ToList(),
        u.AppEntitlements.Select(a => a.AppKey).ToList());

    private static UserDetail ToDetail(User u) => new(
        u.Id, u.Email, u.Name, u.EmployeeId,
        u.UserRoles.Select(r => r.Role.Name).ToList(),
        u.AppEntitlements.Select(a => new AppGrant(a.AppKey, a.Role)).ToList(),
        u.Status.ToString(),
        u.MustChangePassword,
        u.LockedUntil is { } lu && lu > DateTimeOffset.UtcNow,
        u.LockedUntil,
        u.ActiveSessionId is not null && u.SessionExpiresAt is { } se && se > DateTimeOffset.UtcNow,
        u.PasswordLastChanged,
        u.CreatedAt);

    private string Actor() => User.FindFirst(QmsClaimTypes.Email)?.Value ?? "admin";
    private string ActorRole() => User.FindFirst(QmsClaimTypes.Role)?.Value ?? "";

    private async Task RecordChange(string action, User u, object? before, object? after, CancellationToken ct)
    {
        var beforeJson = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before);
        var afterJson = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after);
        _db.ChangeHistories.Add(new ChangeHistory
        {
            EntityType = "User",
            EntityId = u.Id.ToString(),
            EntityLabel = u.Email,
            Action = action,
            Before = beforeJson,
            After = afterJson,
            ChangedBy = Actor(),
            ChangedByRole = ActorRole(),
            IpAddress = _audit.CurrentIp(),
        });
        await _db.SaveChangesAsync(ct);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? app, CancellationToken ct)
    {
        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(app))
            query = query.Where(u => u.AppEntitlements.Any(a => a.AppKey == app));

        var users = await query.OrderBy(u => u.Name).ToListAsync(ct);
        return Ok(users.Select(ToDetail));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? NotFound() : Ok(ToDetail(user));
    }

    [HttpPost]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var normalized = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            return Conflict(new { error = "A user with this email already exists." });

        var pwError = await _settings.ValidatePasswordAsync(req.Password, ct);
        if (pwError is not null) return BadRequest(new { error = pwError });

        var user = new User
        {
            Email = req.Email.Trim(),
            NormalizedEmail = normalized,
            Name = req.Name.Trim(),
            EmployeeId = req.EmployeeId?.Trim() ?? "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            MustChangePassword = true, // force change on first login (GxP)
        };

        var roles = await _db.Roles.Where(r => req.Roles.Contains(r.Name)).ToListAsync(ct);
        foreach (var r in roles) user.UserRoles.Add(new UserRole { Role = r, User = user });
        foreach (var g in req.Apps.DistinctBy(a => a.AppKey))
            user.AppEntitlements.Add(new AppEntitlement { AppKey = g.AppKey, Role = g.Role, User = user });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.UserCreated, user.Id, user.Email,
            detail: $"roles=[{string.Join(',', req.Roles)}] apps=[{string.Join(',', req.Apps.Select(a => a.AppKey))}]", ct: ct);
        await RecordChange("CREATE", user, null,
            new { user.Email, user.Name, roles = req.Roles, apps = req.Apps }, ct);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDetail(user));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        var before = new
        {
            user.Name, user.EmployeeId, status = user.Status.ToString(),
            roles = user.UserRoles.Select(r => r.Role.Name).ToList(),
            apps = user.AppEntitlements.Select(a => new { a.AppKey, a.Role }).ToList()
        };

        user.Name = req.Name.Trim();
        user.EmployeeId = req.EmployeeId?.Trim() ?? "";
        if (Enum.TryParse<UserStatus>(req.Status, out var st)) user.Status = st;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // Reconcile roles (mutate tracked collection in place).
        var desiredRoles = await _db.Roles.Where(r => req.Roles.Contains(r.Name)).ToListAsync(ct);
        user.UserRoles.Clear();
        foreach (var r in desiredRoles)
            user.UserRoles.Add(new UserRole { RoleId = r.Id, UserId = user.Id });

        // Reconcile app entitlements + per-app roles.
        var beforeApps = user.AppEntitlements.Select(a => a.AppKey).ToHashSet();
        var afterApps = req.Apps.Select(a => a.AppKey).ToHashSet();
        user.AppEntitlements.Clear();
        foreach (var g in req.Apps.DistinctBy(a => a.AppKey))
            user.AppEntitlements.Add(new AppEntitlement { AppKey = g.AppKey, Role = g.Role, UserId = user.Id });
        if (!beforeApps.SetEquals(afterApps))
            user.TokenVersion++;

        await _db.SaveChangesAsync(ct);

        foreach (var granted in afterApps.Except(beforeApps))
            await _audit.RecordAsync(AuthAuditEventType.AppAccessGranted, user.Id, user.Email, granted, ct: ct);
        foreach (var revoked in beforeApps.Except(afterApps))
            await _audit.RecordAsync(AuthAuditEventType.AppAccessRevoked, user.Id, user.Email, revoked, ct: ct);

        var after = new
        {
            user.Name, user.EmployeeId, status = user.Status.ToString(),
            roles = req.Roles, apps = req.Apps
        };
        await RecordChange("UPDATE", user, before, after, ct);

        return Ok(ToDetail(user));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] StatusChangeRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        if (!Enum.TryParse<UserStatus>(req.Status, out var st))
            return BadRequest(new { error = "Invalid status." });

        var before = user.Status.ToString();
        user.Status = st;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        // Deactivating ends the active session too.
        if (st != UserStatus.Active)
        {
            user.ActiveSessionId = null;
            user.SessionExpiresAt = null;
            user.ForceLogoutAt = DateTimeOffset.UtcNow;
            user.TokenVersion++;
        }
        await _db.SaveChangesAsync(ct);

        var evt = st == UserStatus.Active ? AuthAuditEventType.UserReactivated : AuthAuditEventType.UserDeactivated;
        await _audit.RecordAsync(evt, user.Id, user.Email, detail: $"{before} -> {st}", ct: ct);
        await RecordChange(st == UserStatus.Active ? "REACTIVATE" : "DEACTIVATE", user,
            new { status = before }, new { status = st.ToString() }, ct);

        return Ok(ToDetail(user));
    }

    [HttpPut("{id:guid}/unlock")]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        user.LockedUntil = null;
        user.FailedLoginCount = 0;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(AuthAuditEventType.AccountUnlocked, user.Id, user.Email, detail: "manual unlock", ct: ct);
        return Ok(ToDetail(user));
    }

    [HttpPost("{id:guid}/force-logout")]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> ForceLogout(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        user.ForceLogoutAt = DateTimeOffset.UtcNow;
        user.ActiveSessionId = null;
        user.SessionExpiresAt = null;
        user.TokenVersion++;
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(AuthAuditEventType.ForceLogoutApplied, user.Id, user.Email, detail: "admin force-logout", ct: ct);
        return Ok(ToDetail(user));
    }
}
