namespace QMSofts.Shared.Contracts;

/// <summary>Login request posted to Identity by the Shell.</summary>
public sealed record LoginRequest(string Email, string Password, string? SessionDecision = null);

/// <summary>Token response returned by Identity on successful login/refresh.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserProfile User);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Lightweight user projection. This is what apps cache as a "shadow user"
/// (id + name + email + roles) so existing queries/dropdowns keep working
/// without owning the users table. Apps subscribe to changes from Identity.
/// </summary>
public sealed record UserProfile(
    Guid Id,
    string Email,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Apps);

/// <summary>App access grant with the user's role within that app.</summary>
public sealed record AppGrant(string AppKey, string? Role);

/// <summary>Full user detail for the admin UI.</summary>
public sealed record UserDetail(
    Guid Id,
    string Email,
    string Name,
    string EmployeeId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AppGrant> Apps,
    string Status,
    bool MustChangePassword,
    bool IsLocked,
    DateTimeOffset? LockedUntil,
    bool HasActiveSession,
    DateTimeOffset PasswordLastChanged,
    DateTimeOffset CreatedAt);

/// <summary>Create/update payloads for Identity's user admin (used by Shell).</summary>
public sealed record CreateUserRequest(
    string Email,
    string Name,
    string Password,
    string? EmployeeId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AppGrant> Apps);

public sealed record UpdateUserRequest(
    string Name,
    string? EmployeeId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AppGrant> Apps,
    string Status);

public sealed record StatusChangeRequest(string Status);
