using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// The filter contract, checked where it is decided. These are the rules search and saved
/// searches share, so a preset can never store a filter value the search endpoint would
/// refuse.
/// </summary>
public sealed class SearchFilterNormalizationTests
{
    [Fact]
    public void Absent_filters_restrict_nothing()
    {
        var filters = SearchFilterNormalization.Normalize(null, "Filters");

        Assert.Equal(string.Empty, filters.Text);
        Assert.True(filters.StatusIsUnrestricted);
        Assert.Empty(filters.SkillCriteria);
        Assert.Empty(filters.LanguageCriteria);
        Assert.Empty(filters.ProgramCriteria);
        Assert.Equal(CvPresence.Unset, filters.HasCv);
    }

    [Fact]
    public void Blank_text_and_blank_criteria_are_dropped_rather_than_refused()
    {
        var filters = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(
                "   ",
                null,
                [new SearchCriterionInput("  ", "B2"), new SearchCriterionInput(" Java ", "  ")],
                null,
                null,
                null,
                null,
                null,
                null),
            "Filters");

        Assert.Equal(string.Empty, filters.Text);
        var criterion = Assert.Single(filters.SkillCriteria);
        // The filter panel produces a blank line every time a user adds one before choosing
        // its value; refusing the request would make the UI unusable.
        Assert.Equal("Java", criterion.Value);
        Assert.True(criterion.MatchesAnyLevel);
    }

    [Fact]
    public void Repeated_criteria_collapse_regardless_of_case_and_spacing()
    {
        var filters = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(
                null,
                null,
                null,
                null,
                [
                    new SearchCriterionInput("Inglés", "B2"),
                    new SearchCriterionInput(" inglés ", " b2 "),
                    new SearchCriterionInput("Inglés", ""),
                ],
                "ALL",
                null,
                null,
                null),
            "Filters");

        // "Inglés at B2" twice is one requirement; "Inglés at any level" is a different one
        // and survives beside it.
        Assert.Equal(2, filters.LanguageCriteria.Count);
        Assert.Equal(MultiValueMode.All, filters.LanguageMode);
    }

    [Fact]
    public void Empty_and_complete_status_selections_are_both_unrestricted()
    {
        var empty = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, [], null, null, null, null, null, null, null),
            "Filters");
        var complete = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, [.. CandidateStatuses.All], null, null, null, null, null, null, null),
            "Filters");

        Assert.True(empty.StatusIsUnrestricted);
        Assert.True(complete.StatusIsUnrestricted);
    }

    [Fact]
    public void Status_subset_is_kept_and_deduplicated()
    {
        var filters = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(
                null,
                [CandidateStatuses.Hired, " ", CandidateStatuses.Hired, CandidateStatuses.New],
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            "Filters");

        Assert.False(filters.StatusIsUnrestricted);
        Assert.Equal([CandidateStatuses.Hired, CandidateStatuses.New], filters.StatusValues);
    }

    [Theory]
    [InlineData("archived", SearchErrors.StatusInvalid)]
    [InlineData("NEW", SearchErrors.StatusInvalid)]
    public void Unsupported_status_is_refused_with_a_stable_code(string status, string code)
    {
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, [status], null, null, null, null, null, null, null),
            "Filters"));

        Assert.Contains(failure.Issues, issue => issue.Code == code);
    }

    [Theory]
    [InlineData("EITHER", SearchErrors.ModeInvalid)]
    [InlineData("any of them", SearchErrors.ModeInvalid)]
    public void Unsupported_mode_is_refused(string mode, string code)
    {
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, null, [new SearchCriterionInput("Java", "")], mode, null, null, null, null, null),
            "Filters"));

        Assert.Contains(failure.Issues, issue => issue.Code == code);
    }

    [Theory]
    [InlineData("", MultiValueMode.Any)]
    [InlineData("any", MultiValueMode.Any)]
    [InlineData("all", MultiValueMode.All)]
    [InlineData("ALL", MultiValueMode.All)]
    public void Mode_defaults_to_any_and_accepts_either_casing(string mode, MultiValueMode expected)
    {
        var filters = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, null, null, mode, null, null, null, null, null),
            "Filters");

        Assert.Equal(expected, filters.SkillMode);
    }

    [Theory]
    [InlineData("", CvPresence.Unset)]
    [InlineData("yes", CvPresence.Present)]
    [InlineData("no", CvPresence.Absent)]
    public void Cv_selection_accepts_the_documented_values(string value, CvPresence expected)
    {
        var filters = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, null, null, null, null, null, null, null, value),
            "Filters");

        Assert.Equal(expected, filters.HasCv);
    }

    [Fact]
    public void Unsupported_cv_selection_is_refused()
    {
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, null, null, null, null, null, null, null, "maybe"),
            "Filters"));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.CvInvalid);
    }

    [Fact]
    public void One_refusal_reports_every_problem_in_the_request()
    {
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, ["archived"], null, "SOME", null, null, null, null, "maybe"),
            "Filters"));

        Assert.Equal(3, failure.Issues.Count);
    }

    [Fact]
    public void Overlong_text_and_criteria_are_refused()
    {
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(
                new string('a', SearchFilterNormalization.MaximumTextLength + 1),
                null,
                [new SearchCriterionInput(new string('b', SearchFilterNormalization.MaximumCriterionLength + 1), "")],
                null,
                null,
                null,
                null,
                null,
                null),
            "Filters"));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.TextTooLong);
        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.CriterionTooLong);
    }

    [Fact]
    public void An_unbounded_criteria_list_is_refused()
    {
        var many = Enumerable
            .Range(0, SearchFilterNormalization.MaximumCriteriaPerFamily + 1)
            .Select(index => (SearchCriterionInput?)new SearchCriterionInput($"skill-{index}", ""))
            .ToArray();

        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterNormalization.Normalize(
            new SearchFiltersInput(null, null, many, "ALL", null, null, null, null, null),
            "Filters"));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.CriteriaTooMany);
    }

    [Theory]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("back\\slash", "%back\\\\slash%")]
    [InlineData("plain", "%plain%")]
    public void Wildcard_characters_are_escaped_into_literals(string text, string expected)
    {
        // Without this a search for "100%" would match every candidate whose notes start
        // with "100", and a search for "a_b" would match "axb".
        Assert.Equal(expected, SearchTextPattern.Contains(text));
    }

    [Fact]
    public void A_filter_value_round_trips_through_its_stored_document()
    {
        var original = SearchFilterNormalization.Normalize(
            new SearchFiltersInput(
                "Marta",
                [CandidateStatuses.Available],
                [new SearchCriterionInput("Java", "Avanzado")],
                "ALL",
                [new SearchCriterionInput("Inglés", "")],
                "ANY",
                [new SearchCriterionInput("Excel", "Alto")],
                "ALL",
                "yes"),
            "Filters");

        var stored = SearchFilterDocument.Serialize(original);
        var restored = SearchFilterDocument.Parse(stored);

        // Compared through the document rather than by record equality: the value's members
        // are collections, so record equality would compare references and pass for any two
        // instances that happen to share them.
        Assert.Equal(stored, SearchFilterDocument.Serialize(restored));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"version\":1,\"hasCv\":\"maybe\"}")]
    public void A_stored_filter_value_that_cannot_be_understood_is_refused(string stored)
    {
        // Never silently replaced by empty filters: that would turn someone's saved search
        // into "every candidate", which is the opposite of what they asked for.
        var failure = Assert.Throws<RequestValidationException>(() => SearchFilterDocument.Parse(stored));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.PresetFiltersInvalid);
    }

    [Fact]
    public void Absent_pagination_becomes_the_documented_defaults()
    {
        var issues = new List<ValidationIssue>();

        var (page, pageSize) = SearchPaging.Validate(null, null, issues);

        Assert.Empty(issues);
        Assert.Equal(1, page);
        Assert.Equal(25, pageSize);
    }

    [Theory]
    [InlineData(0, 25, SearchErrors.PageInvalid)]
    [InlineData(-1, 25, SearchErrors.PageInvalid)]
    [InlineData(1, 0, SearchErrors.PageSizeInvalid)]
    [InlineData(1, 101, SearchErrors.PageSizeInvalid)]
    public void Pagination_outside_its_bounds_is_refused(int page, int pageSize, string code)
    {
        var issues = new List<ValidationIssue>();

        SearchPaging.Validate(page, pageSize, issues);

        Assert.Contains(issues, issue => issue.Code == code);
    }

    [Fact]
    public void The_maximum_page_size_is_accepted()
    {
        var issues = new List<ValidationIssue>();

        var (_, pageSize) = SearchPaging.Validate(1, SearchPaging.MaximumPageSize, issues);

        Assert.Empty(issues);
        Assert.Equal(100, pageSize);
    }
}
