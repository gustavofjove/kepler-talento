using KeplerTalento.Domain.Candidates;

namespace KeplerTalento.Application.Abstractions.Persistence;

public interface ICandidateReader
{
    Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken);
}
