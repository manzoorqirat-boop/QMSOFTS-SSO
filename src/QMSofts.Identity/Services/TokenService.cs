using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using QMSofts.Identity.Models;
using QMSofts.Shared.Auth;

namespace QMSofts.Identity.Services;

public sealed class TokenService
{
    private readonly SigningKeyProvider _keys;
    private readonly IConfiguration _config;

    public TokenService(SigningKeyProvider keys, IConfiguration config)
    {
        _keys = keys;
        _config = config;
    }

    private string Issuer => _config["QmsAuth:Authority"]
        ?? throw new InvalidOperationException("QmsAuth:Authority not configured.");

    private string Audience => _config["QmsAuth:Audience"] ?? "qmsofts";

    private int AccessMinutes => _config.GetValue<int?>("QmsAuth:AccessTokenMinutes") ?? 30;

    public (string token, DateTimeOffset expiresAt) CreateAccessToken(
        User user, IEnumerable<string> roles, IEnumerable<string> apps)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(AccessMinutes);

        var claims = new List<Claim>
        {
            new(QmsClaimTypes.UserId, user.Id.ToString()),
            new(QmsClaimTypes.Email, user.Email),
            new(QmsClaimTypes.Name, user.Name),
            new(QmsClaimTypes.TokenVersion, user.TokenVersion.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Thin contract: global roles + app entitlements only.
        claims.AddRange(roles.Select(r => new Claim(QmsClaimTypes.Role, r)));
        claims.AddRange(apps.Select(a => new Claim(QmsClaimTypes.AppAccess, a)));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _keys.SigningCredentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }

    /// <summary>Opaque refresh token; only its hash is stored.</summary>
    public (string raw, string hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var raw = Base64UrlEncoder.Encode(bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return (raw, hash);
    }

    public static string HashRefreshToken(string raw)
    {
        var bytes = Base64UrlEncoder.DecodeBytes(raw);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
