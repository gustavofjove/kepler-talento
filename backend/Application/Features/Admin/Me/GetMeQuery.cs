using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using MediatR;

namespace KeplerTalento.Application.Features.Admin.Me;

/// <summary>The caller's own profile and effective permissions.</summary>
public sealed record GetMeQuery : IRequest<MeResponse>;

/// <summary>
/// Authentication only, no permission: a caller always reads their own profile, and requiring a
/// permission to do so would mean a newly provisioned user could not load the application that
/// is about to tell them they may do nothing.
/// </summary>
/// <remarks>
/// It returns only the caller's own row, and never the provider's subject — the browser has no
/// use for it and it is the key that identifies this person at the provider (design D2).
/// </remarks>
public sealed class GetMeHandler(IUserRepository users, IRoleRepository roles, ICurrentActor actor)
    : IRequestHandler<GetMeQuery, MeResponse>
{
    public async Task<MeResponse> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        AdminGuards.RequireAuthenticated(actor);

        // An authenticated caller always has a resolved user id: the resolver refuses a token
        // whose subject it could not turn into an active user, so there is no authenticated
        // state without one.
        var userId = actor.UserId ?? throw AdminGuards.UserNotFound();
        var user = await users.FindAsync(userId, cancellationToken) ?? throw AdminGuards.UserNotFound();
        var role = await roles.FindByNameAsync(user.RoleName, cancellationToken);

        // An inactive role leaves the caller authenticated with no permissions: they are still
        // here, they just cannot currently do anything (design D3). The label still comes back
        // so the screen can say which role it is.
        var permissions = role is { IsActive: true } ? role.Permissions : [];

        return new MeResponse(
            user.Id,
            user.DisplayName,
            user.Email,
            user.RoleName,
            role?.Label ?? user.RoleName,
            user.IsActive,
            permissions);
    }
}
