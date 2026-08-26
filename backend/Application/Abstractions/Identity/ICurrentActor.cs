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
    public const string CatalogsRead = "catalogs.read";
    public const string CatalogsManage = "catalogs.manage";
}
