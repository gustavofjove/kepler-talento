using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class CandidateReader(ApplicationDbContext dbContext) : ICandidateReader
{
    public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Candidates.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
}
