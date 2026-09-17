namespace KeplerTalento.Domain.Documents;

/// <summary>
/// Audit event types recorded for a candidate's documents. The subject names the candidate and
/// the document by identifier; no event carries a filename, content or storage key.
/// </summary>
public static class DocumentAuditEvents
{
    public const string UploadAccepted = "document.upload.accepted";
    public const string Scanned = "document.scan";
    public const string Downloaded = "document.downloaded";
    public const string PrimaryChanged = "document.primary.changed";
    public const string Removed = "document.removed";
    public const string Reconciliation = "document.reconciliation";
}
