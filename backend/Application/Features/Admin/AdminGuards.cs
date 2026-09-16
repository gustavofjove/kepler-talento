using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Common.Errors;

namespace KeplerTalento.Application.Features.Admin;

/// <summary>
/// Authorization and refusals shared by the user and role slices.
/// </summary>
/// <remarks>
/// Every handler calls its guard first, before validating anything. Refusing before validation
/// is what stops an unauthorized caller probing the administration surface through validation
/// problems — learning which role names exist by watching which ones come back "not found"
/// rather than "invalid".
/// </remarks>
public static class AdminGuards
{
    public static void RequireManageUsers(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.UsersManage))
        {
            throw new ForbiddenException();
        }
    }

    public static void RequireManageRoles(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated || !actor.HasPermission(Permissions.RolesManage))
        {
            throw new ForbiddenException();
        }
    }

    /// <summary>Authentication only: a caller always reads their own profile.</summary>
    public static void RequireAuthenticated(ICurrentActor actor)
    {
        if (!actor.IsAuthenticated)
        {
            throw new ForbiddenException();
        }
    }

    public static NotFoundException UserNotFound() =>
        new("admin.user.notFound", "El usuario indicado no existe.");

    public static NotFoundException RoleNotFound() =>
        new("admin.role.notFound", "El rol indicado no existe.");

    public static ConflictException UserConcurrencyConflict() =>
        new("admin.user.versionConflict", "El usuario ha cambiado desde que lo consultó. Vuelva a cargarlo.");

    public static ConflictException RoleConcurrencyConflict() =>
        new("admin.role.versionConflict", "El rol ha cambiado desde que lo consultó. Vuelva a cargarlo.");

    /// <summary>
    /// The address is already registered. This is told only to a caller who already holds
    /// <c>users.manage</c> and can therefore list every user anyway, so it discloses nothing
    /// they could not already read.
    /// </summary>
    public static ConflictException UserEmailConflict() =>
        new("admin.user.emailConflict", "Ya existe un usuario con ese correo electrónico.");

    public static ConflictException RoleNameConflict() =>
        new("admin.role.nameConflict", "Ya existe un rol con ese nombre.");

    /// <summary>
    /// The write would leave the installation with nobody able to administer it. Refused on all
    /// three paths that can reach it: deactivating a user, changing their role, and removing
    /// <c>users.manage</c> from a role (design D6).
    /// </summary>
    public static ConflictException LastAdministrator() =>
        new(
            "admin.lastAdministrator",
            "Esta operación dejaría la instalación sin ningún administrador activo.");

    /// <summary>
    /// A caller changing their own role or deactivating themselves. Refused because the mistake
    /// is unrecoverable by the person making it: they lose the permission that would let them
    /// undo it.
    /// </summary>
    public static ConflictException SelfTarget(string code, string message) =>
        new(code, message);

    public static ConflictException CannotChangeOwnRole() =>
        SelfTarget("admin.user.selfRoleChange", "No puede cambiar su propio rol.");

    public static ConflictException CannotDeactivateSelf() =>
        SelfTarget("admin.user.selfDeactivation", "No puede desactivar su propia cuenta.");

    public static ConflictException SystemRoleProtected(string code, string message) =>
        new(code, message);

    public static ConflictException CannotDeactivateSystemRole() =>
        SystemRoleProtected("admin.role.systemDeactivation", "No se puede desactivar un rol del sistema.");

    /// <summary>
    /// A role still held by at least one user, active or not. Deactivating it would leave those
    /// users holding a role that grants nothing, which is a lockout by another name.
    /// </summary>
    public static ConflictException RoleStillInUse() =>
        new("admin.role.inUse", "No se puede desactivar un rol que todavía tiene usuarios asignados.");
}
