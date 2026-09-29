using System.Text.RegularExpressions;

namespace KeplerTalento.Infrastructure.CvExtraction;

public sealed record SpanishProvince(string Code, string Name);

public sealed record SpanishMunicipality(string Code, string Name)
{
    public string ProvinceCode => Code[..2];
}

/// <summary>
/// Spanish provinces and municipalities for CV location suggestions (KTL-32 design D6).
/// </summary>
/// <remarks>
/// Municipalities come from the embedded INE "Relación de municipios y códigos por comunidades
/// autónomas y provincias" (1 January 2026). INE writes a leading article after the name
/// ("Coruña, A") and joins co-official names with a slash ("Donostia/San Sebastián"); both are
/// expanded into every form a CV is likely to use. Reference data, not personal data.
/// </remarks>
public sealed partial class SpanishPlaces
{
    private const string ResourceName = "KeplerTalento.Infrastructure.CvExtraction.Resources.es-municipalities.csv";

    /// <summary>INE province codes with the names the application's data already uses.</summary>
    private static readonly (string Code, string Name, string[] Aliases)[] ProvinceTable =
    [
        ("01", "Álava", ["Araba", "Araba/Álava"]),
        ("02", "Albacete", []),
        ("03", "Alicante", ["Alacant"]),
        ("04", "Almería", []),
        ("05", "Ávila", []),
        ("06", "Badajoz", []),
        ("07", "Illes Balears", ["Baleares", "Islas Baleares", "Balears"]),
        ("08", "Barcelona", []),
        ("09", "Burgos", []),
        ("10", "Cáceres", []),
        ("11", "Cádiz", []),
        ("12", "Castellón", ["Castelló"]),
        ("13", "Ciudad Real", []),
        ("14", "Córdoba", []),
        ("15", "A Coruña", ["La Coruña"]),
        ("16", "Cuenca", []),
        ("17", "Girona", ["Gerona"]),
        ("18", "Granada", []),
        ("19", "Guadalajara", []),
        ("20", "Gipuzkoa", ["Guipúzcoa"]),
        ("21", "Huelva", []),
        ("22", "Huesca", []),
        ("23", "Jaén", []),
        ("24", "León", []),
        ("25", "Lleida", ["Lérida"]),
        ("26", "La Rioja", []),
        ("27", "Lugo", []),
        ("28", "Madrid", ["Comunidad de Madrid"]),
        ("29", "Málaga", []),
        ("30", "Murcia", ["Región de Murcia"]),
        ("31", "Navarra", ["Nafarroa"]),
        ("32", "Ourense", ["Orense"]),
        ("33", "Asturias", ["Principado de Asturias"]),
        ("34", "Palencia", []),
        ("35", "Las Palmas", []),
        ("36", "Pontevedra", []),
        ("37", "Salamanca", []),
        ("38", "Santa Cruz de Tenerife", ["Tenerife"]),
        ("39", "Cantabria", []),
        ("40", "Segovia", []),
        ("41", "Sevilla", []),
        ("42", "Soria", []),
        ("43", "Tarragona", []),
        ("44", "Teruel", []),
        ("45", "Toledo", []),
        ("46", "Valencia", ["València"]),
        ("47", "Valladolid", []),
        ("48", "Bizkaia", ["Vizcaya"]),
        ("49", "Zamora", []),
        ("50", "Zaragoza", []),
        ("51", "Ceuta", []),
        ("52", "Melilla", []),
    ];

    /// <summary>Everyday names that are not an INE form of the municipality.</summary>
    private static readonly (string Alias, string Code)[] MunicipalityAliases =
    [
        ("Vitoria", "01059"),
        ("Palma de Mallorca", "07040"),
        ("Castellón", "12040"),
        ("Castellón de la Plana", "12040"),
        ("La Coruña", "15030"),
        ("Orense", "32054"),
        ("Gerona", "17079"),
        ("Lérida", "25120"),
    ];

    private readonly Dictionary<string, SpanishProvince> _provincesByCode;
    private readonly Dictionary<string, SpanishProvince> _provincesByName;
    private readonly Dictionary<string, List<SpanishMunicipality>> _municipalitiesByName;

    public SpanishPlaces()
        : this(LoadEmbeddedMunicipalities())
    {
    }

