using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Data;
using QMSofts.Identity.Models;
using QMSofts.Shared.Auth;

namespace QMSofts.Identity.Services;

/// <summary>
/// Idempotent startup seeder (same spirit as Parakh's WorkflowSeeder).
/// Ensures the canonical roles exist and, on a fresh DB, creates a bootstrap
/// admin from configuration so you can log in and create everyone else.
/// </summary>
public sealed class IdentitySeeder
{
    private readonly IdentityDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(IdentityDbContext db, IConfiguration config, ILogger<IdentitySeeder> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    private static readonly (string Name, string Description)[] CanonicalRoles =
    {
        (QmsRoles.Admin, "Full platform administration."),
        (QmsRoles.QualityManager, "Quality oversight across modules."),
        (QmsRoles.Auditor, "Conducts and records audits."),
        (QmsRoles.Reviewer, "Reviews and approves records."),
        (QmsRoles.User, "Standard application user.")
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // 1. Roles.
        foreach (var (name, description) in CanonicalRoles)
        {
            if (!await _db.Roles.AnyAsync(r => r.Name == name, ct))
                _db.Roles.Add(new Role { Name = name, Description = description });
        }
        await _db.SaveChangesAsync(ct);

        // 2. Bootstrap admin (only when there are no users at all).
        if (!await _db.Users.AnyAsync(ct))
        {
            var email = _config["QmsAuth:BootstrapAdminEmail"];
            var password = _config["QmsAuth:BootstrapAdminPassword"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "No users and no bootstrap admin configured. Set " +
                    "QmsAuth:BootstrapAdminEmail and QmsAuth:BootstrapAdminPassword to seed one.");
                return;
            }

            var adminRole = await _db.Roles.FirstAsync(r => r.Name == QmsRoles.Admin, ct);
            var admin = new User
            {
                Email = email.Trim(),
                NormalizedEmail = email.Trim().ToLowerInvariant(),
                Name = "Platform Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                UserRoles = new() { },
                AppEntitlements = new()
                {
                    new AppEntitlement { AppKey = QmsApps.Parakh },
                    new AppEntitlement { AppKey = QmsApps.Eres }
                }
            };
            admin.UserRoles.Add(new UserRole { Role = adminRole, User = admin });

            _db.Users.Add(admin);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded bootstrap admin {Email}.", email);
        }
    }
}
