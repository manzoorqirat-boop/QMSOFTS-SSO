using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QMSofts.Identity.Services;
using QMSofts.Shared.Audit;
using QMSofts.Shared.Auth;

namespace QMSofts.Identity.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settings;
    private readonly AuditService _audit;

    public SettingsController(SettingsService settings, AuditService audit)
    {
        _settings = settings;
        _audit = audit;
    }

    public sealed record SettingUpdate(string Key, string Value);

    /// <summary>Returns all security settings (defaults merged with overrides).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _settings.GetAllAsync(ct));

    [HttpPut]
    [Authorize(Roles = QmsRoles.Admin)]
    public async Task<IActionResult> Update([FromBody] SettingUpdate req, CancellationToken ct)
    {
        if (!SettingsService.Defaults.ContainsKey(req.Key))
            return BadRequest(new { error = $"Unknown setting key: {req.Key}" });

        var actor = User.FindFirst(QmsClaimTypes.Email)?.Value ?? "admin";
        await _settings.SetAsync(req.Key, req.Value, actor, ct);
        await _audit.RecordAsync(AuthAuditEventType.SettingChanged, detail: $"{req.Key}={req.Value}", ct: ct);
        return Ok(await _settings.GetAllAsync(ct));
    }
}
