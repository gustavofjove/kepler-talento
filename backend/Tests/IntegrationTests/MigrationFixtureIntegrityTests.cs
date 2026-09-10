using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// The fixtures are the evidence for every migration test, so a fixture that quietly drifts
/// would weaken all of them at once. These assertions keep the export set honest about what
/// its README claims it contains.
/// </summary>
public sealed class MigrationFixtureIntegrityTests
{
    [Fact]
    public void The_export_set_contains_every_required_file()
    {
        Assert.True(Directory.Exists(MigrationFixtures.ExportDirectory), MigrationFixtures.ExportDirectory);
        Assert.All(
            MigrationFixtures.EntityFiles,
            file => Assert.True(
                File.Exists(Path.Combine(MigrationFixtures.ExportDirectory, file)),
                $"Missing fixture file: {file}"));
        Assert.True(File.Exists(MigrationFixtures.MappingFile));
    }

    [Fact]
    public void The_mapping_file_sits_outside_the_export_set()
    {
        // It is an operator decision about the data, not something Access produced.
        Assert.False(File.Exists(Path.Combine(MigrationFixtures.ExportDirectory, "mappings.csv")));
    }

    [Fact]
    public void Every_source_key_is_unique_within_its_file()
    {
        foreach (var file in MigrationFixtures.EntityFiles)
        {
            var keys = MigrationFixtures.ReadCsv(file).Select(row => row["SourceKey"]).ToList();
            Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(keys, key => string.IsNullOrWhiteSpace(key));
        }
    }

    [Fact]
    public void Every_child_row_points_at_a_candidate_in_the_export()
    {
        var candidateKeys = MigrationFixtures.ReadCsv("candidates.csv")
            .Select(row => row["SourceKey"])
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in MigrationFixtures.EntityFiles.Where(name => name != "candidates.csv"))
        {
            Assert.All(
                MigrationFixtures.ReadCsv(file),
                row => Assert.Contains(row["CandidateSourceKey"], candidateKeys));
        }
    }

    [Fact]
    public void The_candidate_rows_cover_every_outcome_the_migration_can_produce()
    {
        var candidates = MigrationFixtures.ReadCsv("candidates.csv");
        Assert.Equal(7, candidates.Count);

        // A row with no consent date, so the consent rejection has something to reject.
        Assert.Contains(candidates, row => row["SourceKey"] == "C-003" && row["ConsentAt"].Length == 0);
        // A malformed email, for field validation.
        Assert.Contains(candidates, row => row["SourceKey"] == "C-004" && !row["Email"].Contains('@'));
        // A logically removed row, which must arrive as logical state rather than absence.
        Assert.Contains(
            candidates,
            row => row["SourceKey"] == "C-002" && row["IsActive"] == "false" && row["DeletedAt"].Length > 0);
        // Active rows carry no removal date.
        Assert.All(
            candidates.Where(row => row["IsActive"] == "true"),
            row => Assert.Empty(row["DeletedAt"]));
    }

    [Fact]
    public void The_language_rows_cover_all_three_resolver_steps_and_a_failure()
    {
        var languages = MigrationFixtures.ReadCsv("languages.csv");
        Assert.Contains(languages, row => row["Language"] == "Inglés");   // exact
        Assert.Contains(languages, row => row["Language"] == "ingles");   // normalized code
        Assert.Contains(languages, row => row["Language"] == "Ingl.");    // mapping file
        Assert.Contains(languages, row => row["Language"] == "Klingon");  // unresolvable
    }

    [Fact]
    public void The_mapping_file_only_resolves_the_value_that_needs_it()
    {
        var lines = File.ReadAllLines(MigrationFixtures.MappingFile)
            .Where(line => line.Trim().Length > 0)
            .ToList();
        Assert.Equal("Family,SourceValue,TargetCode", lines[0]);
        var mapping = Assert.Single(lines.Skip(1));
        Assert.Equal("language,Ingl.,INGLES", mapping);
    }

    [Fact]
    public async Task Document_hashes_match_their_files_except_the_deliberate_mismatch()
    {
        foreach (var row in MigrationFixtures.ReadCsv("documents.csv"))
        {
            var path = MigrationFixtures.DocumentFile(row["RelativePath"]);
            Assert.True(File.Exists(path), path);
            var actual = await MigrationFixtures.Sha256Async(path, CancellationToken.None);
            if (row["SourceKey"] == "D-006")
            {
                Assert.NotEqual(actual, row["Sha256"]);
                continue;
            }
            Assert.Equal(actual, row["Sha256"]);
        }
    }

    [Fact]
    public void At_most_one_document_per_candidate_claims_to_be_primary()
    {
        var primaries = MigrationFixtures.ReadCsv("documents.csv")
            .Where(row => row["IsPrimary"] == "true")
            .GroupBy(row => row["CandidateSourceKey"], StringComparer.Ordinal);
        Assert.All(primaries, group => Assert.Single(group));
    }

    [Fact]
    public void The_malware_fixture_is_a_synthetic_marker_and_not_a_real_eicar_string()
    {
        var content = File.ReadAllText(MigrationFixtures.DocumentFile("cv-007.txt"));
        Assert.Contains("SYNTHETIC-MALWARE-MARKER", content, StringComparison.Ordinal);
        // A real EICAR string would be quarantined by antivirus on a developer machine and
        // would break the checkout rather than the test.
        Assert.DoesNotContain("EICAR-STANDARD-ANTIVIRUS-TEST-FILE", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_personal_data_field_carries_a_sentinel()
    {
        // The no-leak test searches for one prefix; that only proves anything if the
        // fixtures actually put it in every field a leak could come from.
        foreach (var row in MigrationFixtures.ReadCsv("candidates.csv"))
        {
            foreach (var field in new[] { "FirstName", "LastName", "Phone", "Location", "Province", "Notes" })
            {
                Assert.Contains(MigrationFixtures.SentinelPrefix, row[field], StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void No_fixture_file_contains_a_byte_order_mark_free_encoding_accident()
    {
        // Accents surviving the round trip is the cheapest possible mojibake check.
        var languages = File.ReadAllText(Path.Combine(MigrationFixtures.ExportDirectory, "languages.csv"));
        Assert.Contains("Inglés", languages, StringComparison.Ordinal);
        Assert.Contains("Alemán", languages, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", languages, StringComparison.Ordinal);
    }
}
