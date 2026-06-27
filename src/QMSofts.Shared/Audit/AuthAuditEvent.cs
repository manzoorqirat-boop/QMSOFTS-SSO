namespace QMSofts.Shared.Audit;

/// <summary>
/// Canonical authentication/authorization audit event kinds, shared so every
/// app records them consistently for 21 CFR Part 11 / EU Annex 11 traceability.
/// Identity records the auth-side events; apps may record entitlement denials.
/// </summary>
public enum AuthAuditEventType
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    Logout = 3,
    TokenRefreshed = 4,
    PasswordChanged = 5,
    AccountLocked = 6,
    AccountUnlocked = 7,
    RoleGranted = 8,
    RoleRevoked = 9,
    AppAccessGranted = 10,
    AppAccessRevoked = 11,
    UserCreated = 12,
    UserDeactivated = 13,
    /// <summary>A valid token tried to enter an app it isn't entitled to.</summary>
    AppEntryDenied = 14,
    /// <summary>Re-authentication tied to an electronic signature action.</summary>
    SignatureAuthentication = 15
}

/// <summary>
/// Immutable audit record contract. Stored append-only; never updated/deleted.
/// </summary>
public sealed record AuthAuditEvent
{
    public required AuthAuditEventType EventType { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Subject user id, when known (may be null for failed logins by unknown user).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Email/identifier as supplied (captured even on failure).</summary>
    public string? Identifier { get; init; }

    /// <summary>App key this event relates to, when app-scoped (e.g. "parakh").</summary>
    public string? AppKey { get; init; }

    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }

    /// <summary>Human-readable reason/context (e.g. "bad password", "role=Auditor granted").</summary>
    public string? Detail { get; init; }
}
