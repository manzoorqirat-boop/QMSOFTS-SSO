using Microsoft.AspNetCore.Mvc;
using QMSofts.Identity.Services;

namespace QMSofts.Identity.Controllers;

/// <summary>
/// Publishes the public-key material every QMSofts app uses to validate tokens.
/// The JwtBearer middleware in each app reads openid-configuration, which points
/// at jwks.json. No shared secret ever leaves this service.
/// </summary>
[ApiController]
public class WellKnownController : ControllerBase
{
    private readonly SigningKeyProvider _keys;
    private readonly IConfiguration _config;

    public WellKnownController(SigningKeyProvider keys, IConfiguration config)
    {
        _keys = keys;
        _config = config;
    }

    private string Authority => _config["QmsAuth:Authority"] ?? $"{Request.Scheme}://{Request.Host}";

    [HttpGet("/.well-known/openid-configuration")]
    public IActionResult OpenIdConfiguration()
    {
        var authority = Authority.TrimEnd('/');
        return Ok(new
        {
            issuer = authority,
            jwks_uri = $"{authority}/.well-known/jwks.json",
            token_endpoint = $"{authority}/api/auth/login",
            response_types_supported = new[] { "token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        });
    }

    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Jwks() => Ok(_keys.GetJwks());
}
