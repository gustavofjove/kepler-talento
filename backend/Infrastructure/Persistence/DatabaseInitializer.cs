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
        var seededAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var candidate = new Candidate(ReferenceCandidateId, "Candidata", "Sintética", seededAtUtc);
        candidate.SetDetails(
            phone: "+34 600 000 000",
            email: "candidata.sintetica@example.invalid",
            location: "Ciudad Sintética",
            province: "Provincia Sintética",
            country: "España",
            availability: "Inmediata",
            status: CandidateStatuses.New,
            source: "Semilla",
            notes: string.Empty,
            updatedAtUtc: seededAtUtc);
        // Consent metadata is explicit even for the synthetic row: nothing in the system
        // should model a candidate whose consent state was never established.
        candidate.SetConsent(
            receivedAt: new DateOnly(2026, 1, 1),
            consentAt: new DateOnly(2026, 1, 1),
            reviewDueAt: new DateOnly(2028, 1, 1),
            updatedAtUtc: seededAtUtc);
        dbContext.Candidates.Add(candidate);
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