    // Private on purpose: DI would pick a public IEnumerable<> constructor and satisfy it with an
    // empty collection, silently loading no municipalities.
    private SpanishPlaces(IEnumerable<(string Code, string Name)> municipalities)
    {
        _provincesByCode = ProvinceTable.ToDictionary(row => row.Code, row => new SpanishProvince(row.Code, row.Name));
        _provincesByName = new(StringComparer.Ordinal);
        foreach (var (code, name, aliases) in ProvinceTable)
        {
            foreach (var form in aliases.Prepend(name))
            {
                _provincesByName[TextNormalization.Normalize(form)] = _provincesByCode[code];
            }
        }

        _municipalitiesByName = new(StringComparer.Ordinal);
        var byCode = new Dictionary<string, (string Code, string Name)>(StringComparer.Ordinal);
        foreach (var row in municipalities)
        {
            byCode[row.Code] = row;
            foreach (var (display, form) in Forms(row.Name))
            {
                AddMunicipality(form, new SpanishMunicipality(row.Code, display));
            }
        }
        foreach (var (alias, code) in MunicipalityAliases)
        {
            if (byCode.ContainsKey(code))
            {
                AddMunicipality(alias, new SpanishMunicipality(code, alias));
            }
        }
        MunicipalityCount = byCode.Count;
    }

    public int MunicipalityCount { get; }

    /// <summary>The longest place name in words, bounding how far a matcher needs to look.</summary>
    public int MaximumWords { get; private set; }

    public SpanishProvince? ProvinceByCode(string code) => _provincesByCode.GetValueOrDefault(code);

    /// <summary>The province a Spanish postcode belongs to: its first two digits are the INE code.</summary>
    public SpanishProvince? ProvinceOfPostcode(string postcode) =>
        PostcodePattern().IsMatch(postcode) ? ProvinceByCode(postcode[..2]) : null;

    public SpanishProvince? FindProvince(string normalizedName) => _provincesByName.GetValueOrDefault(normalizedName);

    public IReadOnlyList<SpanishMunicipality> FindMunicipalities(string normalizedName) =>
        _municipalitiesByName.TryGetValue(normalizedName, out var found) ? found : [];

    [GeneratedRegex(@"^(0[1-9]|[1-4]\d|5[0-2])\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostcodePattern();

    [GeneratedRegex(@"^(?<base>.+?),\s*(?<article>[\p{L}]+'?)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingArticle();

    private void AddMunicipality(string form, SpanishMunicipality municipality)
    {
        var key = TextNormalization.Normalize(form);
        if (key.Length == 0)
        {
            return;
        }
        if (!_municipalitiesByName.TryGetValue(key, out var list))
        {
            list = [];
            _municipalitiesByName[key] = list;
        }
        if (!list.Any(existing => existing.Code == municipality.Code))
        {
            list.Add(municipality);
        }
        MaximumWords = Math.Max(MaximumWords, key.Count(character => character == ' ') + 1);
    }

    /// <summary>
    /// The display name and match forms of one INE entry: each co-official part, with its article
    /// moved to the front ("Coruña, A" → "A Coruña") and, for longer names, without it.
    /// </summary>
    private static IEnumerable<(string Display, string Form)> Forms(string ineName)
    {
        foreach (var rawPart in ineName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = TrailingArticle().Match(rawPart);
            if (!match.Success)
            {
                yield return (rawPart, rawPart);
                continue;
            }
            var article = match.Groups["article"].Value;
            var bare = match.Groups["base"].Value.Trim();
            var display = article.EndsWith('\'')
                ? $"{char.ToUpperInvariant(article[0])}{article[1..]}{bare}"
                : $"{char.ToUpperInvariant(article[0])}{article[1..]} {bare}";
            yield return (display, display);
            if (bare.Length >= 5)
            {
                yield return (display, bare);
            }
        }
    }

    private static IEnumerable<(string Code, string Name)> LoadEmbeddedMunicipalities()
    {
        using var stream = typeof(SpanishPlaces).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The municipality reference list is missing.");
        using var reader = new StreamReader(stream);
        var rows = new List<(string, string)>();
        _ = reader.ReadLine();
        while (reader.ReadLine() is { } line)
        {
            var separator = line.IndexOf(';');
            if (separator == 5)
            {
                rows.Add((line[..5], line[(separator + 1)..]));
            }
        }
        return rows;
    }
}
