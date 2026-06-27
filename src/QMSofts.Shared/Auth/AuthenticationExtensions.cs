using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace QMSofts.Shared.Auth;

/// <summary>
/// One-call wiring that any QMSofts app (Parakh, ERES, future) uses to trust
/// tokens issued by QMSofts.Identity. No shared secret: each app validates the
/// signature against Identity's published JWKS (public key) endpoint.
///
/// Usage in an app's Program.cs:
///     builder.Services.AddQmsAuthentication(builder.Configuration, requiredApp: QmsApps.Parakh);
///     ...
///     app.UseAuthentication();
///     app.UseAuthorization();
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddQmsAuthentication(
        this IServiceCollection services,
        IConfiguration config,
        string requiredApp)
    {
        // Identity service base URL, e.g. https://identity.qmsofts.com
        var authority = config["QmsAuth:Authority"]
            ?? throw new InvalidOperationException(
                "QmsAuth:Authority is not configured. Set it to the QMSofts.Identity base URL.");

        var audience = config["QmsAuth:Audience"] ?? "qmsofts";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Authority drives metadata + JWKS discovery:
                //   {authority}/.well-known/openid-configuration
                //   {authority}/.well-known/jwks.json
                options.Authority = authority;
                options.Audience = audience;

                // Railway terminates TLS at the edge; tokens travel internally.
                // Keep true in prod; allow override for local http testing.
                options.RequireHttpsMetadata =
                    config.GetValue<bool?>("QmsAuth:RequireHttpsMetadata") ?? true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = QmsClaimTypes.Name,
                    RoleClaimType = QmsClaimTypes.Role
                };
            });

        // Every endpoint in the app additionally requires the app-access
        // entitlement for THIS app. A valid QMSofts token is not enough —
        // the user must be entitled to enter this specific module.
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    ctx.User.HasClaim(QmsClaimTypes.AppAccess, requiredApp))
                .Build());

        return services;
    }

    /// <summary>Convenience reads off the authenticated principal.</summary>
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(QmsClaimTypes.UserId)?.Value;

    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(QmsClaimTypes.Email)?.Value;

    public static IEnumerable<string> GetApps(this ClaimsPrincipal user) =>
        user.FindAll(QmsClaimTypes.AppAccess).Select(c => c.Value);
}
