using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Domain.Identity;
using MediatR;

namespace KeplerTalento.Application.Features.Admin.Users;

public sealed record ListUsersQuery(bool IncludeInactive, int Page, int PageSize) : IRequest<UserPageResponse>;

public sealed record GetUserQuery(Guid Id) : IRequest<UserResponse>;

public sealed record CreateUserCommand(string? DisplayName, string? Email, string? RoleName)
    : IRequest<UserResponse>;

public sealed record UpdateUserCommand(Guid Id, string? DisplayName, uint Version) : IRequest<UserResponse>;

public sealed record SetUserRoleCommand(Guid Id, string? RoleName, uint Version) : IRequest<UserResponse>;

public sealed record SetUserActiveCommand(Guid Id, bool IsActive, uint Version) : IRequest<UserResponse>;

/// <summary>
/// Versions are <c>xmin</c> and never zero for a stored row, so 0 is what an absent or
/// unparseable version arrives as. It is refused as invalid rather than treated as "whatever is
/// current", which would silently overwrite a concurrent change.
/// </summary>
internal static class AdminValidation
{
    public static IRuleBuilderOptions<T, uint> MustBeAnIssuedVersion<T>(this IRuleBuilder<T, uint> rule) =>
        rule.GreaterThan(0u).WithMessage("La versión indicada no es válida.");
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320);
        RuleFor(command => command.RoleName).NotEmpty();
    }
}

public sealed class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 10_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(command => command.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

public sealed class SetUserRoleValidator : AbstractValidator<SetUserRoleCommand>
{
    public SetUserRoleValidator()
    {
        RuleFor(command => command.RoleName).NotEmpty();
        RuleFor(command => command.Version).MustBeAnIssuedVersion();
    }
}

public sealed class SetUserActiveValidator : AbstractValidator<SetUserActiveCommand>
{
    public SetUserActiveValidator() => RuleFor(command => command.Version).MustBeAnIssuedVersion();
}

public sealed class ListUsersHandler(IUserRepository users, ICurrentActor actor)
    : IRequestHandler<ListUsersQuery, UserPageResponse>
{
    public async Task<UserPageResponse> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);
        var stored = await users.ListAsync(
            request.IncludeInactive,
            request.Page,
            request.PageSize,
            cancellationToken);
        return new UserPageResponse(
            [.. stored.Items.Select(AdminMapping.ToResponse)],
            request.Page,
            request.PageSize,
            stored.TotalCount);
    }
}

public sealed class GetUserHandler(IUserRepository users, ICurrentActor actor)
    : IRequestHandler<GetUserQuery, UserResponse>
{
    public async Task<UserResponse> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);
        var user = await users.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.UserNotFound();
        return AdminMapping.ToResponse(user);
    }
}

/// <summary>
/// Creates a user before they have ever signed in, so an administrator can put someone in the
/// right role in advance. The row carries no provider subject; it is linked to one by
/// <c>CallerIdentityResolver</c> on that person's first sign-in, matching on the email address.
/// </summary>
public sealed class CreateUserHandler(IUserRepository users, IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<CreateUserCommand, UserResponse>
{
    public async Task<UserResponse> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);

        var role = await roles.FindByNameAsync(request.RoleName!, cancellationToken)
            ?? throw AdminGuards.RoleNotFound();

        User user;
        try
        {
            user = new User(
                Guid.CreateVersion7(),
                externalSubject: null,
                request.DisplayName!,
                request.Email!,
                role.Name,
                DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new RequestValidationException(
                [new ValidationIssue(exception.ParamName ?? "request", "admin.user.invalid", exception.Message)]);
        }

        users.Add(user);
        // No read-then-check for the address: the unique index is the only answer that holds
        // under two concurrent creations, so the conflict is detected where it is decided.
        return await users.SaveAsync(cancellationToken) == UserSaveOutcome.NaturalKeyConflict
            ? throw AdminGuards.UserEmailConflict()
            : AdminMapping.ToResponse(user);
    }
}

public sealed class UpdateUserHandler(IUserRepository users, ICurrentActor actor)
    : IRequestHandler<UpdateUserCommand, UserResponse>
{
    public async Task<UserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);
        var user = await users.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.UserNotFound();
        users.ExpectVersion(user, request.Version);
        user.Rename(request.DisplayName!, DateTimeOffset.UtcNow);

        return await users.SaveAsync(cancellationToken) switch
        {
            UserSaveOutcome.ConcurrencyConflict => throw AdminGuards.UserConcurrencyConflict(),
            UserSaveOutcome.NaturalKeyConflict => throw AdminGuards.UserEmailConflict(),
            _ => AdminMapping.ToResponse(user),
        };
    }
}

public sealed class SetUserRoleHandler(IUserRepository users, IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<SetUserRoleCommand, UserResponse>
{
    public async Task<UserResponse> Handle(SetUserRoleCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);
        AdminInvariants.RequireNotSelfRoleChange(actor, request.Id);

        var user = await users.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.UserNotFound();
        var role = await roles.FindByNameAsync(request.RoleName!, cancellationToken)
            ?? throw AdminGuards.RoleNotFound();

        // Only a move that takes administration away can cause a lockout, so the count is asked
        // for only when it can change the answer.
        var losesAdministration = user.IsActive
            && !role.Grants(Permissions.UsersManage)
            && await CurrentlyAdministersAsync(roles, user, cancellationToken);

        if (losesAdministration)
        {
            await AdminInvariants.RequireAnotherAdministratorRemainsAsync(users, user.Id, cancellationToken);
        }

        users.ExpectVersion(user, request.Version);
        user.AssignRole(role.Name, DateTimeOffset.UtcNow);

        return await users.SaveAsync(cancellationToken) switch
        {
            UserSaveOutcome.ConcurrencyConflict => throw AdminGuards.UserConcurrencyConflict(),
            _ => AdminMapping.ToResponse(user),
        };
    }

    private static async Task<bool> CurrentlyAdministersAsync(
        IRoleRepository roles,
        User user,
        CancellationToken cancellationToken)
    {
        var current = await roles.FindByNameAsync(user.RoleName, cancellationToken);
        return current?.Grants(Permissions.UsersManage) == true;
    }
}

public sealed class SetUserActiveHandler(IUserRepository users, IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<SetUserActiveCommand, UserResponse>
{
    public async Task<UserResponse> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireManageUsers(actor);

        var user = await users.FindAsync(request.Id, cancellationToken) ?? throw AdminGuards.UserNotFound();

        if (!request.IsActive)
        {
            AdminInvariants.RequireNotSelfDeactivation(actor, request.Id);

            var role = await roles.FindByNameAsync(user.RoleName, cancellationToken);
            if (user.IsActive && role?.Grants(Permissions.UsersManage) == true)
            {
                await AdminInvariants.RequireAnotherAdministratorRemainsAsync(users, user.Id, cancellationToken);
            }
        }

        users.ExpectVersion(user, request.Version);
        var now = DateTimeOffset.UtcNow;
        if (request.IsActive)
        {
            user.Reactivate(now);
        }
        else
        {
            // Deactivation, never deletion (non-negotiable 5). ktl_runtime holds no DELETE on
            // ADM_Users, so this is the only removal there is.
            user.Deactivate(now);
        }

        return await users.SaveAsync(cancellationToken) switch
        {
            UserSaveOutcome.ConcurrencyConflict => throw AdminGuards.UserConcurrencyConflict(),
            _ => AdminMapping.ToResponse(user),
        };
    }
}
