using System.Security.Claims;

using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Identity;
using Microsoft.Extensions.Options;

namespace KeplerTalento.Web.Identity;

/// <summary>
/// What the API decided about the caller, once, for this request.
/// </summary>
/// <param name="UserId">The internal id of the stored user behind the caller.</param>
/// <param name="Permissions">
/// The effective permission set, read from the user's role at resolution time. Never read from
/// claims carried in the token: a permission revoked from a role has to bite on the caller's
/// very next request, which a claim cannot do until it expires (design D3).
/// </param>
public sealed record CallerIdentity(Guid UserId, string ExternalKey, IReadOnlyList<string> Permissions);

/// <summary>
/// The caller identity as the application sees it, built from a validated bearer token plus the
/// caller's row in the database.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately a holder rather than a resolver. <see cref="ICurrentActor"/> is
/// synchronous and resolution is a database read, so <see cref="IdentityResolutionMiddleware"/>
/// does the async work once per request and calls <see cref="Resolve"/>. An instance nobody
/// resolved answers "not authenticated" — the failure mode of a middleware that did not run is a
/// refusal, not an unguarded request.
/// </para>
/// <para>
/// There is no fallback to <see cref="DevelopmentActor"/> here or anywhere: the two are a branch
/// at composition time in <c>Program.cs</c> (design D11), which is what makes "an absent token
/// never resolves to the development actor" provable rather than asserted.
/// </para>
/// </remarks>
public sealed class TokenCurrentActor : ICurrentActor
{
    private CallerIdentity? identity;

    public void Resolve(CallerIdentity resolved) => identity = resolved;

    public string? ExternalKey => identity?.ExternalKey;

    public Guid? UserId => identity?.UserId;

    public bool IsAuthenticated => identity is not null;

    public bool HasPermission(string permission) =>
        identity is not null && identity.Permissions.Contains(permission, StringComparer.Ordinal);
}

