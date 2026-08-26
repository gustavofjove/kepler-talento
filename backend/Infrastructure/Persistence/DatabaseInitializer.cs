using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
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

    /// <summary>
    /// Populates the default vocabulary of any family that holds no rows at all. Seeding
    /// per family rather than per value keeps the action idempotent without resurrecting
    /// values an administrator deliberately removed from a family.
    /// </summary>
    public static async Task SeedCatalogsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var populatedFamilies = await dbContext.CatalogItems
            .Select(item => item.Family)
            .Distinct()
            .ToListAsync(cancellationToken);
        var seededAtUtc = DateTimeOffset.UtcNow;
        var added = false;
        foreach (var (family, names) in CatalogSeedData.Families)
        {
            if (populatedFamilies.Contains(family, StringComparer.Ordinal))
            {
                continue;
            }
            for (var index = 0; index < names.Count; index++)
            {
                dbContext.CatalogItems.Add(new CatalogItem(
                    Guid.CreateVersion7(),
                    family,
                    CatalogName.DeriveCode(names[index]),
                    names[index],
                    nameEn: null,
                    sortOrder: index + 1,
                    createdAtUtc: seededAtUtc));
                added = true;
            }
        }
        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
