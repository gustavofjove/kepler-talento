using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Identity;
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
                var value = names[index];
                dbContext.CatalogItems.Add(new CatalogItem(
                    Guid.CreateVersion7(),
                    family,
                    value.ResolveCode(),
                    value.NameEs,
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

    /// <summary>The role the bootstrap administrator is seeded into. Seeded by the migration.</summary>
    private const string BootstrapRoleName = "rrhh_admin";

    /// <summary>
    /// Ensures the installation has someone who can administer it, and refuses to finish quietly
    /// when it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the lockout guard the KTL-16 design names as a risk. It runs from the
    /// <c>--migrate</c> entry point, after the migration has created the tables and seeded the
    /// system roles. The administrator cannot be seeded by the migration itself because the
    /// address comes from <c>Authentication:BootstrapAdministrator</c>, which a migration cannot
    /// read.
    /// </para>
    /// <para>
    /// The row is seeded with no external subject: nobody knows the provider's subject for an
    /// address until that person signs in, and <see cref="User.LinkExternalSubject"/> attaches it
    /// then. The partial unique index on <c>ExternalSubject</c> is what lets the unlinked row
    /// exist.
    /// </para>
    /// <para>
    /// It is idempotent in the way that matters: an installation that already has an active
    /// administrator is left exactly as it is, including one whose address differs from the
    /// configured one. Re-seeding would otherwise resurrect an administrator that an operator
    /// deliberately replaced.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No active administrator exists and no bootstrap address was configured — deploying on
    /// would leave nobody able to administer the installation.
    /// </exception>
    public static async Task SeedBootstrapAdministratorAsync(
        ApplicationDbContext dbContext,
        string? email,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (await HasActiveAdministratorAsync(dbContext, cancellationToken))
        {
            return;
        }

        var normalizedEmail = User.NormalizeEmail(email);
        if (normalizedEmail.Length == 0)
        {
            throw new InvalidOperationException(
                "This installation has no active administrator and no Authentication:BootstrapAdministrator "
                + "was configured. Set it and run --migrate again, or restore one against the database as "
                + "docs/ktl-16/runbook.md describes.");
        }

        var existing = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        var seededAtUtc = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            dbContext.Users.Add(new User(
                Guid.CreateVersion7(),
                // Seeded unlinked: the provider's subject is unknown until this person signs in.
                externalSubject: null,
                displayName: string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName,
                email: normalizedEmail,
                roleName: BootstrapRoleName,
                createdAtUtc: seededAtUtc));
        }
        else
        {
            // The configured administrator exists but is deactivated or demoted — which is
            // exactly the locked-out case this is here to recover.
            existing.AssignRole(BootstrapRoleName, seededAtUtc);
            existing.Reactivate(seededAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// An administrator is an active user whose active role grants <c>users.manage</c> — defined
    /// by permission rather than by role name, so the rule survives an installation that renames
    /// or reshapes its roles (design D6).
    /// </summary>
    private static async Task<bool> HasActiveAdministratorAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // ADM_Roles has as many rows as the installation has roles, and the permission set is a
        // jsonb document behind a value converter with no SQL translation, so the filter happens
        // here rather than in the query.
        var administratorRoles = (await dbContext.Roles
                .AsNoTracking()
                .Where(role => role.IsActive)
                .ToListAsync(cancellationToken))
            .Where(role => role.Grants("users.manage"))
            .Select(role => role.Name)
            .ToList();

        if (administratorRoles.Count == 0)
        {
            return false;
        }

        return await dbContext.Users
            .AnyAsync(
                user => user.IsActive && administratorRoles.Contains(user.RoleName),
                cancellationToken);
    }
}
