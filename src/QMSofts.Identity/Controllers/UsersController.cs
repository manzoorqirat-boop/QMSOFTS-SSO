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

    public UsersController(IdentityDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private static UserProfile ToProfile(User u) => new(
        u.Id, u.Email, u.Name,
        u.UserRoles.Select(r => r.Role.Name).ToList(),
        u.AppEntitlements.Select(a => a.AppKey).ToList());

    /// <summary>
    /// List users. Apps (e.g. Parakh's lead-auditor dropdown) call this, and the
    /// shadow-sync uses ?app=parakh to fetch only users entitled to that app.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? app, CancellationToken ct)
    {
        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .Where(u => u.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(app))
            query = query.Where(u => u.AppEntitlements.Any(a => a.AppKey == app));

        var users = await query.OrderBy(u => u.Name).ToListAsync(ct);
        return Ok(users.Select(ToProfile));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        return user is null ? NotFound() : Ok(ToProfile(user));
    }

    [HttpPost]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var normalized = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            return Conflict(new { error = "A user with this email already exists." });

        if (req.Password.Length is < 12 or > 128)
            return BadRequest(new { error = "Password must be 12–128 characters." });

        var user = new User
        {
            Email = req.Email.Trim(),
            NormalizedEmail = normalized,
            Name = req.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12)
        };

        var roles = await _db.Roles.Where(r => req.Roles.Contains(r.Name)).ToListAsync(ct);
        user.UserRoles = roles.Select(r => new UserRole { Role = r, User = user }).ToList();
        user.AppEntitlements = req.Apps
            .Distinct()
            .Select(a => new AppEntitlement { AppKey = a, User = user })
            .ToList();

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(AuthAuditEventType.UserCreated, user.Id, user.Email,
            detail: $"roles=[{string.Join(',', req.Roles)}] apps=[{string.Join(',', req.Apps)}]",
            ct: ct);

        var created = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.AppEntitlements)
            .FirstAsync(u => u.Id == user.Id, ct);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToProfile(created));
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

        user.Name = req.Name.Trim();
        user.IsActive = req.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // Reconcile roles. Mutate the tracked collection in place so EF emits
        // the right INSERT/DELETEs (reassigning the navigation reference confuses
        // change tracking and can leave orphaned rows).
        var desiredRoles = await _db.Roles.Where(r => req.Roles.Contains(r.Name)).ToListAsync(ct);
        user.UserRoles.Clear();
        foreach (var r in desiredRoles)
            user.UserRoles.Add(new UserRole { RoleId = r.Id, UserId = user.Id });

        // Reconcile app entitlements (invalidate tokens so revoked access takes effect).
        var before = user.AppEntitlements.Select(a => a.AppKey).ToHashSet();
        var after = req.Apps.Distinct().ToHashSet();
        user.AppEntitlements.Clear();
        foreach (var a in after)
            user.AppEntitlements.Add(new AppEntitlement { AppKey = a, UserId = user.Id });
        if (!before.SetEquals(after))
            user.TokenVersion++;

        await _db.SaveChangesAsync(ct);

        foreach (var granted in after.Except(before))
            await _audit.RecordAsync(AuthAuditEventType.AppAccessGranted, user.Id, user.Email, granted, ct: ct);
        foreach (var revoked in before.Except(after))
            await _audit.RecordAsync(AuthAuditEventType.AppAccessRevoked, user.Id, user.Email, revoked, ct: ct);

        return Ok(ToProfile(user));
    }
}
