using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Application.Features.Candidates;

/// <summary>
/// Translates between the catalog names the API contract speaks and the catalog entries
/// the schema stores.
/// </summary>
/// <remarks>
/// The wire contract deliberately carries names (<c>"Inglés"</c>, <c>"B2"</c>) rather than
/// catalog identifiers, so the five section components and the search filters keep binding
/// what they bind today. Resolution happens here, once per request, over families loaded
/// whole — they are short reference vocabularies, and a per-name round trip would turn one
/// collection write into dozens of queries.
///
/// Resolution includes inactive entries on purpose: a value an administrator retired stays
/// resolvable for the candidates that already reference it. What deactivation removes is
/// the value's availability for *new* selections, which the frontend enforces by listing
/// active values only — not its meaning for existing records.
///
/// An unresolvable name is refused. It is never created: a typo would otherwise pollute
/// the shared vocabulary permanently, and silently.
/// </remarks>
internal sealed class CandidateCatalogLookup(ICatalogRepository catalogs)
{
    private readonly Dictionary<string, IReadOnlyList<CatalogItem>> _families = [];

    public async Task<Guid> ResolveAsync(
        string family,
        string? name,
        string property,
        CancellationToken cancellationToken)
    {
        var items = await LoadAsync(family, cancellationToken);
        var normalized = CatalogName.Normalize(name);
        if (normalized.Length == 0)
        {
            throw CandidateGuards.UnknownCatalogValue(property);
        }
        var match = items.FirstOrDefault(item => item.NameNormalized == normalized)
            ?? throw CandidateGuards.UnknownCatalogValue(property);
        return match.Id;
    }

    public async Task<string> NameAsync(string family, Guid id, CancellationToken cancellationToken)
    {
        var items = await LoadAsync(family, cancellationToken);
        // A relation always references a real entry — the composite foreign key makes
        // anything else unstorable — so the fallback is unreachable rather than lenient.
        return items.FirstOrDefault(item => item.Id == id)?.NameEs ?? string.Empty;
    }

    /// <summary>Preloads the families a projection is about to read, in one pass.</summary>
    public async Task PreloadAsync(IEnumerable<string> families, CancellationToken cancellationToken)
    {
        foreach (var family in families.Distinct(StringComparer.Ordinal))
        {
            await LoadAsync(family, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<CatalogItem>> LoadAsync(string family, CancellationToken cancellationToken)
    {
        if (_families.TryGetValue(family, out var cached))
        {
            return cached;
        }
        var items = await catalogs.ListAsync(family, includeInactive: true, cancellationToken);
        _families[family] = items;
        return items;
    }
}
