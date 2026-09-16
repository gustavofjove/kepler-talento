using KeplerTalento.Domain.Import;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class ImportBatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly string Digest = new('a', 64);

    /// <summary>Every transition the design allows, and the state it lands in.</summary>
    public static TheoryData<string, string> ValidTransitions => new()
    {
        { "start-scan", ImportBatchStates.Scanning },
        { "scanned", ImportBatchStates.Scanned },
        { "infected", ImportBatchStates.Infected },
        { "unscannable", ImportBatchStates.Unscannable },
        { "start-validation", ImportBatchStates.Validating },
        { "validated", ImportBatchStates.Validated },
        { "validation-failed", ImportBatchStates.Failed },
        { "start-commit", ImportBatchStates.Committing },
        { "committed", ImportBatchStates.Committed },
        { "commit-failed", ImportBatchStates.Failed },
        { "purge-validated", ImportBatchStates.Expired },
        { "purge-committed", ImportBatchStates.Expired },
        { "purge-failed", ImportBatchStates.Expired },
        { "purge-infected", ImportBatchStates.Infected },
        { "purge-unscannable", ImportBatchStates.Unscannable },
    };

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void Every_valid_transition_lands_in_its_documented_state(string path, string expected)
    {
        var batch = Walk(path);
        Assert.Equal(expected, batch.State);
    }

    [Fact]
    public void A_new_batch_starts_uploaded_with_no_counts_and_its_file_retained()
    {
        var batch = NewBatch();

        Assert.Equal(ImportBatchStates.Uploaded, batch.State);
        Assert.Null(batch.RowCount);
        Assert.Equal(0, batch.AccountedRows);
        Assert.True(batch.FileRetained);
        Assert.Null(batch.ClosedAtUtc);
    }

    /// <summary>Every operation attempted from every state it is not allowed from.</summary>
    public static TheoryData<string, string> InvalidTransitions()
    {
        var operations = new[]
        {
            "start-scan", "mark-scanned", "mark-infected", "mark-unscannable", "start-validation",
            "complete-validation", "start-commit", "complete-commit", "fail", "purge",
        };
        var allowed = new Dictionary<string, string[]>
        {
            ["start-scan"] = [ImportBatchStates.Uploaded],
            ["mark-scanned"] = [ImportBatchStates.Scanning],
            ["mark-infected"] = [ImportBatchStates.Scanning],
            ["mark-unscannable"] = [ImportBatchStates.Scanning],
            ["start-validation"] = [ImportBatchStates.Scanned],
            ["complete-validation"] = [ImportBatchStates.Validating],
            ["start-commit"] = [ImportBatchStates.Validated],
            ["complete-commit"] = [ImportBatchStates.Committing],
            ["fail"] = [ImportBatchStates.Validating, ImportBatchStates.Committing],
            ["purge"] =
            [
                ImportBatchStates.Validated, ImportBatchStates.Committed, ImportBatchStates.Failed,
                ImportBatchStates.Infected, ImportBatchStates.Unscannable,
            ],
        };
        var data = new TheoryData<string, string>();
        foreach (var state in ImportBatchStates.All)
        {
            foreach (var operation in operations)
            {
                if (!allowed[operation].Contains(state))
                {
                    data.Add(state, operation);
                }
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Every_invalid_transition_is_rejected(string state, string operation)
    {
        var batch = InState(state);

        Assert.ThrowsAny<InvalidOperationException>(() => Apply(batch, operation));
        Assert.Equal(state, batch.State);
    }

    [Fact]
    public void An_infected_file_can_never_be_made_validatable()
    {
        var batch = Walk("infected");

        Assert.Throws<InvalidOperationException>(() => batch.StartScan(Now));
        Assert.Throws<InvalidOperationException>(() => batch.MarkScanned(Now));
        Assert.Throws<InvalidOperationException>(() => batch.StartValidation(Now));
        Assert.Equal(ImportBatchStates.Infected, batch.State);
    }

    [Fact]
    public void Counts_that_do_not_account_for_every_row_are_refused()
    {
        var batch = Walk("start-validation");

        Assert.Throws<InvalidOperationException>(() => batch.CompleteValidation(10, 5, 2, 2, "[]", Now));
        Assert.Equal(ImportBatchStates.Validating, batch.State);
    }

    [Fact]
    public void A_purged_batch_keeps_its_counts_and_failure_code()
    {
        var batch = Walk("committed");

        batch.MarkFilePurged(Now.AddDays(31));

        Assert.Equal(ImportBatchStates.Expired, batch.State);
        Assert.False(batch.FileRetained);
        Assert.Equal(3, batch.RowCount);
        Assert.Equal(3, batch.AccountedRows);
    }

    [Fact]
    public void A_file_name_is_reduced_to_its_bounded_leaf()
    {
        var batch = new ImportBatch(Guid.CreateVersion7(), "imports/x/content", "../../etc/" + new string('n', 400) + ".csv", 10, Digest, null, "actor", Now);

        Assert.DoesNotContain("/", batch.OriginalFileName, StringComparison.Ordinal);
        Assert.True(batch.OriginalFileName.Length <= ImportBatch.MaximumFileNameLength);
    }

    [Fact]
    public void A_row_outcome_has_no_member_that_could_hold_a_value()
    {
        var properties = typeof(ImportRowOutcome).GetProperties().Select(property => property.Name).Order().ToArray();

        Assert.Equal(
            ["BatchId", "CandidateId", "CreatedAtUtc", "Field", "Id", "Outcome", "Phase", "ReasonCode", "RowNumber"],
            properties);
    }

    [Fact]
    public void A_rejected_row_outcome_requires_a_known_reason_code()
    {
        Assert.Throws<ArgumentException>(() => Outcome(ImportRowOutcomes.Rejected, reasonCode: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => Outcome(ImportRowOutcomes.Rejected, reasonCode: "made.up"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Outcome(ImportRowOutcomes.Rejected, reasonCode: ImportReasonCodes.FileTooLarge));
        Assert.Equal(ImportReasonCodes.EmailInvalid, Outcome(ImportRowOutcomes.Rejected, ImportReasonCodes.EmailInvalid).ReasonCode);
    }

    private static ImportRowOutcome Outcome(string outcome, string? reasonCode) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), ImportPhases.Validation, 1, outcome, "email", reasonCode, null, Now);

    private static ImportBatch NewBatch() =>
        new(Guid.CreateVersion7(), "imports/batch/content", "candidatos.csv", 120, Digest, null, "actor", Now);

    private static ImportBatch Walk(string path)
    {
        var batch = NewBatch();
        var steps = path switch
        {
            "start-scan" => ["start-scan"],
            "scanned" => ["start-scan", "mark-scanned"],
            "infected" => ["start-scan", "mark-infected"],
            "unscannable" => ["start-scan", "mark-unscannable"],
            "start-validation" => ["start-scan", "mark-scanned", "start-validation"],
            "validated" => ["start-scan", "mark-scanned", "start-validation", "complete-validation"],
            "validation-failed" => ["start-scan", "mark-scanned", "start-validation", "fail"],
            "start-commit" => ["start-scan", "mark-scanned", "start-validation", "complete-validation", "start-commit"],
            "committed" => ["start-scan", "mark-scanned", "start-validation", "complete-validation", "start-commit", "complete-commit"],
            "commit-failed" => ["start-scan", "mark-scanned", "start-validation", "complete-validation", "start-commit", "fail"],
            "purge-validated" => ["start-scan", "mark-scanned", "start-validation", "complete-validation", "purge"],
            "purge-committed" => ["start-scan", "mark-scanned", "start-validation", "complete-validation", "start-commit", "complete-commit", "purge"],
            "purge-failed" => ["start-scan", "mark-scanned", "start-validation", "fail", "purge"],
            "purge-infected" => ["start-scan", "mark-infected", "purge"],
            "purge-unscannable" => ["start-scan", "mark-unscannable", "purge"],
            _ => Array.Empty<string>(),
        };
        foreach (var step in steps)
        {
            Apply(batch, step);
        }
        return batch;
    }

    private static ImportBatch InState(string state) => state switch
    {
        ImportBatchStates.Uploaded => NewBatch(),
        ImportBatchStates.Scanning => Walk("start-scan"),
        ImportBatchStates.Scanned => Walk("scanned"),
        ImportBatchStates.Validating => Walk("start-validation"),
        ImportBatchStates.Validated => Walk("validated"),
        ImportBatchStates.Committing => Walk("start-commit"),
        ImportBatchStates.Committed => Walk("committed"),
        ImportBatchStates.Infected => Walk("infected"),
        ImportBatchStates.Unscannable => Walk("unscannable"),
        ImportBatchStates.Failed => Walk("validation-failed"),
        ImportBatchStates.Expired => Walk("purge-committed"),
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static void Apply(ImportBatch batch, string operation)
    {
        switch (operation)
        {
            case "start-scan": batch.StartScan(Now); break;
            case "mark-scanned": batch.MarkScanned(Now); break;
            case "mark-infected": batch.MarkInfected(ImportReasonCodes.ScanInfected, Now); break;
            case "mark-unscannable": batch.MarkUnscannable(ImportReasonCodes.ScanUnscannable, Now); break;
            case "start-validation": batch.StartValidation(Now); break;
            case "complete-validation": batch.CompleteValidation(3, 3, 0, 0, "[]", Now); break;
            case "start-commit": batch.StartCommit(Now); break;
            case "complete-commit": batch.CompleteCommit(2, 0, 1, Now); break;
            case "fail": batch.Fail(ImportReasonCodes.ColumnMissing, "email", Now); break;
            case "purge": batch.MarkFilePurged(Now); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