/// <summary>
/// Turns a validated token into a <see cref="CallerIdentity"/>: reads the subject claim, loads
/// the user and its role, provisions on first sign-in, and refreshes the claims the provider
/// owns.
/// </summary>
public sealed class CallerIdentityResolver(
    IUserRepository users,
    IRoleRepository roles,
    IOptions<KeplerAuthenticationOptions> options)
{
    private readonly KeplerAuthenticationOptions settings = options.Value;

    /// <summary>
    /// Resolves the caller, or <see langword="null"/> when they have no standing: no subject
    /// claim, an unknown subject that provisioning is not allowed to create, or a user who has
    /// been deactivated.
    /// </summary>
    /// <remarks>
    /// A deactivated user resolves to <see langword="null"/> — unauthenticated — rather than to
    /// an authenticated caller with no permissions. The distinction is deliberate: a deactivated
    /// account should not be told it still exists, and 401 sends the browser to sign-in, which
    /// is the right outcome for them (design D3).
    /// </remarks>
    public async Task<CallerIdentity?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var subject = principal.FindFirst(settings.SubjectClaim)?.Value?.Trim();
        if (string.IsNullOrEmpty(subject))
        {
            // A token that validated but carries no subject identifies nobody. Refuse rather
            // than invent an identity for it.
            return null;
        }

        var user = await users.FindByExternalSubjectAsync(subject, cancellationToken)
            ?? await LinkOrProvisionAsync(principal, subject, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        await RefreshAsync(user, principal, cancellationToken);

        var role = await roles.FindByNameAsync(user.RoleName, cancellationToken);

        // An inactive role yields no permissions but leaves the caller authenticated: they are
        // still here, they just cannot currently do anything.
        var permissions = role is { IsActive: true } ? role.Permissions : [];

        return new CallerIdentity(user.Id, subject, permissions);
    }

    /// <summary>
    /// First sign-in. Either this subject's address already has a row — the bootstrap
    /// administrator seeded by email, which is linked now — or they are new and provisioned into
    /// the least-privileged role.
    /// </summary>
    private async Task<User?> LinkOrProvisionAsync(
        ClaimsPrincipal principal,
        string subject,
        CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(FirstClaim(principal, settings.EmailClaims));
        var displayName = FirstClaim(principal, settings.NameClaims) ?? email;
        var now = DateTimeOffset.UtcNow;

        if (email.Length > 0)
        {
            var seeded = await users.FindByEmailAsync(email, cancellationToken);
            if (seeded is not null)
            {
                if (seeded.ExternalSubject is null)
                {
                    seeded.LinkExternalSubject(subject, now);
                    seeded.MarkSignedIn(now);
                    users.ExpectVersion(seeded, seeded.Version);
                    await users.SaveAsync(cancellationToken);
                }
                // A row already linked to a different subject shares this address, which the
                // unique index says cannot happen. Returning it unlinked would be worse than
                // returning it: the caller is refused by the subject lookup on the next line.
                return seeded.ExternalSubject == subject ? seeded : null;
            }
        }

        if (!settings.ProvisionUnknownSubjects || email.Length == 0)
        {
            // Invitation-only, or a token with no address to key a new row by.
            return null;
        }

        var provisioned = new User(
            Guid.CreateVersion7(),
            subject,
            displayName!,
            email,
            settings.DefaultRoleName,
            now);
        provisioned.MarkSignedIn(now);
        users.Add(provisioned);

        var outcome = await users.SaveAsync(cancellationToken);
        if (outcome == UserSaveOutcome.NaturalKeyConflict)
        {
            // Two first requests from the same new person raced. The unique index on the
            // subject decided which one won; this one re-reads and uses the winner's row
            // rather than failing the caller (design D4).
            return await users.FindByExternalSubjectAsync(subject, cancellationToken);
        }

        return outcome == UserSaveOutcome.Saved ? provisioned : null;
    }

    /// <summary>
    /// Propagates a rename at the provider without an administrator touching anything. Writes
    /// only when something actually differs, which is almost never, so the common request stays
    /// one read.
    /// </summary>
    private async Task RefreshAsync(User user, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var displayName = FirstClaim(principal, settings.NameClaims) ?? string.Empty;
        var email = FirstClaim(principal, settings.EmailClaims) ?? string.Empty;

        if (!user.RefreshFromClaims(displayName, email, DateTimeOffset.UtcNow))
        {
            return;
        }

        users.ExpectVersion(user, user.Version);
        // A conflict here means an administrator wrote the same row concurrently. Their change
        // wins and the refresh is dropped; the next request retries it for free.
        await users.SaveAsync(cancellationToken);
    }

    private static string? FirstClaim(ClaimsPrincipal principal, IEnumerable<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return null;
    }
}

/// <summary>
/// Resolves the caller once per request, between authentication and endpoint dispatch.
/// </summary>
/// <remarks>
/// An unauthenticated request is left unresolved rather than refused here: refusing is the
/// endpoint's business, through <c>RequireAuthorization()</c> and the handler guards, so that a
/// health endpoint stays reachable and every business endpoint keeps failing closed on its own
/// terms.
/// </remarks>
public sealed class IdentityResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentActor actor,
        CallerIdentityResolver resolver)
    {
        // Only the token actor is resolvable. When the development actor is registered instead
        // (a composition-time branch), there is nothing to resolve and nothing to fall back to.
        if (actor is TokenCurrentActor tokenActor && context.User.Identity?.IsAuthenticated == true)
        {
            var identity = await resolver.ResolveAsync(context.User, context.RequestAborted);
            if (identity is not null)
            {
                tokenActor.Resolve(identity);
            }
            else
            {
                // Authentication proved that the token is genuine; identity resolution decides
                // whether its subject still has standing in this application. A deactivated or
                // otherwise unresolved subject is unauthenticated here, so the fallback policy
                // returns 401 before endpoint binding/validation rather than dispatching a
                // permission guard that would turn the refusal into 403.
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }

        await next(context);
    }
}
