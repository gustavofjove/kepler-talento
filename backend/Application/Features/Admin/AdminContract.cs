using KeplerTalento.Domain.Identity;

namespace KeplerTalento.Application.Features.Admin;

/// <summary>
/// A user as an administrator sees them.
/// </summary>
/// <remarks>
/// The provider's subject is deliberately absent. It is the key that identifies this person at
/// the identity provider, and nothing in the product needs it; <see cref="Id"/> is what responses
/// and links carry (design D2, and the spec requirement that identity data stays out of
/// responses).
/// </remarks>
public sealed record UserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string RoleName,
    bool IsActive,
    DateTimeOffset? LastSignInAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record UserPageResponse(
    IReadOnlyList<UserResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string Label,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

/// <summary>
/// The caller's own profile. Carries the effective permission set so the browser can hide
/// controls the API would refuse anyway — decoration over an authoritative server decision,
/// never the control itself.
/// </summary>
public sealed record MeResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string RoleName,
    string RoleLabel,
    bool IsActive,
    IReadOnlyList<string> Permissions);

public static class AdminMapping
{
    public static UserResponse ToResponse(User user) => new(
        user.Id,
        user.DisplayName,
        user.Email,
        user.RoleName,
        user.IsActive,
        user.LastSignInAtUtc,
        user.CreatedAtUtc,
        user.UpdatedAtUtc,
        user.Version);

    public static RoleResponse ToResponse(Role role) => new(
        role.Id,
        role.Name,
        role.Label,
        role.IsSystem,
        role.Permissions,
        role.IsActive,
        role.CreatedAtUtc,
        role.UpdatedAtUtc,
        role.Version);
}
