using FastEndpoints;

namespace KeplerTalento.Web.Features.Platform;

public sealed record PlatformInfoResponse(string Service, string Version);

public sealed class GetPlatformInfoEndpoint : EndpointWithoutRequest<PlatformInfoResponse>
{
    public override void Configure()
    {
        Get("/api/platform");
        AllowAnonymous();
        Description(builder => builder.WithTags("Platform"));
    }

    public override Task HandleAsync(CancellationToken cancellationToken) =>
        Send.OkAsync(new("kepler-talento-api", "v1"), cancellationToken);
}
