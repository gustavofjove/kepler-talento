using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;

namespace KeplerTalento.Tools.TestDataGenerator;

/// <summary>
/// Builds one synthetic export set. Every decision comes from a single seeded
/// <see cref="Random"/>, so a seed reproduces a run byte for byte and a failing dataset can
/// be handed to someone else as a number.
/// </summary>
public sealed class ExportSetGenerator(GeneratorOptions options)
{
    private readonly Random _random = new(options.Seed);
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Status mix. Weighted rather than uniform so list ordering, status filters and the
    /// counts on the dashboard look like a real pipeline instead of five equal buckets.
    /// </summary>
    private static readonly (string Status, int Weight)[] StatusWeights =
    [
        (CandidateStatuses.New, 20),
        (CandidateStatuses.Available, 34),
        (CandidateStatuses.InProcess, 20),
        (CandidateStatuses.Hired, 9),
        (CandidateStatuses.Rejected, 17),
    ];

    public async Task<GenerationSummary> WriteAsync(CancellationToken cancellationToken)
    {
        var exportDirectory = Path.Combine(options.OutputDirectory, "export");
        var filesDirectory = Path.Combine(exportDirectory, ExportContract.FilesDirectory);
        Directory.CreateDirectory(filesDirectory);

        var candidates = new CsvWriter(ExportContract.Columns[ExportContract.Candidates]);
        var languages = new CsvWriter(ExportContract.Columns[ExportContract.Languages]);
        var programs = new CsvWriter(ExportContract.Columns[ExportContract.Programs]);
        var education = new CsvWriter(ExportContract.Columns[ExportContract.Education]);
        var experience = new CsvWriter(ExportContract.Columns[ExportContract.Experience]);
        var skills = new CsvWriter(ExportContract.Columns[ExportContract.Skills]);
        var documents = new CsvWriter(ExportContract.Columns[ExportContract.Documents]);

        var summary = new GenerationSummary();

        for (var number = 1; number <= options.Candidates; number++)
        {
            var candidate = BuildCandidate(number);
            WriteCandidate(candidates, candidate);
            summary.Candidates++;
            summary.Inactive += candidate.DeletedAt is null ? 0 : 1;

            summary.Languages += WriteLanguages(languages, candidate);
            summary.Programs += WritePrograms(programs, candidate);
            summary.Skills += WriteSkills(skills, candidate);
            summary.Education += WriteEducation(education, candidate);
            summary.Experience += WriteExperience(experience, candidate);
            summary.Documents += await WriteDocumentAsync(
                documents, filesDirectory, candidate, cancellationToken);
        }

        await candidates.SaveAsync(Path.Combine(exportDirectory, ExportContract.Candidates), cancellationToken);
        await languages.SaveAsync(Path.Combine(exportDirectory, ExportContract.Languages), cancellationToken);
        await programs.SaveAsync(Path.Combine(exportDirectory, ExportContract.Programs), cancellationToken);
        await education.SaveAsync(Path.Combine(exportDirectory, ExportContract.Education), cancellationToken);
        await experience.SaveAsync(Path.Combine(exportDirectory, ExportContract.Experience), cancellationToken);
        await skills.SaveAsync(Path.Combine(exportDirectory, ExportContract.Skills), cancellationToken);
        await documents.SaveAsync(Path.Combine(exportDirectory, ExportContract.Documents), cancellationToken);

        return summary;
    }

    private sealed record GeneratedCandidate(
        int Number,
        string SourceKey,
        string FirstName,
        string LastName,
        string Email,
        DateOnly ReceivedAt,
        DateOnly ConsentAt,
        DateOnly? DeletedAt);

    private GeneratedCandidate BuildCandidate(int number)
    {
        var firstName = Pick(SpanishVocabulary.FirstNames);
        var lastName = $"{Pick(SpanishVocabulary.LastNames)} {Pick(SpanishVocabulary.LastNames)}";

        // The number keeps the address unique without making the name itself unnatural;
        // .invalid is reserved by RFC 2606 and can never reach a real mailbox.
        var localPart = $"{Ascii(firstName)}.{Ascii(lastName).Replace(' ', '.')}{number}";
        var receivedAt = _today.AddDays(-_random.Next(0, 1095));
        // Consent is recorded when the application is filed, occasionally a day or two later.
        var consentAt = receivedAt.AddDays(_random.Next(0, 3));

        // A small share are logically removed. DeletedAt must be present exactly when the
        // row is inactive, or the validator rejects it as inconsistent.
        DateOnly? deletedAt = _random.Next(100) < 8
            ? consentAt.AddDays(_random.Next(30, 400))
            : null;
        if (deletedAt > _today)
        {
            deletedAt = _today;
        }

        return new GeneratedCandidate(
            number,
            $"C-{number:D5}",
            firstName,
            lastName,
            $"{localPart}@example.invalid",
            receivedAt,
            consentAt,
            deletedAt);
    }

