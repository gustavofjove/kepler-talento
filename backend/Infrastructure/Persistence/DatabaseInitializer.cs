using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task MigrateAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // There is deliberately no candidate seed. An empty database means an empty candidate
    // list, and that is the truth; a fabricated row in a table of personal data is a
    // liability rather than a convenience. Real candidate data arrives through KTL-7's
    // migration.

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
