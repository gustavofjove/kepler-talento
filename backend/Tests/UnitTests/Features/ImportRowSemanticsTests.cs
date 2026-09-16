using System.Reflection;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Import;
using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Import;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// The shared row semantics after the KTL-17 extraction, and the import evaluator built on them.
/// </summary>
public sealed class ImportRowSemanticsTests
{
    private static readonly Guid English = Guid.CreateVersion7();
    private static readonly Guid B2 = Guid.CreateVersion7();

    [Fact]
    public void Load_result_counts_sum_to_the_rows_it_recorded()
    {
        var result = new LoadResult();
        result.Record("candidate", "1", RowOutcome.Loaded);
        result.AddProblem(new RowProblem("candidate", "2", "email", "email.invalid"));
        result.Record("candidate", "3", RowOutcome.Skipped);
        result.AddProblemWithoutRejecting(new RowProblem("candidate", "3", "email", "candidate.duplicate"));

        Assert.Equal(
            result.TotalRows,
            result.Count(RowOutcome.Loaded) + result.Count(RowOutcome.Rejected) + result.Count(RowOutcome.Skipped));
        Assert.Equal(RowOutcome.Skipped, result.OutcomeOf("candidate", "3"));
    }

    [Fact]
    public void Every_rejected_row_in_a_load_result_carries_a_reason_code()
    {
        var result = new LoadResult();
        result.AddProblem(new RowProblem("candidate", "7", "first_name", "field.required"));

        var rejected = result.SourceKeys("candidate", RowOutcome.Rejected);
        Assert.All(rejected, key => Assert.Contains(result.Problems, problem => problem.SourceKey == key && problem.ReasonCode.Length > 0));
    }

