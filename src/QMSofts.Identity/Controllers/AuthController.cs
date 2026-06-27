using Microsoft.AspNetCore.Mvc;
using QMSofts.Identity.Services;
using QMSofts.Shared.Contracts;

namespace QMSofts.Identity.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        var result = await _auth.LoginAsync(req, ct);
        return result.Succeeded
            ? Ok(result.Tokens)
            : Unauthorized(new { error = result.Error });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var result = await _auth.RefreshAsync(req, ct);
        return result.Succeeded
            ? Ok(result.Tokens)
            : Unauthorized(new { error = result.Error });
    }
}
