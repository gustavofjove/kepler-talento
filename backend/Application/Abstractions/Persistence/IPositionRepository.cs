using KeplerTalento.Domain.Positions;

namespace KeplerTalento.Application.Abstractions.Persistence;

public enum PositionSaveOutcome { Saved, TitleConflict, ConcurrencyConflict, ConstraintViolation }

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
    uint Version);

public sealed record PositionPage(IReadOnlyList<PositionSummary> Items, int Page, int PageSize, int TotalCount);

public interface IPositionRepository
{
    Task<PositionPage> ListAsync(PositionListOptions options, CancellationToken cancellationToken);
    Task<Position?> FindAsync(Guid id, CancellationToken cancellationToken);
    void Add(Position position);
    void ExpectVersion(Position position, uint version);
    Task<PositionSaveOutcome> SaveAsync(string auditEventType, CancellationToken cancellationToken);
}
