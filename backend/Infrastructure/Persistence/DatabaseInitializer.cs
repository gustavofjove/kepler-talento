using KeplerTalento.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static readonly Guid ReferenceCandidateId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public static async Task MigrateAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public static async Task SeedSyntheticReferenceAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Candidates.AnyAsync(candidate => candidate.Id == ReferenceCandidateId, cancellationToken))
        {
            return;
        }
        dbContext.Candidates.Add(new Candidate(
            ReferenceCandidateId,
            "Candidata",
            "Sintética",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
