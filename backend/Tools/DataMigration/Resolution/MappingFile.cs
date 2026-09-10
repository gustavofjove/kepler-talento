using System.Text;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Validation;

namespace KeplerTalento.Tools.DataMigration.Resolution;

/// <summary>
/// The operator's decisions about source values that resolve to nothing on their own.
/// </summary>
/// <remarks>
/// This file is the "explicit decision" the specification requires. It maps a source value
/// onto an existing catalog code; it cannot create catalog vocabulary, because a mapping to
/// a code the catalog does not have is reported as a broken mapping rather than honoured.
/// It sits outside the export set: it is a decision about the data, not something Access
/// produced.
/// </remarks>
public static class MappingFile
{
    public static readonly IReadOnlyList<string> Columns = ["Family", "SourceValue", "TargetCode"];

    public static bool TryRead(
        string? path,
        out IReadOnlyDictionary<(string Family, string Value), string> mappings,
        out IReadOnlyList<StructuralProblem> problems)
    {
        var found = new List<StructuralProblem>();
        var parsed = new Dictionary<(string, string), string>();
        mappings = parsed;

        if (string.IsNullOrWhiteSpace(path))
        {
            problems = found;
            return true;
        }

        var name = Path.GetFileName(path);
        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(false, throwOnInvalidBytes: true));
        }
        catch (DecoderFallbackException)
        {
            problems = [new StructuralProblem(name, "the file is not valid UTF-8")];
            return false;
        }

        var document = CsvDocument.Parse(text);
        var missing = Columns.Except(document.Header, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            problems = [new StructuralProblem(name, $"missing required columns: {string.Join(", ", missing)}")];
            return false;
        }

        foreach (var row in document.Rows)
        {
            var family = row["Family"].Trim();
            var value = row["SourceValue"].Trim();
            var code = row["TargetCode"].Trim();

            if (!CatalogFamilies.IsKnown(family))
            {
                found.Add(new StructuralProblem(name, $"line {row.LineNumber}: unknown family '{family}'"));
                continue;
            }
            if (value.Length == 0 || code.Length == 0)
            {
                found.Add(new StructuralProblem(name, $"line {row.LineNumber}: SourceValue and TargetCode are both required"));
                continue;
            }
            if (!parsed.TryAdd((family, value), code))
            {
                // Two decisions about one value is an ambiguity only the operator can settle.
                found.Add(new StructuralProblem(
                    name,
                    $"line {row.LineNumber}: '{value}' is mapped more than once in family '{family}'"));
            }
        }

        problems = found;
        return found.Count == 0;
    }
}
