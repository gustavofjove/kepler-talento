using KeplerTalento.Application.Features.Admin;
using KeplerTalento.Application.Features.Admin.Roles;
using KeplerTalento.Application.Features.Admin.Users;
using MediatR;

namespace KeplerTalento.Web.Features.Admin;

/// <summary>
/// User and role administration.
/// </summary>
/// <remarks>
/// There is deliberately no DELETE verb: users and roles are retired by deactivation, never
/// physically deleted (non-negotiable 5), and <c>ktl_runtime</c> holds no <c>DELETE</c> on either
/// table so the database refuses it even if an endpoint were added.
///
/// Authorization is not repeated here. Every handler calls its own guard before validating —
/// <c>AdminGuards.RequireManageUsers</c> or <c>RequireManageRoles</c> — and the HTTP layer adds
/// the authentication requirement through the fallback policy in <c>Program.cs</c>. Two places,
/// both failing closed.
/// </remarks>
public static class AdminEndpoints
{
    public sealed record CreateUserRequest(string DisplayName, string Email, string RoleName);

    public sealed record UpdateUserRequest(string DisplayName, uint Version);

    public sealed record SetUserRoleRequest(string RoleName, uint Version);

    public sealed record SetUserActiveRequest(bool IsActive, uint Version);

    public sealed record CreateRoleRequest(string Name, string Label, IReadOnlyList<string> Permissions);

    public sealed record UpdateRoleLabelRequest(string Label, uint Version);

    public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions, uint Version);

    public sealed record SetRoleActiveRequest(bool IsActive, uint Version);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapUsers(endpoints.MapGroup("/api/admin/users")
            .WithTags("Administration")
            .RequireAuthorization(KeplerTalento.Application.Abstractions.Identity.Permissions.UsersManage));
        MapRoles(endpoints.MapGroup("/api/admin/roles")
            .WithTags("Administration")
            .RequireAuthorization(KeplerTalento.Application.Abstractions.Identity.Permissions.RolesManage));
        return endpoints;
    }

    private static void MapUsers(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
                bool? includeInactive,
                int? page,
                int? pageSize,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new ListUsersQuery(includeInactive ?? false, page ?? 1, pageSize ?? 25),
                    cancellationToken)))
            .WithName("ListUsers")
            .Produces<UserPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetUserQuery(id), cancellationToken)))
            .WithName("GetUser")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateUserRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(
                    new CreateUserCommand(request.DisplayName, request.Email, request.RoleName),
                    cancellationToken);
                return Results.Created($"/api/admin/users/{created.Id}", created);
            })
            .WithName("CreateUser")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new UpdateUserCommand(id, request.DisplayName, request.Version),
                    cancellationToken)))
            .WithName("UpdateUser")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/role", async (
                Guid id,
                SetUserRoleRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SetUserRoleCommand(id, request.RoleName, request.Version),
                    cancellationToken)))
            .WithName("SetUserRole")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // The replacement for the old ProfileService.remove(), which physically dropped the row.
        group.MapPut("/{id:guid}/active", async (
                Guid id,
                SetUserActiveRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SetUserActiveCommand(id, request.IsActive, request.Version),
                    cancellationToken)))
            .WithName("SetUserActive")
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapRoles(RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListRolesQuery(), cancellationToken)))
            .WithName("ListRoles")
            .Produces<IReadOnlyList<RoleResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetRoleQuery(id), cancellationToken)))
            .WithName("GetRole")
            .Produces<RoleResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateRoleRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(
                    new CreateRoleCommand(request.Name, request.Label, request.Permissions),
                    cancellationToken);
                return Results.Created($"/api/admin/roles/{created.Id}", created);
            })
            .WithName("CreateRole")
            .Produces<RoleResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Relabel, not rename: Role.Name is a foreign key target and immutable (design D7).
        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateRoleLabelRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new UpdateRoleLabelCommand(id, request.Label, request.Version),
                    cancellationToken)))
            .WithName("UpdateRoleLabel")
            .Produces<RoleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/permissions", async (
                Guid id,
                SetRolePermissionsRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SetRolePermissionsCommand(id, request.Permissions, request.Version),
                    cancellationToken)))
            .WithName("SetRolePermissions")
            .Produces<RoleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/active", async (
                Guid id,
                SetRoleActiveRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SetRoleActiveCommand(id, request.IsActive, request.Version),
                    cancellationToken)))
            .WithName("SetRoleActive")
            .Produces<RoleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
