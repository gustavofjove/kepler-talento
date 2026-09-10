namespace KeplerTalento.Application.Abstractions.Identity;

public interface ICurrentActor
{
    string? ExternalKey { get; }
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
    public const string CatalogsRead = "catalogs.read";
    public const string CatalogsManage = "catalogs.manage";
}