    [Theory]
    [InlineData(typeof(RowProblem), new[] { "Entity", "Field", "ReasonCode", "SourceKey" })]
    [InlineData(typeof(StructuralProblem), new[] { "Detail", "File" })]
    [InlineData(typeof(UnresolvedValue), new[] { "Family", "Occurrences", "Value" })]
    public void No_shared_outcome_type_gains_a_place_for_a_row_value(Type type, string[] expected)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name => name != "EqualityContract")
            .Order();

        Assert.Equal(expected, properties);
    }

    [Fact]
    public void Validation_outcomes_sum_to_the_data_row_count()
    {
        var evaluator = NewEvaluator();
        var rows = new[]
        {
            Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")),
            Row(2, ("first_name", ""), ("last_name", "Ruiz"), ("email", "blank@example.test")),
            Row(3, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ANA@example.test ")),
            Row(4, ("first_name", "Luis"), ("last_name", "Gil"), ("email", "not-an-address")),
        };

        var outcomes = rows.Select(evaluator.Evaluate).ToList();

        Assert.Equal(rows.Length, outcomes.Count(o => o.Outcome == RowOutcome.Loaded)
            + outcomes.Count(o => o.Outcome == RowOutcome.Rejected)
            + outcomes.Count(o => o.Outcome == RowOutcome.Skipped));
        Assert.All(outcomes.Where(o => o.Outcome != RowOutcome.Loaded), o => Assert.False(string.IsNullOrEmpty(o.ReasonCode)));
    }

    [Theory]
    [InlineData("first_name", "", ImportReasonCodes.FieldRequired)]
    [InlineData("email", "", ImportReasonCodes.FieldRequired)]
    [InlineData("email", "two@@example.test", ImportReasonCodes.EmailInvalid)]
    [InlineData("status", "archived", ImportReasonCodes.StatusUnknown)]
    [InlineData("consent_at", "17/03/2026", ImportReasonCodes.DateInvalid)]
    [InlineData("languages", "Inglés", ImportReasonCodes.ReferenceMalformed)]
    [InlineData("languages", "Klingon:B2", ImportReasonCodes.ReferenceUnresolved)]
    [InlineData("languages", "Inglés:B2;inglés:B2", ImportReasonCodes.ReferenceDuplicate)]
    [InlineData("phone", "01234567890123456789012345678901234567890", ImportReasonCodes.FieldTooLong)]
    public void A_row_failing_a_rule_is_rejected_with_the_rule_and_the_column(string column, string value, string reasonCode)
    {
        var evaluator = NewEvaluator();
        var values = new Dictionary<string, string>
        {
            ["first_name"] = "Ana",
            ["last_name"] = "Ruiz",
            ["email"] = "ana@example.test",
            [column] = value,
        };

        var evaluation = evaluator.Evaluate(new ImportFileRow(12, values, shapeValid: true));

        Assert.Equal(RowOutcome.Rejected, evaluation.Outcome);
        Assert.Equal(column, evaluation.Field);
        Assert.Equal(reasonCode, evaluation.ReasonCode);
        Assert.Null(evaluation.Command);
        Assert.Equal("12", evaluation.Problem!.SourceKey);
    }

    [Fact]
    public void A_rejected_row_carries_no_value_from_the_row()
    {
        var evaluator = NewEvaluator();
        var evaluation = evaluator.Evaluate(Row(3, ("first_name", "Zoraida"), ("last_name", "Villalobos"), ("email", "zoraida@@sentinel")));

        var rendered = string.Join('|', evaluation.RowNumber, evaluation.Field, evaluation.ReasonCode, evaluation.Problem);
        Assert.DoesNotContain("Zoraida", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unresolved_reference_is_reported_by_family_and_value_and_nothing_is_created()
    {
        var resolver = NewResolver();
        var evaluator = new CandidateImportRowEvaluator(resolver, new HashSet<string>());

        var evaluation = evaluator.Evaluate(Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test"), ("languages", "Klingon:B2")));

        Assert.Equal(RowOutcome.Rejected, evaluation.Outcome);
        var unresolved = Assert.Single(resolver.UnresolvedValues);
        Assert.Equal(CatalogFamilies.Language, unresolved.Family);
        Assert.Equal("Klingon", unresolved.Value);
    }

    [Fact]
    public void A_resolved_reference_loads_with_its_catalog_entries()
    {
        var evaluation = NewEvaluator().Evaluate(Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test"), ("languages", " inglés : b2 ")));

        Assert.Equal(RowOutcome.Loaded, evaluation.Outcome);
        Assert.Equal(new ImportedLanguage(English, B2), Assert.Single(evaluation.Languages));
    }

    [Fact]
    public void Two_rows_for_the_same_person_load_once_and_skip_once()
    {
        var evaluator = NewEvaluator();

        var first = evaluator.Evaluate(Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")));
        var second = evaluator.Evaluate(Row(2, ("first_name", "Ana María"), ("last_name", "Ruiz"), ("email", " ANA@Example.test")));

        Assert.Equal(RowOutcome.Loaded, first.Outcome);
        Assert.Equal(RowOutcome.Skipped, second.Outcome);
        Assert.Equal(ImportReasonCodes.CandidateDuplicate, second.ReasonCode);
    }

    [Fact]
    public void A_broken_row_does_not_claim_an_address_for_a_later_correct_row()
    {
        var evaluator = NewEvaluator();

        var broken = evaluator.Evaluate(Row(1, ("first_name", ""), ("last_name", "Ruiz"), ("email", "ana@example.test")));
        var fixedRow = evaluator.Evaluate(Row(2, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")));

        Assert.Equal(RowOutcome.Rejected, broken.Outcome);
        Assert.Equal(RowOutcome.Loaded, fixedRow.Outcome);
    }

    [Fact]
    public void A_row_for_a_person_already_in_the_database_is_skipped_not_rejected()
    {
        var evaluator = new CandidateImportRowEvaluator(NewResolver(), new HashSet<string> { "ana@example.test" });

        var evaluation = evaluator.Evaluate(Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "Ana@example.test")));

        Assert.Equal(RowOutcome.Skipped, evaluation.Outcome);
        Assert.Equal(ImportReasonCodes.CandidateDuplicate, evaluation.ReasonCode);
    }

    [Fact]
    public void A_resumed_run_judges_later_rows_exactly_as_the_uninterrupted_run()
    {
        var rows = new[]
        {
            Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")),
            Row(2, ("first_name", "Luis"), ("last_name", "Gil"), ("email", "luis@example.test")),
            Row(3, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")),
        };
        var uninterrupted = NewEvaluator();
        var expected = rows.Select(row => uninterrupted.Evaluate(row).Outcome).ToArray();

        var resumed = NewEvaluator();
        resumed.RecordEarlierLoad("ana@example.test");
        var actual = new[] { expected[0] }.Concat(rows.Skip(1).Select(row => resumed.Evaluate(row).Outcome)).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void A_row_with_the_wrong_number_of_fields_is_rejected_as_a_shape_problem()
    {
        var evaluation = NewEvaluator().Evaluate(new ImportFileRow(5, new Dictionary<string, string>(), shapeValid: false));

        Assert.Equal(RowOutcome.Rejected, evaluation.Outcome);
        Assert.Equal(ImportReasonCodes.RowShapeInvalid, evaluation.ReasonCode);
    }

    [Fact]
    public void Absent_status_defaults_to_new_as_a_direct_create_would()
    {
        var evaluation = NewEvaluator().Evaluate(Row(1, ("first_name", "Ana"), ("last_name", "Ruiz"), ("email", "ana@example.test")));

        Assert.Equal("new", evaluation.Command!.Status);
    }

    [Fact]
    public void Every_row_reason_code_is_distinct_from_every_batch_code()
    {
        Assert.Empty(ImportReasonCodes.RowCodes.Intersect(ImportReasonCodes.BatchCodes));
        Assert.Equal(ImportReasonCodes.RowCodes.Count, ImportReasonCodes.RowCodes.Distinct().Count());
        Assert.Equal(ImportReasonCodes.BatchCodes.Count, ImportReasonCodes.BatchCodes.Distinct().Count());
    }

    private static CatalogResolver NewResolver() => new(
    [
        new CatalogResolver.CatalogEntry(English, CatalogFamilies.Language, CatalogName.DeriveCode("Inglés"), "Inglés"),
        new CatalogResolver.CatalogEntry(B2, CatalogFamilies.LanguageLevel, CatalogName.DeriveCode("B2"), "B2"),
    ]);

    private static CandidateImportRowEvaluator NewEvaluator() => new(NewResolver(), new HashSet<string>());

    private static ImportFileRow Row(int number, params (string Column, string Value)[] values) =>
        new(number, values.ToDictionary(pair => pair.Column, pair => pair.Value), shapeValid: true);
}
