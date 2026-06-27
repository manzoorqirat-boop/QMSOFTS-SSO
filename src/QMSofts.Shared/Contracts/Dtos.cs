namespace QMSofts.Shared.Contracts;

/// <summary>Login request posted to Identity by the Shell.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Token response returned by Identity on successful login/refresh.</summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserProfile User);

public sealed record RefreshRequest(string RefreshToken);

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

/// <summary>Create/update payloads for Identity's user admin (used by Shell).</summary>
public sealed record CreateUserRequest(
    string Email,
    string Name,
    string Password,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Apps);

public sealed record UpdateUserRequest(
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Apps,
    bool IsActive);
