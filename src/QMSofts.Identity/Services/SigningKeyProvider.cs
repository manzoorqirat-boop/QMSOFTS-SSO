using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace QMSofts.Identity.Services;

/// <summary>
/// Holds the RSA key used to sign JWTs. The PUBLIC half is published at
/// /.well-known/jwks.json so every app (Parakh, ERES) can validate tokens
/// without any shared secret.
///
/// Key source priority:
///   1. QmsAuth:SigningKeyPem  (PKCS#8 PEM private key) — set this in Railway.
///   2. Generated in-memory at boot (DEV ONLY — tokens won't survive a restart
///      and other instances can't validate them; never use in production).
///
/// The "kid" lets you rotate keys later without breaking in-flight tokens.
/// </summary>
public sealed class SigningKeyProvider
{
    private readonly RSA _rsa;
    public RsaSecurityKey SecurityKey { get; }
    public string KeyId { get; }
    public SigningCredentials SigningCredentials { get; }

    public SigningKeyProvider(IConfiguration config, ILogger<SigningKeyProvider> logger)
    {
        var pem = config["QmsAuth:SigningKeyPem"];
        var rsa = RSA.Create();

        if (!string.IsNullOrWhiteSpace(pem))
        {
            rsa.ImportFromPem(pem);
            logger.LogInformation("Loaded RSA signing key from configuration.");
        }
        else
        {
            rsa = RSA.Create(2048);
            logger.LogWarning(
                "No QmsAuth:SigningKeyPem configured. Generated an EPHEMERAL key. " +
                "Tokens will not survive restarts and cannot be validated by other " +
                "instances. Set QmsAuth:SigningKeyPem in production.");
        }

        // Deterministic kid from the public key so the same key => same kid.
        var publicParams = rsa.ExportParameters(false);
        var modulusHash = SHA256.HashData(publicParams.Modulus!);
        KeyId = Base64UrlEncoder.Encode(modulusHash)[..16];

        _rsa = rsa;
        SecurityKey = new RsaSecurityKey(rsa) { KeyId = KeyId };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);
    }

    /// <summary>Builds the JWKS document exposing the public key.</summary>
    public object GetJwks()
    {
        var p = _rsa.ExportParameters(false);

        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(p.Modulus),
                    e = Base64UrlEncoder.Encode(p.Exponent)
                }
            }
        };
    }
}
