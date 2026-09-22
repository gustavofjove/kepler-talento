using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Positions;

namespace KeplerTalento.Domain.Auditing;

/// <summary>
/// The closed catalogue of audit event types, assembled from the per-feature classes.
/// </summary>
/// <remarks>
/// A type that is not listed here cannot be written, which is what makes filtering by event type
/// exhaustive rather than a substring guess (KTL-19 design D6). A new audited operation registers
/// its type here as well as in its feature class.
/// </remarks>
public static class AuditEventTypes
{
    public static readonly IReadOnlyList<string> All =
    [
        CandidateAuditEvents.Created,
        CandidateAuditEvents.Updated,
        CandidateAuditEvents.StatusChanged,
        CandidateAuditEvents.Removed,
        CandidateAuditEvents.Restored,
        CandidateAuditEvents.RelationsChanged,
        CandidateAuditEvents.TagsChanged,
        CandidateAuditEvents.NoteAdded,
        CandidateAuditEvents.NoteUpdated,
        CandidateAuditEvents.NoteRetired,
        CandidateAuditEvents.DocumentsChanged,
        CandidateAuditEvents.Read,
        CatalogAuditEvents.Created,
        CatalogAuditEvents.Updated,
        CatalogAuditEvents.Reordered,
        CatalogAuditEvents.ActivationChanged,
        DocumentAuditEvents.UploadAccepted,
        DocumentAuditEvents.Scanned,
        DocumentAuditEvents.Downloaded,
        DocumentAuditEvents.PrimaryChanged,
        DocumentAuditEvents.Removed,
        DocumentAuditEvents.Reconciliation,
        PositionAuditEvents.Created,
        PositionAuditEvents.Updated,
        PositionAuditEvents.StatusChanged,
    ];

    private static readonly HashSet<string> Known = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string? eventType) => eventType is not null && Known.Contains(eventType);
}
