using System.ComponentModel.DataAnnotations;

namespace QMSofts.Identity.Models;

/// <summary>Account lifecycle status, mirroring ERES (Active/Inactive/Disabled).</summary>
public enum UserStatus
{
    Active = 0,
    Inactive = 1,
    Disabled = 2
}

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

    /// <summary>Organisation employee/staff ID (optional), as in ERES.</summary>
    [MaxLength(100)]
    public string EmployeeId { get; set; } = "";

    public required string PasswordHash { get; set; }

    // ── Account lifecycle ──────────────────────────────────────────────
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>True once an account is created or reset; forces a change at next login.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Incremented to force token invalidation (revocation).</summary>
    public int TokenVersion { get; set; } = 1;

    // ── Password policy state ──────────────────────────────────────────
    public DateTimeOffset PasswordLastChanged { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Previous bcrypt hashes (most recent last), for reuse prevention. Capped at 10.</summary>
    public List<string> PasswordHistory { get; set; } = new();

    // ── Lockout ────────────────────────────────────────────────────────
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }

    // ── Single active session enforcement (mirrors ERES activeSession) ──
    /// <summary>Current valid session id; a login rotates this, invalidating older tokens.</summary>
    [MaxLength(64)]
    public string? ActiveSessionId { get; set; }
    public DateTimeOffset? SessionIssuedAt { get; set; }
    public DateTimeOffset? SessionExpiresAt { get; set; }
    [MaxLength(64)]
    public string? SessionIp { get; set; }

    /// <summary>Set by an admin "force logout"; tokens issued before this are rejected.</summary>
    public DateTimeOffset? ForceLogoutAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<UserRole> UserRoles { get; set; } = new();
    public List<AppEntitlement> AppEntitlements { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();

    public bool IsActive => Status == UserStatus.Active;
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

    /// <summary>
    /// The user's role WITHIN this app, e.g. "Approver" for ERES. Identity stores
    /// the assignment; each app defines what the role can do via its own frontend
    /// privilege config. Null/empty means "entitled, role assigned app-side".
    /// </summary>
    [MaxLength(100)]
    public string? Role { get; set; }

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

/// <summary>
/// Configurable security knobs (mirrors ERES Settings). Stored as string values,
/// parsed at use. Seeded with GxP-aligned defaults; editable by admins.
/// Keys: minPasswordLength, forceComplexity, passwordHistory, passwordExpiry,
/// maxFailedAttempts, lockoutDuration, sessionTimeout.
/// </summary>
public class Setting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public required string Key { get; set; }

    [MaxLength(500)]
    public string Value { get; set; } = "";

    [MaxLength(200)]
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Generic before/after change record (mirrors ERES ChangeHistory). Every
/// create/update/deactivate on a user writes one. Append-only; never deleted —
/// the permanent record for 21 CFR Part 11 / Annex 11.
/// </summary>
public class ChangeHistory
{
    public long Id { get; set; }

    [MaxLength(50)]
    public required string EntityType { get; set; } // "User" | "Setting" | ...

    [MaxLength(100)]
    public required string EntityId { get; set; }

    [MaxLength(200)]
    public string EntityLabel { get; set; } = "";

    [MaxLength(30)]
    public required string Action { get; set; } // CREATE | UPDATE | DEACTIVATE | REACTIVATE

    /// <summary>JSON snapshot before the change (sensitive fields stripped).</summary>
    public string? Before { get; set; }
    /// <summary>JSON snapshot after the change.</summary>
    public string? After { get; set; }

    /// <summary>Comma-separated list of fields that changed.</summary>
    [MaxLength(500)]
    public string ChangedFields { get; set; } = "";

    [MaxLength(200)]
    public required string ChangedBy { get; set; }
    [MaxLength(100)]
    public string ChangedByRole { get; set; } = "";

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
