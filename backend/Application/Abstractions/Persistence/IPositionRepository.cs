using KeplerTalento.Domain.Positions;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum PositionSaveOutcome { Saved, TitleConflict, ConcurrencyConflict, ConstraintViolation, AlreadyLinked }

/// <summary>
/// One link as a position lists it (KTL-30): the same contact columns candidate search shows, since
/// reading it already requires <c>candidates.read</c>; never documents, notes or other fields.
/// </summary>
public sealed record PositionCandidateItem(
    Guid CandidateId,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    bool HasPrimaryCv,
    bool CandidateIsActive,
    string Stage,
    DateTimeOffset AddedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version);

/// <summary>One link as a candidate lists it: never the description or requirements (KTL-30).</summary>
public sealed record CandidatePositionItem(
    Guid PositionId,
    string Title,
    string PositionStatus,
    string Stage,
    DateTimeOffset AddedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    uint Version);

public sealed record PositionListOptions(
    string Status,
    string Text,
    int Page,
    int PageSize,
    string SortField,
    string SortDirection);

public sealed record PositionSummary(
    Guid Id,
    string Title,
    string Location,
    string Status,
    DateTimeOffset UpdatedAtUtc,
    uint Version,
    int CandidateCount);

public sealed record PositionPage(IReadOnlyList<PositionSummary> Items, int Page, int PageSize, int TotalCount);

public interface IPositionRepository
{
    Task<PositionPage> ListAsync(PositionListOptions options, CancellationToken cancellationToken);
    Task<Position?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(Position position);
    void ExpectVersion(Position position, uint version);
    Task<PositionSaveOutcome> SaveAsync(string auditEventType, CancellationToken cancellationToken);

    // KTL-30 position candidate links.
    Task<IReadOnlyList<PositionCandidateItem>> ListCandidatesAsync(Guid positionId, CancellationToken cancellationToken);
    Task<PositionCandidateItem?> FindCandidateItemAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CandidatePositionItem>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    /// <summary>Whether the position is open, or <see langword="null"/> when it does not exist.</summary>
    Task<bool?> IsPositionOpenAsync(Guid positionId, CancellationToken cancellationToken);

    /// <summary>Whether the candidate is active, or <see langword="null"/> when it does not exist.</summary>
    Task<bool?> IsCandidateActiveAsync(Guid candidateId, CancellationToken cancellationToken);

    Task<PositionCandidate?> FindLinkAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken);
    Task<int> CountLinksAsync(Guid positionId, CancellationToken cancellationToken);
    void AddLink(PositionCandidate link);
    void RemoveLink(PositionCandidate link);
    void ExpectLinkVersion(PositionCandidate link, uint version);

    /// <summary>Saves the staged link change with one audit event whose subject names both ids.</summary>
    Task<PositionSaveOutcome> SaveLinkAsync(string auditEventType, PositionCandidate link, CancellationToken cancellationToken);
}
