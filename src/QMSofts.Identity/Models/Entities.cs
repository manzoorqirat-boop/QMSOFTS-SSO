using System.ComponentModel.DataAnnotations;

namespace QMSofts.Identity.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public required string Email { get; set; }

    /// <summary>Normalized (lower, trimmed) email for case-insensitive uniqueness.</summary>
    [MaxLength(256)]
    public required string NormalizedEmail { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public required string PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Incremented to force token invalidation (revocation).</summary>
    public int TokenVersion { get; set; } = 1;

    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<UserRole> UserRoles { get; set; } = new();
    public List<AppEntitlement> AppEntitlements { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>e.g. Admin, QualityManager, Auditor. Matches QmsRoles constants.</summary>
    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>
/// Which apps a user may enter. Drives the Shell tiles AND is enforced by each
/// app on token validation (the qms_app claim).
/// </summary>
public class AppEntitlement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>App key, e.g. "parakh", "eres". Matches QmsApps constants.</summary>
    [MaxLength(50)]
    public required string AppKey { get; set; }

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [MaxLength(200)]
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}

/// <summary>
/// Append-only auth audit trail (Part 11 / Annex 11). Never updated or deleted.
/// </summary>
public class AuthAuditRecord
{
    public long Id { get; set; }
    public int EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UserId { get; set; }

    [MaxLength(256)]
    public string? Identifier { get; set; }

    [MaxLength(50)]
    public string? AppKey { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    [MaxLength(1000)]
    public string? Detail { get; set; }
}
