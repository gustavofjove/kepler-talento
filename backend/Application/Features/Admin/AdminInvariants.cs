using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Identity;

namespace KeplerTalento.Application.Features.Admin;

/// <summary>
/// The rules that keep an installation administrable, in one place.
/// </summary>
/// <remarks>
/// <para>
/// These used to live in the browser's <c>profile.service.ts</c>, where they were advice rather
/// than enforcement — a caller talking to the API directly was bound by none of them, and the
/// roles screen could edit its way into a lockout that the users screen prevented.
/// </para>
/// <para>
/// "Administrator" means <em>an active user whose active role grants <c>users.manage</c></em>,
/// not a role called <c>rrhh_admin</c>. Defining it by permission is what makes the rule survive
/// an installation that renames or reshapes its roles (design D6).
/// </para>
/// <para>
/// Every check runs after the permission guard and before the save, inside the handler's write
/// transaction. Asking "is there another active administrator?" outside the transaction is a
/// race that deactivates the last two administrators concurrently.
/// </para>
/// </remarks>
public static class AdminInvariants
{
    /// <summary>
    /// Refuses a write that would remove the last administrator. Asked with the affected user
    /// excluded, so the question is "would anyone be left <em>afterwards</em>".
    /// </summary>
    public static async Task RequireAnotherAdministratorRemainsAsync(
        IUserRepository users,
        Guid affectedUserId,
        CancellationToken cancellationToken)
    {
        var remaining = await users.CountActiveHoldersOfPermissionAsync(
            Permissions.UsersManage,
            excludingUserId: affectedUserId,
            cancellationToken);

        if (remaining == 0)
        {
            throw AdminGuards.LastAdministrator();
        }
    }

    /// <summary>
    /// Refuses a role edit that would strip <c>users.manage</c> while the role's holders are the
    /// only administrators left. This is the path <c>profile.service.ts</c> never had, because
    /// roles could not be edited from the users screen.
    /// </summary>
    public static async Task RequireRoleEditLeavesAnAdministratorAsync(
        IUserRepository users,
        Role role,
        IReadOnlyCollection<string> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var grantedBefore = role.Grants(Permissions.UsersManage);
        var grantsAfter = replacementPermissions.Contains(Permissions.UsersManage, StringComparer.Ordinal);

        if (!grantedBefore || grantsAfter)
        {
            // Either the role never conferred administration, or it still will. Nothing to lose.
            return;
        }

        if (!await AnyOtherActiveAdministratorRoleHolderAsync(users, role, cancellationToken))
        {
            throw AdminGuards.LastAdministrator();
        }
    }

    /// <summary>Deactivating a role that currently confers administration is the same question.</summary>
    public static async Task RequireRoleDeactivationLeavesAnAdministratorAsync(
        IUserRepository users,
        Role role,
        CancellationToken cancellationToken)
    {
        if (!role.Grants(Permissions.UsersManage))
        {
            return;
        }

        if (!await AnyOtherActiveAdministratorRoleHolderAsync(users, role, cancellationToken))
        {
            throw AdminGuards.LastAdministrator();
        }
    }

    /// <summary>
    /// Whether an active administrator would remain if this role stopped conferring
    /// administration. Counted across every active holder of any administering role, then
    /// reduced by the ones this role accounts for.
    /// </summary>
    private static async Task<bool> AnyOtherActiveAdministratorRoleHolderAsync(
        IUserRepository users,
        Role role,
        CancellationToken cancellationToken)
    {
        var total = await users.CountActiveHoldersOfPermissionAsync(
            Permissions.UsersManage,
            excludingUserId: null,
            cancellationToken);

        var throughThisRole = await users.CountActiveHoldersOfRoleAsync(role.Name, cancellationToken);

        return total - throughThisRole > 0;
    }

    /// <summary>
    /// A caller cannot change their own role. The mistake is unrecoverable by the person making
    /// it: demoting yourself takes away the permission that would let you undo it.
    /// </summary>
    public static void RequireNotSelfRoleChange(ICurrentActor actor, Guid targetUserId)
    {
        if (actor.UserId is { } callerId && callerId == targetUserId)
        {
            throw AdminGuards.CannotChangeOwnRole();
        }
    }

    /// <summary>A caller cannot deactivate themselves, for the same reason.</summary>
    public static void RequireNotSelfDeactivation(ICurrentActor actor, Guid targetUserId)
    {
        if (actor.UserId is { } callerId && callerId == targetUserId)
        {
            throw AdminGuards.CannotDeactivateSelf();
        }
    }

    /// <summary>
    /// System roles may have their label and permission set changed but may not be renamed,
    /// emptied or deactivated (design D5). An installation that wants <c>rrhh_user</c> to stop
    /// downloading documents should not have to clone the role to do it; what must not happen is
    /// a system role vanishing while users hold it.
    /// </summary>
    public static void RequireSystemRoleMayBeDeactivated(Role role)
    {
        if (role.IsSystem)
        {
            throw AdminGuards.CannotDeactivateSystemRole();
        }
    }

    /// <summary>
    /// Refuses deactivating a role that users still hold, system or not. Those users would keep
    /// a role that grants nothing, which is a lockout by another name.
    /// </summary>
    public static async Task RequireRoleIsUnusedAsync(
        IUserRepository users,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (await users.AnyWithRoleAsync(roleName, cancellationToken))
        {
            throw AdminGuards.RoleStillInUse();
        }
    }
}
