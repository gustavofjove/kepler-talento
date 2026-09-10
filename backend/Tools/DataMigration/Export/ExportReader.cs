using System.Text;
using KeplerTalento.Tools.DataMigration.Validation;

namespace KeplerTalento.Tools.DataMigration.Export;

public sealed class ExportSet
{
    private readonly IReadOnlyDictionary<string, CsvDocument> _documents;

    internal ExportSet(string root, IReadOnlyDictionary<string, CsvDocument> documents)
    {
        Root = root;
        _documents = documents;
    }

    public string Root { get; }

    public CsvDocument this[string file] => _documents[file];

    public string FilesDirectory => Path.Combine(Root, ExportContract.FilesDirectory);

    public int TotalRows => _documents.Values.Sum(document => document.Rows.Count);

    public int RowCount(string file) => _documents[file].Rows.Count;
}

/// <summary>
/// Reads an export set and reports everything wrong with its shape before any business data
/// is looked at, let alone written.
/// </summary>
public sealed class ExportReader
{
    /// <summary>
    /// Reads every file, collecting structural problems rather than stopping at the first —
    /// an operator fixing an export wants the whole list in one pass.
    /// </summary>
    public bool TryRead(
        string root,
        out ExportSet exportSet,
        out IReadOnlyList<StructuralProblem> problems)
    {
        exportSet = null!;
        var found = new List<StructuralProblem>();
        var documents = new Dictionary<string, CsvDocument>(StringComparer.Ordinal);

        foreach (var file in ExportContract.Files)
        {
            var path = Path.Combine(root, file);
            if (!File.Exists(path))
            {
                found.Add(new StructuralProblem(file, "the export set does not contain this file"));
                continue;
            }

            string text;
            try
            {
                // Throw on invalid UTF-8 rather than substituting replacement characters:
                // silently mangling accents is exactly the failure the contract guards.
                text = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            }
            catch (DecoderFallbackException)
            {
                found.Add(new StructuralProblem(file, "the file is not valid UTF-8"));
                continue;
            }

            var document = CsvDocument.Parse(text);
            if (document.Header.Count == 0)
            {
                found.Add(new StructuralProblem(file, "the file has no header row"));
                continue;
            }

            var expected = ExportContract.Columns[file];
            var missing = expected.Except(document.Header, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                found.Add(new StructuralProblem(file, $"missing required columns: {string.Join(", ", missing)}"));
            }

            // A renamed column arrives as one missing and one unexpected; reporting both is
            // what makes the rename obvious instead of mysterious.
            var unexpected = document.Header.Except(expected, StringComparer.Ordinal).ToList();
            if (unexpected.Count > 0)
            {
                found.Add(new StructuralProblem(file, $"unexpected columns: {string.Join(", ", unexpected)}"));
            }

            var duplicates = document.Header
                .GroupBy(column => column, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicates.Count > 0)
            {
                found.Add(new StructuralProblem(file, $"duplicate columns: {string.Join(", ", duplicates)}"));
            }

            if (missing.Count == 0)
            {
                found.AddRange(SourceKeyProblems(file, document));
            }

            documents[file] = document;
        }

        if (!Directory.Exists(Path.Combine(root, ExportContract.FilesDirectory))
            && documents.TryGetValue(ExportContract.Documents, out var manifest)
            && manifest.Rows.Count > 0)
        {
            found.Add(new StructuralProblem(
                ExportContract.FilesDirectory,
                "the manifest lists documents but the files directory is absent"));
        }

        problems = found;
        if (found.Count > 0)
        {
            return false;
        }

        exportSet = new ExportSet(root, documents);
        return true;
    }

    private static IEnumerable<StructuralProblem> SourceKeyProblems(string file, CsvDocument document)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var blank = 0;
        var duplicated = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var row in document.Rows)
        {
            var key = row[ExportContract.SourceKeyColumn].Trim();
            if (key.Length == 0)
            {
                blank++;
                continue;
            }
            if (!seen.Add(key))
            {
                duplicated.Add(key);
            }
        }

        if (blank > 0)
        {
            yield return new StructuralProblem(file, $"{blank} row(s) have no {ExportContract.SourceKeyColumn}");
        }
        if (duplicated.Count > 0)
        {
            // Source keys are identifiers, not personal data, so naming them is safe and is
            // the only way an operator can find the offending rows in Access.
            yield return new StructuralProblem(
                file,
                $"duplicate {ExportContract.SourceKeyColumn} values: {string.Join(", ", duplicated)}");
        }
    }
}