    private void WriteCandidate(CsvWriter writer, GeneratedCandidate candidate)
    {
        var (city, province) = Pick(SpanishVocabulary.Places);
        writer.WriteRow(
            candidate.SourceKey,
            candidate.FirstName,
            candidate.LastName,
            $"+34 6{_random.Next(0, 100):D2} {_random.Next(0, 1000):D3} {_random.Next(0, 1000):D3}",
            candidate.Email,
            city,
            province,
            "España",
            Pick(SpanishVocabulary.Availabilities),
            PickStatus(),
            Pick(SpanishVocabulary.Sources),
            Pick(SpanishVocabulary.CandidateNotes),
            Date(candidate.ReceivedAt),
            Date(candidate.ConsentAt),
            // The review horizon the retention rules use: two years from consent.
            Date(candidate.ConsentAt.AddYears(2)),
            candidate.DeletedAt is null ? "true" : "false",
            Date(candidate.DeletedAt));
    }

    private int WriteLanguages(CsvWriter writer, GeneratedCandidate candidate)
    {
        var chosen = PickDistinct(Seeded(CatalogFamilies.Language), _random.Next(1, 4));
        for (var index = 0; index < chosen.Count; index++)
        {
            writer.WriteRow(
                $"L-{candidate.Number:D5}-{index + 1}",
                candidate.SourceKey,
                chosen[index],
                Pick(Seeded(CatalogFamilies.LanguageLevel)),
                Pick(SpanishVocabulary.Certifications),
                Pick(SpanishVocabulary.RelationNotes));
        }
        return chosen.Count;
    }

    private int WritePrograms(CsvWriter writer, GeneratedCandidate candidate)
    {
        var chosen = PickDistinct(Seeded(CatalogFamilies.Program), _random.Next(1, 6));
        for (var index = 0; index < chosen.Count; index++)
        {
            writer.WriteRow(
                $"P-{candidate.Number:D5}-{index + 1}",
                candidate.SourceKey,
                chosen[index],
                Pick(Seeded(CatalogFamilies.ProgramLevel)),
                _random.Next(1, 16).ToString(CultureInfo.InvariantCulture),
                Pick(SpanishVocabulary.RelationNotes));
        }
        return chosen.Count;
    }

    private int WriteSkills(CsvWriter writer, GeneratedCandidate candidate)
    {
        var chosen = PickDistinct(Seeded(CatalogFamilies.Skill), _random.Next(1, 6));
        for (var index = 0; index < chosen.Count; index++)
        {
            writer.WriteRow(
                $"S-{candidate.Number:D5}-{index + 1}",
                candidate.SourceKey,
                chosen[index],
                Pick(Seeded(CatalogFamilies.SkillLevel)),
                Pick(SpanishVocabulary.RelationNotes));
        }
        return chosen.Count;
    }

    private int WriteEducation(CsvWriter writer, GeneratedCandidate candidate)
    {
        var chosen = PickDistinct(Seeded(CatalogFamilies.EducationType), _random.Next(1, 4));
        for (var index = 0; index < chosen.Count; index++)
        {
            var educationType = chosen[index];
            var status = Pick(Seeded(CatalogFamilies.EducationStatus));
            writer.WriteRow(
                $"E-{candidate.Number:D5}-{index + 1}",
                candidate.SourceKey,
                educationType,
                DegreeFor(educationType),
                Pick(SpanishVocabulary.Specialties),
                Pick(SpanishVocabulary.Institutions),
                status,
                _random.Next(candidate.ReceivedAt.Year - 25, candidate.ReceivedAt.Year + 1)
                    .ToString(CultureInfo.InvariantCulture),
                Pick(SpanishVocabulary.RelationNotes));
        }
        return chosen.Count;
    }

    /// <summary>
    /// A welding or metal certification is not a degree title; giving it the same
    /// "Ingeniería Mecánica" text as a university row would make the screens read as noise.
    /// </summary>
    private string DegreeFor(string educationType) =>
        educationType.StartsWith("Soldadura", StringComparison.Ordinal)
            ? $"{educationType} homologada"
            : educationType == "Tarjeta del metal"
                ? $"Tarjeta profesional del metal ({(_random.Next(0, 2) == 0 ? "20" : "60")} horas)"
                : Pick(SpanishVocabulary.DegreeTitles);

