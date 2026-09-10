using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeplerTalento.Tools.DataMigration.Reporting;

/// <summary>
/// Emits the reconciliation report as JSON, for the run record and for tooling, and as
/// Markdown, for the person who has to read it.
/// </summary>
public static class ReportWriter
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(ReconciliationReport report) =>
        JsonSerializer.Serialize(report, JsonOptions);

    public static ReconciliationReport FromJson(string json) =>
        JsonSerializer.Deserialize<ReconciliationReport>(json, JsonOptions)
            ?? throw new InvalidOperationException("The stored report could not be read.");

    /// <summary>
    /// Writes both renderings and returns their paths.
    /// </summary>
    public static async Task<(string JsonPath, string MarkdownPath)> WriteAsync(
        ReconciliationReport report,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, $"reconciliation-{report.RunId}.json");
        var markdownPath = Path.Combine(outputDirectory, $"reconciliation-{report.RunId}.md");
        await File.WriteAllTextAsync(jsonPath, ToJson(report), new UTF8Encoding(false), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, ToMarkdown(report), new UTF8Encoding(false), cancellationToken);
        return (jsonPath, markdownPath);
    }

    public static string ToMarkdown(ReconciliationReport report)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"# Migration reconciliation — run {report.RunId}");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Verb:** `{report.Verb}`");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Started:** {report.StartedAtUtc:u}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Finished:** {report.FinishedAtUtc:u}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Outcome:** {report.Outcome}");
        text.AppendLine(
            report.PreMigrationBackup is null
                ? "- **Pre-migration backup:** not applicable — this run wrote nothing"
                : $"- **Pre-migration backup:** `{report.PreMigrationBackup}`");
        if (report.OverwrittenApplicationEdits > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"- **Application edits overwritten:** {report.OverwrittenApplicationEdits}");
        }
        text.AppendLine();

        if (!report.Reconciles)
        {
            text.AppendLine("> **This run did not reconcile.** The counts below do not account for");
            text.AppendLine("> every source row. Treat the load as incomplete and investigate before");
            text.AppendLine("> relying on the data.");
            text.AppendLine();
        }

        text.AppendLine("## Row counts");
        text.AppendLine();
        text.AppendLine("| Entity | Source | Loaded | Rejected | Skipped | Accounted for |");
        text.AppendLine("| ------ | -----: | -----: | -------: | ------: | ------------- |");
        foreach (var tally in report.Entities)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {tally.Entity} | {tally.SourceRows} | {tally.Loaded} | {tally.Rejected} | {tally.Skipped} | {(tally.Reconciles ? "yes" : "**no**")} |");
        }
        text.AppendLine();

        AppendUnresolved(text, "Unresolved reference values", report.Unresolved,
            "These resolved to no catalog entry. Nothing was created for them, and every row "
            + "carrying one was rejected. Add the value through the catalog administration "
            + "screens or map it in `mappings.csv`, then re-run.");

        AppendUnresolved(text, "Broken mappings", report.BrokenMappings,
            "These are mapped in `mappings.csv` to a catalog code that does not exist. Correct "
            + "the mapping file and re-run.");

        if (report.Rejected.Count > 0)
        {
            text.AppendLine("## Rejected rows");
            text.AppendLine();
            text.AppendLine("Identified by source key and failing field. Values are deliberately absent.");
            text.AppendLine();
            text.AppendLine("| Entity | Source key | Field | Reason |");
            text.AppendLine("| ------ | ---------- | ----- | ------ |");
            foreach (var row in report.Rejected)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"| {row.Entity} | `{row.SourceKey}` | {row.Field} | `{row.ReasonCode}` |");
            }
            text.AppendLine();
        }

        if (report.Skipped.Count > 0)
        {
            text.AppendLine("## Skipped rows");
            text.AppendLine();
            text.AppendLine("Left exactly as they are. These are sound records the migration chose not to touch.");
            text.AppendLine();
            text.AppendLine("| Entity | Source key | Reason |");
            text.AppendLine("| ------ | ---------- | ------ |");
            foreach (var row in report.Skipped)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"| {row.Entity} | `{row.SourceKey}` | `{row.ReasonCode}` |");
            }
            text.AppendLine();
        }

        if (report.UnmatchedTargetRecords.Count > 0)
        {
            text.AppendLine("## Stored records absent from this export");
            text.AppendLine();
            text.AppendLine("Counted apart from rejections and skips, and **nothing was changed about them**.");
            text.AppendLine("A row deleted in Access and a row missing from the export query look identical");
            text.AppendLine("from here, so the decision is yours.");
            text.AppendLine();
            text.AppendLine("| Entity | Source key |");
            text.AppendLine("| ------ | ---------- |");
            foreach (var record in report.UnmatchedTargetRecords)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"| {record.Entity} | `{record.SourceKey}` |");
            }
            text.AppendLine();
        }

        if (report.Documents.Count > 0)
        {
            text.AppendLine("## Document verification");
            text.AppendLine();
            text.AppendLine("`matched` means the bytes in private storage hash to the value the manifest");
            text.AppendLine("declared and the scan came back clean. No storage key or path appears here.");
            text.AppendLine();
            text.AppendLine("| Source key | Result | Reason |");
            text.AppendLine("| ---------- | ------ | ------ |");
            foreach (var document in report.Documents)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"| `{document.SourceKey}` | {document.Result} | {(document.ReasonCode is null ? "—" : $"`{document.ReasonCode}`")} |");
            }
            text.AppendLine();
        }

        if (report.Checklist.Count > 0)
        {
            text.AppendLine("## What to do next");
            text.AppendLine();
            foreach (var item in report.Checklist)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- [ ] {item}");
            }
            text.AppendLine();
        }

        return text.ToString();
    }

    private static void AppendUnresolved(
        StringBuilder text,
        string heading,
        IReadOnlyList<ReconciliationReport.UnresolvedEntry> entries,
        string explanation)
    {
        if (entries.Count == 0)
        {
            return;
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"## {heading}");
        text.AppendLine();
        text.AppendLine(explanation);
        text.AppendLine();
        text.AppendLine("| Family | Value | Occurrences |");
        text.AppendLine("| ------ | ----- | ----------: |");
        foreach (var entry in entries)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"| {entry.Family} | {entry.Value} | {entry.Occurrences} |");
        }
        text.AppendLine();
    }
}
