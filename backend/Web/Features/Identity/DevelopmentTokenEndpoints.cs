using KeplerTalento.Web.Identity;

namespace KeplerTalento.Web.Features.Identity;

/// <summary>
/// The development token issuer's HTTP surface (design D8).
/// </summary>
/// <remarks>
/// Mapped only outside Production, from a <c>Program.cs</c> branch, and only when
/// <c>Authentication:DevelopmentIssuer:Enabled</c> is set. It is anonymous by necessity — it is
/// how a caller acquires the token everything else requires — which is exactly why it must not
/// exist in Production, and why start-up fails if it is configured there.
/// </remarks>
public static class DevelopmentTokenEndpoints
{
    public sealed record IssueDevelopmentTokenRequest(string Subject, string? DisplayName, string? Email);

    public sealed record IssueDevelopmentTokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Anonymous by necessity: this is how a caller acquires the token every other endpoint
        // requires. It is reachable only because Program.cs maps it outside Production alone.
        var group = endpoints.MapGroup("/api/dev").WithTags("Development").AllowAnonymous();

        group.MapPost("/token", (
                IssueDevelopmentTokenRequest request,
                DevelopmentTokenIssuer issuer,
                Microsoft.Extensions.Options.IOptions<KeplerAuthenticationOptions> options) =>
            {
                var subject = (request.Subject ?? string.Empty).Trim();
                if (subject.Length == 0)
                {
                    return Results.BadRequest(new { error = "A subject is required." });
                }

                // A development subject needs an address, because provisioning keys the new user
                // row by it. Deriving one keeps the endpoint usable with just a subject.
                var email = string.IsNullOrWhiteSpace(request.Email)
                    ? $"{subject}@kepler-talento.local"
                    : request.Email.Trim();
                var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? subject : request.DisplayName.Trim();

                var token = issuer.Issue(subject, displayName, email);
                var lifetime = options.Value.DevelopmentIssuer!.TokenLifetimeMinutes * 60;

                return Results.Ok(new IssueDevelopmentTokenResponse(token, "Bearer", lifetime));
            })
            .WithName("IssueDevelopmentToken")
            .Produces<IssueDevelopmentTokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