    private int WriteExperience(CsvWriter writer, GeneratedCandidate candidate)
    {
        var count = _random.Next(0, 5);
        // Walk backwards from the application date so the periods never overlap and the
        // most recent row is the one that may still be open.
        var cursor = candidate.ReceivedAt.AddDays(-_random.Next(0, 120));

        for (var index = 0; index < count; index++)
        {
            var months = _random.Next(4, 73);
            var start = cursor.AddMonths(-months);
            // Only the first row written — the most recent — can be the current job.
            var isCurrent = index == 0 && candidate.DeletedAt is null && _random.Next(100) < 35;
            DateOnly? end = isCurrent ? null : cursor;

            writer.WriteRow(
                $"X-{candidate.Number:D5}-{index + 1}",
                candidate.SourceKey,
                Pick(SpanishVocabulary.Companies),
                Pick(SpanishVocabulary.Positions),
                Pick(Seeded(CatalogFamilies.Sector)),
                Pick(SpanishVocabulary.Functions),
                Date(start),
                Date(end),
                Math.Max(1, months / 12).ToString(CultureInfo.InvariantCulture),
                isCurrent ? "true" : "false",
                Pick(SpanishVocabulary.RelationNotes));

            cursor = start.AddDays(-_random.Next(0, 200));
        }
        return count;
    }

    private async Task<int> WriteDocumentAsync(
        CsvWriter writer,
        string filesDirectory,
        GeneratedCandidate candidate,
        CancellationToken cancellationToken)
    {
        // Not every candidate arrives with a CV attached, and the screens have to render
        // that case too.
        if (_random.Next(100) >= 75)
        {
            return 0;
        }

        var relativePath = $"cv-{candidate.Number:D5}.txt";
        var content = BuildCurriculum(candidate);
        var absolute = Path.Combine(filesDirectory, relativePath);
        await File.WriteAllTextAsync(absolute, content, new UTF8Encoding(false), cancellationToken);

        // The manifest hash must match the bytes on disk or the loader rejects the document.
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(await File.ReadAllBytesAsync(absolute, cancellationToken)));

        writer.WriteRow(
            $"D-{candidate.Number:D5}-1",
            candidate.SourceKey,
            relativePath,
            "CV",
            $"CV {candidate.FirstName} {candidate.LastName}.txt",
            "text/plain",
            hash,
            "true");
        return 1;
    }

    private static string BuildCurriculum(GeneratedCandidate candidate) =>
        $"""
        CURRÍCULUM VITAE
        ================

        {candidate.FirstName} {candidate.LastName}
        {candidate.Email}

        Documento sintético generado por ktl-testdata para pruebas de desarrollo.
        No corresponde a ninguna persona real.

        Candidatura recibida: {candidate.ReceivedAt:yyyy-MM-dd}

        """;

    private static IReadOnlyList<string> Seeded(string family) =>
        SeededByFamily.TryGetValue(family, out var values) ? values : [];

    private static readonly Dictionary<string, IReadOnlyList<string>> SeededByFamily =
        CatalogSeedData.Families.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)[.. entry.Value.Select(value => value.NameEs)],
            StringComparer.Ordinal);

    private string PickStatus()
    {
        var total = StatusWeights.Sum(entry => entry.Weight);
        var roll = _random.Next(total);
        foreach (var (status, weight) in StatusWeights)
        {
            if (roll < weight)
            {
                return status;
            }
            roll -= weight;
        }
        return CandidateStatuses.Available;
    }

    private T Pick<T>(IReadOnlyList<T> values) => values[_random.Next(values.Count)];

    /// <summary>
    /// Distinct because the relation tables carry a unique index per candidate and value;
    /// drawing the same skill twice would fail the load rather than the generator.
    /// </summary>
    private IReadOnlyList<T> PickDistinct<T>(IReadOnlyList<T> values, int count)
    {
        count = Math.Min(count, values.Count);
        var pool = values.ToList();
        var chosen = new List<T>(count);
        for (var index = 0; index < count; index++)
        {
            var position = _random.Next(pool.Count);
            chosen.Add(pool[position]);
            pool.RemoveAt(position);
        }
        return chosen;
    }

    private static string Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Folds accents for the email local part, reusing the catalog's own table.</summary>
    private static string Ascii(string value) =>
        new(CatalogName.Normalize(value).Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray());
}

public sealed class GenerationSummary
{
    public int Candidates { get; set; }
    public int Inactive { get; set; }
    public int Languages { get; set; }
    public int Programs { get; set; }
    public int Skills { get; set; }
    public int Education { get; set; }
    public int Experience { get; set; }
    public int Documents { get; set; }
}
