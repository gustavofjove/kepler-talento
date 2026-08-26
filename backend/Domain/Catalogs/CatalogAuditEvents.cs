namespace KeplerTalento.Domain.Catalogs;

/// <summary>
/// Audit event types recorded for catalog changes. Reads are not audited: catalog values
/// are business reference vocabulary, not personal data.
/// </summary>
public static class CatalogAuditEvents
{
    public const string Created = "catalog.created";
    public const string Updated = "catalog.updated";
    public const string Reordered = "catalog.reordered";
    public const string ActivationChanged = "catalog.activation_changed";
}
