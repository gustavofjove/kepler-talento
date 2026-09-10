using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Persistence;

public sealed class DatabaseNamingTests
{
    private static readonly string[] ApprovedPrefixes = ["CND_", "CAT_", "OPS_", "AUD_", "ADM_"];

    [Fact]
    public void Every_application_table_has_an_explicit_approved_uppercase_prefix()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model;Password=model")
            .Options;
        using var dbContext = new ApplicationDbContext(options);

        var mappedTables = dbContext.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.Equal(11, mappedTables.Length);
        Assert.All(mappedTables, name =>
        {
            Assert.Contains(ApprovedPrefixes, prefix => name.StartsWith(prefix, StringComparison.Ordinal));
            Assert.Equal(name, name.ToUpperInvariant().Split('_')[0] + "_" + name[(name.IndexOf('_') + 1)..]);
        });
        Assert.Contains("CND_Candidates", mappedTables);
        Assert.Contains("CND_CandidateLanguages", mappedTables);
        Assert.Contains("CND_CandidatePrograms", mappedTables);
        Assert.Contains("CND_CandidateEducation", mappedTables);
        Assert.Contains("CND_CandidateExperience", mappedTables);
        Assert.Contains("CND_CandidateSkills", mappedTables);
        Assert.Contains("CND_Documents", mappedTables);
        Assert.Contains("OPS_Operations", mappedTables);
        Assert.Contains("OPS_MigrationRuns", mappedTables);
        Assert.Contains("AUD_Events", mappedTables);
        Assert.Contains("CAT_CatalogItems", mappedTables);
    }

    [Fact]
    public void Candidate_entities_keep_clean_application_names_without_the_physical_prefix()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model;Password=model")
            .Options;
        using var dbContext = new ApplicationDbContext(options);

        var candidateEntities = dbContext.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName()?.StartsWith("CND_", StringComparison.Ordinal) == true)
            .Select(entity => entity.ClrType.Name)
            .ToArray();

        Assert.All(candidateEntities, name =>
            Assert.All(ApprovedPrefixes, prefix =>
                Assert.DoesNotContain(prefix, name, StringComparison.Ordinal)));
        Assert.Contains("Candidate", candidateEntities);
        Assert.Contains("CandidateLanguage", candidateEntities);
    }
}
