namespace KeplerTalento.Web.Identity;

/// <summary>
/// How the API decides who a caller is. Bound from the <c>Authentication</c> section and
/// validated at start-up, so a misconfiguration is a refusal to boot rather than a request that
/// authenticates against nothing.
/// </summary>
public sealed class KeplerAuthenticationOptions
{
    public const string SectionName = "Authentication";

    /// <summary>
    /// The OIDC authority — the tenant's discovery document base. Signing keys come from it and
    /// refresh automatically, which is what survives a key rotation without a restart.
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>The audience the token must carry: this API's application id or scope URI.</summary>
    public string? Audience { get; init; }

    /// <summary>
    /// The claim carrying the provider's stable subject. Entra's <c>oid</c> by default, never
    /// <c>sub</c>: <c>sub</c> is pairwise per application registration, so a <c>sub</c>-keyed
    /// user row breaks the day a second registration appears (design D2).
    /// </summary>
    public string SubjectClaim { get; init; } = "oid";

    /// <summary>The claim carrying the display name, tried in order against the token.</summary>
    public string[] NameClaims { get; init; } = ["name", "preferred_username"];

    /// <summary>The claim carrying the email address, tried in order against the token.</summary>
    public string[] EmailClaims { get; init; } = ["email", "preferred_username", "upn"];

    /// <summary>
    /// The role a user is provisioned into on first sign-in. Least-privileged by default: a new
    /// arrival can see the dashboard and nothing interesting (design D4).
    /// </summary>
    public string DefaultRoleName { get; init; } = "readonly";

    /// <summary>
    /// Whether an unknown but validly-authenticated subject is provisioned a user row. Turning
    /// it off makes the installation invitation-only; an unknown subject is then refused.
    /// </summary>
    public bool ProvisionUnknownSubjects { get; init; } = true;

    /// <summary>
    /// The administrator seeded by the <c>--migrate</c> entry point when the installation has
    /// none. An email address; the runbook uses it to recover a locked-out installation.
    /// </summary>
    public BootstrapAdministratorOptions? BootstrapAdministrator { get; init; }

    /// <summary>
    /// The development-only token issuer (design D8). Configuring it in Production is a start-up
    /// failure, not a warning.
    /// </summary>
    public DevelopmentIssuerOptions? DevelopmentIssuer { get; init; }
}

public sealed class BootstrapAdministratorOptions
{
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>
/// A real token flow without the corporate tenant: a signing key from configuration, an endpoint
/// that mints a token, and the same validation pipeline checking its signature, issuer, audience
/// and lifetime. Only the issuer differs — there is no code path that skips validation.
/// </summary>
public sealed class DevelopmentIssuerOptions
{
    public bool Enabled { get; init; }

    /// <summary>Symmetric signing key. At least 32 bytes, so HS256 has a key worth the name.</summary>
    public string? SigningKey { get; init; }

    public string Issuer { get; init; } = "https://kepler-talento.local/dev";

    public string Audience { get; init; } = "kepler-talento-api";

    /// <summary>Bounded so a development token left behind cannot be a long-lived credential.</summary>
    public int TokenLifetimeMinutes { get; init; } = 60;

    public const int MinimumSigningKeyBytes = 32;
}
