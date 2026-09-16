using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Admin.Users;
using KeplerTalento.Domain.Identity;
using MediatR;

namespace KeplerTalento.Application.Features.Admin.Roles;

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleResponse>>;

public sealed record GetRoleQuery(Guid Id) : IRequest<RoleResponse>;

public sealed record CreateRoleCommand(string? Name, string? Label, IReadOnlyList<string>? Permissions)
    : IRequest<RoleResponse>;

public sealed record UpdateRoleLabelCommand(Guid Id, string? Label, uint Version) : IRequest<RoleResponse>;

public sealed record SetRolePermissionsCommand(Guid Id, IReadOnlyList<string>? Permissions, uint Version)
    : IRequest<RoleResponse>;

public sealed record SetRoleActiveCommand(Guid Id, bool IsActive, uint Version) : IRequest<RoleResponse>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(RoleName.MaximumLength);
        RuleFor(command => command.Label).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Permissions).NotEmpty();
    }
}

public sealed class UpdateRoleLabelValidator : AbstractValidator<UpdateRoleLabelCommand>
{
    public UpdateRoleLabelValidator()
    {
        RuleFor(command => command.Label).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

public sealed class SetRolePermissionsValidator : AbstractValidator<SetRolePermissionsCommand>
{
    public SetRolePermissionsValidator()
    {
        RuleFor(command => command.Permissions).NotEmpty();
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

public sealed class SetRoleActiveValidator : AbstractValidator<SetRoleActiveCommand>
{
    public SetRoleActiveValidator() => RuleFor(command => command.Version).MustBeAnIssuedVersion();
}

/// <summary>
/// Checks a requested permission set against the closed catalogue.
/// </summary>
/// <remarks>
/// The catalogue is code and the assignment is data (design D5). A role holding a permission
/// nothing in the API knows about is not a harmless typo: it looks granted in the administration
/// screen and grants nothing, which is the most confusing possible outcome.
/// </remarks>
internal static class PermissionSet
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<string>? requested)
    {
        var values = (requested ?? [])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (values.Count == 0)
        {
            throw new RequestValidationException(
                [new ValidationIssue("permissions", "admin.role.emptyPermissions", "Un rol necesita al menos un permiso.")]);
        }

        var unknown = values
            .Where(permission => !Permissions.All.Contains(permission, StringComparer.Ordinal))
            .ToList();

        if (unknown.Count > 0)
        {
            throw new RequestValidationException(
            [
                new ValidationIssue(
                    "permissions",
                    "admin.role.unknownPermission",
                    $"Estos permisos no existen: {string.Join(", ", unknown)}."),
            ]);
        }

        return values;
    }
}

public sealed class ListRolesHandler(IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<IReadOnlyList<RoleResponse>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var stored = await roles.ListAsync(cancellationToken);
        return [.. stored.Select(AdminMapping.ToResponse)];
    }
}

public sealed class GetRoleHandler(IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<GetRoleQuery, RoleResponse>
{
    public async Task<RoleResponse> Handle(GetRoleQuery request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var role = await roles.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.RoleNotFound();
        return AdminMapping.ToResponse(role);
    }
}

public sealed class CreateRoleHandler(IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<CreateRoleCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var permissions = PermissionSet.Validate(request.Permissions);

        Role role;
        try
        {
            // Created as a non-system role: only the migration seeds system roles, because
            // "system" means "this installation did not invent it" (design D5).
            role = new Role(
                Guid.CreateVersion7(),
                request.Name!,
                request.Label!,
                isSystem: false,
                permissions,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(
                [new ValidationIssue(exception.ParamName ?? "request", "admin.role.invalid", exception.Message)]);
        }

        roles.Add(role);
        return await roles.SaveAsync(cancellationToken) == RoleSaveOutcome.NameConflict
            ? throw AdminGuards.RoleNameConflict()
            : AdminMapping.ToResponse(role);
    }
}

/// <summary>
/// Relabels a role. Deliberately not a rename: <c>Name</c> is a foreign key target and immutable
/// (design D7), and the label is what an administrator actually wants to change.
/// </summary>
public sealed class UpdateRoleLabelHandler(IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<UpdateRoleLabelCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(UpdateRoleLabelCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var role = await roles.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.RoleNotFound();
        roles.ExpectVersion(role, request.Version);

        try
        {
            role.Relabel(request.Label!, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(
                [new ValidationIssue("label", "admin.role.invalid", exception.Message)]);
        }

        return await roles.SaveAsync(cancellationToken) switch
        {
            RoleSaveOutcome.ConcurrencyConflict => throw AdminGuards.RoleConcurrencyConflict(),
            _ => AdminMapping.ToResponse(role),
        };
    }
}

public sealed class SetRolePermissionsHandler(IRoleRepository roles, IUserRepository users, ICurrentActor actor)
    : IRequestHandler<SetRolePermissionsCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(SetRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var permissions = PermissionSet.Validate(request.Permissions);
        var role = await roles.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.RoleNotFound();

        // The lockout path the browser never had: a role can be edited out of conferring
        // administration, and from the roles screen nobody was watching (design D6).
        await AdminInvariants.RequireRoleEditLeavesAnAdministratorAsync(users, role, permissions, cancellationToken);

        roles.ExpectVersion(role, request.Version);
        try
        {
            role.ReplacePermissions(permissions, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(
                [new ValidationIssue("permissions", "admin.role.invalid", exception.Message)]);
        }

        return await roles.SaveAsync(cancellationToken) switch
        {
            RoleSaveOutcome.ConcurrencyConflict => throw AdminGuards.RoleConcurrencyConflict(),
            _ => AdminMapping.ToResponse(role),
        };
    }
}

public sealed class SetRoleActiveHandler(IRoleRepository roles, IUserRepository users, ICurrentActor actor)
    : IRequestHandler<SetRoleActiveCommand, RoleResponse>
{
    public async Task<RoleResponse> Handle(SetRoleActiveCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageRoles(actor);
        var role = await roles.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.RoleNotFound();

        if (!request.IsActive)
        {
            AdminInvariants.RequireSystemRoleMayBeDeactivated(role);
            await AdminInvariants.RequireRoleIsUnusedAsync(users, role.Name, cancellationToken);
            await AdminInvariants.RequireRoleDeactivationLeavesAnAdministratorAsync(users, role, cancellationToken);
        }

        roles.ExpectVersion(role, request.Version);
        var now = DateTimeOffset.UtcNow;
        if (request.IsActive)
        {
            role.Reactivate(now);
        }
        else
        {
            role.Deactivate(now);
        }

        return await roles.SaveAsync(cancellationToken) switch
        {
            RoleSaveOutcome.ConcurrencyConflict => throw AdminGuards.RoleConcurrencyConflict(),
            _ => AdminMapping.ToResponse(role),
        };
    }
}
