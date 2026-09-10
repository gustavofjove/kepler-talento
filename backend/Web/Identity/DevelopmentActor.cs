using KeplerTalento.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace KeplerTalento.Web.Identity;

public sealed class DevelopmentActorOptions
{
    public const string SectionName = "DevelopmentActor";
    public bool Enabled { get; init; }
    public string ExternalKey { get; init; } = "synthetic-developer";
}

public sealed class DevelopmentActor(IOptions<DevelopmentActorOptions> options) : ICurrentActor
{
    private readonly DevelopmentActorOptions _options = options.Value;
    public string? ExternalKey => _options.Enabled ? _options.ExternalKey : null;
    public bool IsAuthenticated => _options.Enabled;
    private static readonly string[] GrantedPermissions =
    [
        Permissions.CandidatesRead,
        Permissions.CandidatesCreate,
        Permissions.CandidatesUpdate,
        Permissions.CandidatesDelete,
        Permissions.DocumentsUpload,
        Permissions.DocumentsDownload,
        Permissions.CatalogsRead,
        Permissions.CatalogsManage,
    ];

    public bool HasPermission(string permission) =>
        _options.Enabled && GrantedPermissions.Contains(permission, StringComparer.Ordinal);
}
