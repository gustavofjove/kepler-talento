namespace KeplerTalento.Application.Abstractions.Identity;

public interface ICurrentActor
{
    /// <summary>
    /// An opaque key identifying the caller for correlation. Never the provider's subject claim
    /// in a response or a log — see <see cref="UserId"/> for what identifies them to the domain.
    /// </summary>
    string? ExternalKey { get; }

    /// <summary>
    /// The caller's internal user id, or <see langword="null"/> when there is no stored user
    /// behind the caller — a development actor, or a token whose subject was not provisioned.
    /// This is the id responses carry and the one KTL-19 will record on audit rows.
    /// </summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    bool HasPermission(string permission);
}

public static class Permissions
{
    public const string CandidatesRead = "candidates.read";
    public const string CandidatesCreate = "candidates.create";
    public const string CandidatesUpdate = "candidates.update";

    /// <summary>
    /// Governs logical removal <em>and</em> restoration: restoring a removed candidate is
    /// as consequential as removing one, so it is not a lesser capability.
    /// </summary>
    public const string CandidatesDelete = "candidates.delete";
    public const string DocumentsUpload = "documents.upload";
    public const string DocumentsDownload = "documents.download";
    public const string CatalogsRead = "catalogs.read";
    public const string CatalogsManage = "catalogs.manage";

    /// <summary>
    /// Creating, changing and deleting the shared saved-search library. Deliberately does not
    /// imply <see cref="CandidatesRead"/>, which listing and applying presets require.
    /// </summary>
    public const string PresetsManage = "presets.manage";

    /// <summary>
    /// Bulk candidate import. Since KTL-17 it guards every <c>/api/import</c> endpoint — upload,
    /// validate, commit, batch history and row reports. It does not imply
    /// <see cref="CandidatesCreate"/> or <see cref="CandidatesRead"/>, nor is it implied by them.
    /// </summary>
    public const string CandidatesImport = "candidates.import";

    /// <summary>
    /// Exporting candidate data. Its only enforcement point is still the browser — the CSV export on the advanced search page. It is kept in the
    /// catalogue because removing it would ungate candidate personal data.
    /// </summary>
    public const string CandidatesExport = "candidates.export";

    /// <summary>Governs creating, changing and deactivating users, and assigning their roles.</summary>
    public const string UsersManage = "users.manage";

    /// <summary>Governs creating, changing and deactivating roles, and setting their permissions.</summary>
    public const string RolesManage = "roles.manage";

    /// <summary>
    /// Reading the audit trail (KTL-19). Granted narrowly: implied by no candidate, catalog or
    /// document permission, and seeded only on <c>system_admin</c>.
    /// </summary>
    public const string AuditRead = "audit.read";

    /// <summary>
    /// The whole permission catalogue. A role may only hold values from this array, and
    /// the frontend's <c>ALL_PERMISSIONS</c> is asserted to match it.
    /// </summary>
    public static readonly string[] All =
    [
        CandidatesRead,
        CandidatesCreate,
        CandidatesUpdate,
        CandidatesDelete,
        CandidatesImport,
        CandidatesExport,
        DocumentsUpload,
        DocumentsDownload,
        CatalogsRead,
        CatalogsManage,
        PresetsManage,
        UsersManage,
        RolesManage,
        AuditRead,
    ];
}
