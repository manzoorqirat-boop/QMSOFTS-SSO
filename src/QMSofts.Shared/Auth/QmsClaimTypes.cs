namespace QMSofts.Shared.Auth;

/// <summary>
/// The canonical claim contract issued by QMSofts.Identity and consumed by
/// every app (Parakh, ERES, future modules). Keep this DELIBERATELY THIN.
/// Identity owns: who the user is + their global roles + which apps they may enter.
/// Each app owns: mapping roles -> its own permissions (locally, via config table).
/// </summary>
public static class QmsClaimTypes
{
    /// <summary>Stable user id (GUID string). Maps to standard "sub".</summary>
    public const string UserId = "sub";

    public const string Email = "email";

    /// <summary>Display name, used for dropdowns/audit (e.g. Parakh lead-auditor).</summary>
    public const string Name = "name";

    /// <summary>Global role. Repeatable. e.g. Admin, QualityManager, Auditor.</summary>
    public const string Role = "role";

    /// <summary>
    /// App entitlement. Repeatable. The keys of apps this user may launch,
    /// e.g. "parakh", "eres". Drives which tiles render in the Shell AND
    /// is enforced by each app on token validation.
    /// </summary>
    public const string AppAccess = "qms_app";

    /// <summary>Token version, for forced-revocation scenarios.</summary>
    public const string TokenVersion = "qms_tv";

    /// <summary>Per-app role assignment, value "app:role" e.g. "eres:Approver". Repeatable.</summary>
    public const string AppRole = "qms_approle";

    /// <summary>Single-session id; apps may reject tokens whose sid is superseded.</summary>
    public const string SessionId = "qms_sid";
}

/// <summary>Well-known app keys for the QMSofts suite.</summary>
public static class QmsApps
{
    public const string Parakh = "parakh";
    public const string Eres = "eres";
}

/// <summary>Global roles owned by Identity. Apps map these to local permissions.</summary>
public static class QmsRoles
{
    public const string Admin = "Admin";
    public const string QualityManager = "QualityManager";
    public const string Auditor = "Auditor";
    public const string Reviewer = "Reviewer";
    public const string User = "User";
}
