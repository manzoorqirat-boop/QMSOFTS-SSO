using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Data;
using QMSofts.Shared.Auth;

namespace QMSofts.Identity.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = QmsRoles.Admin)]
public class AuditController : ControllerBase
{
    private readonly IdentityDbContext _db;

    public AuditController(IdentityDbContext db) => _db = db;

    /// <summary>Security/auth audit events, most recent first.</summary>
    [HttpGet]
    public async Task<IActionResult> Events(
        [FromQuery] int limit = 200, [FromQuery] Guid? userId = null, CancellationToken ct = default)
    {
        var q = _db.AuthAuditRecords.AsQueryable();
        if (userId is { } uid) q = q.Where(a => a.UserId == uid);
        var rows = await q
            .OrderByDescending(a => a.OccurredAt)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(a => new
            {
                a.Id, eventType = a.EventType, a.OccurredAt, a.UserId,
                a.Identifier, a.AppKey, a.IpAddress, a.Detail
            })
            .ToListAsync(ct);
        return Ok(rows);
    }

    /// <summary>Before/after change history (users, settings), most recent first.</summary>
    [HttpGet("changes")]
    public async Task<IActionResult> Changes(
        [FromQuery] int limit = 200, [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null, CancellationToken ct = default)
    {
        var q = _db.ChangeHistories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(c => c.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(c => c.EntityId == entityId);
        var rows = await q
            .OrderByDescending(c => c.Timestamp)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(ct);
        return Ok(rows);
    }
}
