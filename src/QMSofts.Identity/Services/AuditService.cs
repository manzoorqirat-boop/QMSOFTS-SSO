using Microsoft.AspNetCore.Http;
using QMSofts.Identity.Data;
using QMSofts.Identity.Models;
using QMSofts.Shared.Audit;

namespace QMSofts.Identity.Services;

/// <summary>
/// Writes the append-only authentication audit trail. Captures IP/user-agent
/// from the current request automatically. Records are never updated or deleted.
/// </summary>
public sealed class AuditService
{
    private readonly IdentityDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(IdentityDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    /// <summary>The caller's IP from the current request, if available.</summary>
    public string? CurrentIp() => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public async Task RecordAsync(
        AuthAuditEventType type,
        Guid? userId = null,
        string? identifier = null,
        string? appKey = null,
        string? detail = null,
        CancellationToken ct = default)
    {
        var ctx = _http.HttpContext;
        var record = new AuthAuditRecord
        {
            EventType = (int)type,
            OccurredAt = DateTimeOffset.UtcNow,
            UserId = userId,
            Identifier = identifier,
            AppKey = appKey,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx?.Request.Headers.UserAgent.ToString(),
            Detail = detail
        };

        _db.AuthAuditRecords.Add(record);
        await _db.SaveChangesAsync(ct);
    }
}
