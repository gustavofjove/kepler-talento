using KeplerTalento.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;

// This file's own namespace ends in Identity, so an unqualified `Permissions` binds to
// KeplerTalento.Web.Identity rather than to the catalogue. The alias names it once.
using Catalogue = KeplerTalento.Application.Abstractions.Identity.Permissions;

namespace KeplerTalento.Web.Identity;

public sealed class DevelopmentActorOptions
{
    public const string SectionName = "DevelopmentActor";
    public bool Enabled { get; init; }
    public string ExternalKey { get; init; } = "synthetic-developer";

    /// <summary>
    /// The permission set this actor grants. Configurable since KTL-16 (design D11) so that a
    /// test can produce a caller who is authenticated but <em>not</em> authorized — a state the
    /// suite could not express while the list was compiled in, and the one that most
    /// authorization bugs hide in.
    /// </summary>
    /// <remarks>
    /// Defaults to the nine values that used to be hard-coded, so the tests that flip
    /// <c>DevelopmentActor:Enabled</c> and expect a fully capable caller keep passing untouched.
    /// </remarks>
    public string[] Permissions { get; init; } = DefaultPermissions;

    /// <summary>
    /// The internal user id this actor stands in for. Unset by default, and then every audited
    /// operation refuses it (KTL-19 design D1): a synthetic caller does not get to write audit rows
    /// in nobody's name. A test that performs audited operations sets it to a user it seeded.
    /// </summary>
    public Guid? UserId { get; init; }

    internal static readonly string[] DefaultPermissions =
    [
        Catalogue.CandidatesRead,
        Catalogue.CandidatesCreate,
        Catalogue.CandidatesUpdate,
        Catalogue.CandidatesDelete,
        Catalogue.DocumentsUpload,
        Catalogue.DocumentsDownload,
        Catalogue.CatalogsRead,
        Catalogue.CatalogsManage,
        Catalogue.PresetsManage,
    ];
}

/// <summary>
/// A stand-in caller for local development and tests. It is never a fallback for a missing or
/// invalid token: <c>Program.cs</c> registers either this or <see cref="TokenCurrentActor"/>,
/// as a branch at composition time (design D11), and start-up fails if it is enabled in
/// Production.
/// </summary>
public sealed class DevelopmentActor(IOptions<DevelopmentActorOptions> options) : ICurrentActor
{
    private readonly DevelopmentActorOptions _options = options.Value;
    public string? ExternalKey => _options.Enabled ? _options.ExternalKey : null;
    public bool IsAuthenticated => _options.Enabled;

    /// <summary>
    /// Null unless <see cref="DevelopmentActorOptions.UserId"/> names one. Without it, anything that
    /// needs a real user id - the audit actor KTL-19 records, for one - is refused rather than
    /// satisfied by configuration pretending to be a person.
    /// </summary>
    public Guid? UserId => _options.Enabled ? _options.UserId : null;

    public bool HasPermission(string permission) =>
        _options.Enabled && _options.Permissions.Contains(permission, StringComparer.Ordinal);
}
