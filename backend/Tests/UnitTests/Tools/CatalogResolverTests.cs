using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Tools.DataMigration.Resolution;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Tools;

public sealed class CatalogResolverTests
{
    private static readonly Guid EnglishId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SectorId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static CatalogResolver Resolver(
        IReadOnlyDictionary<(string Family, string Value), string>? mappings = null) =>
        new(
            [
                new CatalogResolver.CatalogEntry(EnglishId, CatalogFamilies.Language, "INGLES", "Inglés"),
                new CatalogResolver.CatalogEntry(SectorId, CatalogFamilies.Sector, "TECNOLOGIA", "Tecnología"),
            ],
            mappings);

    [Fact]
    public void An_exact_name_resolves()
    {
        var resolution = Resolver().Resolve(CatalogFamilies.Language, "Inglés");

        Assert.Equal(EnglishId, resolution.CatalogItemId);
        Assert.Equal(ResolutionStep.ExactName, resolution.Step);
    }

    [Fact]
    public void Surrounding_whitespace_does_not_prevent_an_exact_match()
    {
        Assert.Equal(EnglishId, Resolver().Resolve(CatalogFamilies.Language, "  Inglés  ").CatalogItemId);
    }

    [Theory]
    [InlineData("ingles")]
    [InlineData("INGLES")]
    [InlineData("Ingles")]
    [InlineData("inglés")]
    public void Casing_and_accents_resolve_through_the_catalogs_own_normalization(string value)
    {
        var resolution = Resolver().Resolve(CatalogFamilies.Language, value);

        Assert.Equal(EnglishId, resolution.CatalogItemId);
        Assert.Equal(ResolutionStep.NormalizedCode, resolution.Step);
    }

    [Fact]
    public void An_operator_mapping_resolves_a_value_nothing_else_can()
    {
        var resolver = Resolver(new Dictionary<(string, string), string>
        {
            [(CatalogFamilies.Language, "Ingl.")] = "INGLES",
        });

        var resolution = resolver.Resolve(CatalogFamilies.Language, "Ingl.");

        Assert.Equal(EnglishId, resolution.CatalogItemId);
        Assert.Equal(ResolutionStep.OperatorMapping, resolution.Step);
        Assert.Empty(resolver.UnresolvedValues);
    }

    [Fact]
    public void An_unresolvable_value_is_reported_and_never_created()
    {
        var resolver = Resolver();

        var resolution = resolver.Resolve(CatalogFamilies.Language, "Klingon");

        Assert.False(resolution.Resolved);
        var unresolved = Assert.Single(resolver.UnresolvedValues);
        Assert.Equal(CatalogFamilies.Language, unresolved.Family);
        Assert.Equal("Klingon", unresolved.Value);
        Assert.Equal(1, unresolved.Occurrences);
    }

    [Fact]
    public void Occurrences_are_counted_so_the_operator_can_see_how_much_a_decision_is_worth()
    {
        var resolver = Resolver();
        resolver.Resolve(CatalogFamilies.Language, "Klingon");
        resolver.Resolve(CatalogFamilies.Language, "Klingon");
        resolver.Resolve(CatalogFamilies.Language, "Sindarin");

        Assert.Equal(2, resolver.UnresolvedValues.Single(value => value.Value == "Klingon").Occurrences);
        Assert.Equal(1, resolver.UnresolvedValues.Single(value => value.Value == "Sindarin").Occurrences);
    }

    [Fact]
    public void A_mapping_pointing_at_a_code_the_catalog_does_not_have_is_reported_as_broken()
    {
        // Distinct from unresolved: the operator already made a decision, and it is wrong.
        // Merging the two would have them fix the same value twice.
        var resolver = Resolver(new Dictionary<(string, string), string>
        {
            [(CatalogFamilies.Language, "Klingon")] = "NO_EXISTE",
        });

        var resolution = resolver.Resolve(CatalogFamilies.Language, "Klingon");

        Assert.False(resolution.Resolved);
        Assert.Empty(resolver.UnresolvedValues);
        var broken = Assert.Single(resolver.BrokenMappings);
        Assert.Equal("Klingon", broken.Value);
    }

    [Fact]
    public void A_value_never_resolves_across_families()
    {
        var resolver = Resolver();

        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Tecnología").Resolved);
        Assert.False(resolver.Resolve(CatalogFamilies.Sector, "Inglés").Resolved);
    }

    [Fact]
    public void A_mapping_only_applies_within_its_own_family()
    {
        var resolver = Resolver(new Dictionary<(string, string), string>
        {
            [(CatalogFamilies.Language, "Ingl.")] = "INGLES",
        });

        Assert.False(resolver.Resolve(CatalogFamilies.Sector, "Ingl.").Resolved);
    }

    [Fact]
    public void An_absent_value_resolves_to_nothing_without_being_reported()
    {
        var resolver = Resolver();

        Assert.False(resolver.Resolve(CatalogFamilies.Language, "   ").Resolved);
        Assert.False(resolver.Resolve(CatalogFamilies.Language, null).Resolved);
        // An empty field is the row's problem to report, not a vocabulary decision.
        Assert.Empty(resolver.UnresolvedValues);
    }

    [Fact]
    public void There_is_no_fuzzy_matching()
    {
        var resolver = Resolver();

        // Each is close enough that a similarity match would accept it, which is the point:
        // silently picking the wrong value is worse than a rejection an operator resolves.
        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Inglesa").Resolved);
        Assert.False(resolver.Resolve(CatalogFamilies.Language, "Ingl").Resolved);
        Assert.False(resolver.Resolve(CatalogFamilies.Sector, "Tecnologias").Resolved);
    }

    [Fact]
    public void Unresolved_values_are_ordered_so_two_runs_produce_the_same_report()
    {
        var resolver = Resolver();
        resolver.Resolve(CatalogFamilies.Sector, "Zeta");
        resolver.Resolve(CatalogFamilies.Language, "Beta");
        resolver.Resolve(CatalogFamilies.Language, "Alfa");

        Assert.Equal(
            [("language", "Alfa"), ("language", "Beta"), ("sector", "Zeta")],
            resolver.UnresolvedValues.Select(value => (value.Family, value.Value)));
    }
}
