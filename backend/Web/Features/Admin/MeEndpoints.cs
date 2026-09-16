using KeplerTalento.Application.Features.Admin;
using KeplerTalento.Application.Features.Admin.Me;
using MediatR;

namespace KeplerTalento.Web.Features.Admin;

/// <summary>
/// The caller's own profile. Authentication only — a caller always reads their own profile — so
/// a newly provisioned user can load the application that is about to tell them they may do
/// nothing.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/me").WithTags("Identity");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetMeQuery(), cancellationToken)))
            .WithName("GetMe")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
