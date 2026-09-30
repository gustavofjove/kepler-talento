using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Encryption;

/// <summary>
/// KTL-33: a query that filters or orders on ciphertext is refused before it runs, instead of
/// silently returning the wrong rows. Compiled with <c>ToQueryString</c>; no database is needed.
/// </summary>
public sealed class EncryptedColumnQueryGuardTests
{
    [Fact]
    public void Equality_on_an_encrypted_property_is_refused_and_named()
    {
        using var dbContext = NewContext();

        var failure = Assert.Throws<EncryptedColumnQueryException>(() =>
            dbContext.Candidates.Where(candidate => candidate.Email == "ana@example.test").ToQueryString());

        Assert.Equal("CND_Candidates.Email", failure.Column);
        Assert.DoesNotContain("ana@example.test", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordering_on_an_encrypted_property_is_refused()
    {
        using var dbContext = NewContext();

        Assert.Throws<EncryptedColumnQueryException>(() =>
            dbContext.Candidates.OrderBy(candidate => candidate.LastName).ToQueryString());
    }

    [Fact]
    public void Pattern_matching_on_an_encrypted_relation_property_is_refused()
    {
        using var dbContext = NewContext();

        Assert.Throws<EncryptedColumnQueryException>(() =>
            dbContext.CandidateExperience.Where(experience => EF.Functions.ILike(experience.Company, "%acme%")).ToQueryString());
        Assert.Throws<EncryptedColumnQueryException>(() =>
            dbContext.CandidateSkills.Where(skill => skill.Notes != null && skill.Notes.Contains("x")).ToQueryString());
    }

    [Fact]
    public void An_encrypted_property_inside_a_correlated_subquery_is_refused()
    {
        using var dbContext = NewContext();

        Assert.Throws<EncryptedColumnQueryException>(() =>
            dbContext.Candidates
                .Where(candidate => dbContext.Documents.Any(document =>
                    document.CandidateId == candidate.Id && document.OriginalFileName == "cv.pdf"))
                .ToQueryString());
    }

    [Fact]
    public void Reading_encrypted_properties_and_filtering_on_clear_ones_is_allowed()
    {
        using var dbContext = NewContext();

        var sql = dbContext.Candidates
            .Where(candidate => candidate.IsActive && EF.Property<string>(candidate, CandidateConfiguration.EmailHash) == "b1.x")
            .OrderBy(candidate => candidate.UpdatedAtUtc)
            .Select(candidate => new { candidate.FirstName, candidate.Email })
            .ToQueryString();

        Assert.Contains("\"EmailHash\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_properties_that_share_a_name_on_other_entities_are_allowed()
    {
        using var dbContext = NewContext();

        // Users are not candidate data; their e-mail stays in clear and is looked up by equality.
        var sql = dbContext.Users.Where(user => user.Email == "admin@example.test").ToQueryString();

        Assert.Contains("\"Email\"", sql, StringComparison.Ordinal);
    }

    private static ApplicationDbContext NewContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);
}
