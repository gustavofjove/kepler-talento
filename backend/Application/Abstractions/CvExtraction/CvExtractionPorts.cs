namespace KeplerTalento.Application.Abstractions.CvExtraction;

/// <summary>The CV formats a draft can be extracted from (KTL-32).</summary>
public enum CvFileKind
{
    Pdf,
    Docx,
}

/// <summary>One line of CV text in reading order.</summary>
/// <param name="Page">Zero-based page; a DOCX has a single logical page.</param>
/// <param name="FontSize">Largest point size on the line, when the format records one.</param>
public sealed record CvLine(int Page, string Text, double? FontSize = null);

/// <summary>
/// The text of a CV, bounded by <see cref="CvReadBounds"/>. Personal data: it lives for one
/// request only and is never logged, stored or audited.
/// </summary>
public sealed record CvText(IReadOnlyList<CvLine> Lines, int PageCount)
{
    public static CvText Empty { get; } = new([], 0);

    public bool HasText => Lines.Any(line => line.Text.Any(char.IsLetter));
}

public sealed record CvReadBounds(int MaximumPages, int MaximumCharacters);

/// <summary>
/// The content is malformed, encrypted or otherwise cannot be read. Carries no message from the
/// parser, whose messages can quote document content.
/// </summary>
public sealed class CvUnreadableException() : Exception("The CV could not be read.");

public interface ICvTextReader
{
    /// <summary>Reads the text of a CV that has already been scanned clean.</summary>
    /// <exception cref="CvUnreadableException">The content cannot be parsed.</exception>
    Task<CvText> ReadAsync(CvFileKind kind, Stream content, CvReadBounds bounds, CancellationToken cancellationToken);
}

public enum SuggestionConfidence
{
    High,
    Low,
}

public sealed record FieldSuggestion(string Value, SuggestionConfidence Confidence);

/// <summary>Suggested values for the create form; a null member means "no suggestion".</summary>
public sealed record CandidateDraftSuggestions(
    FieldSuggestion? FirstName = null,
    FieldSuggestion? LastName = null,
    FieldSuggestion? Email = null,
    FieldSuggestion? Phone = null,
    FieldSuggestion? Location = null,
    FieldSuggestion? Province = null)
{
    public static CandidateDraftSuggestions None { get; } = new();
}

/// <summary>Deterministic, local, I/O-free rules that turn CV text into suggestions.</summary>
public interface ICandidateDraftExtractor
{
    CandidateDraftSuggestions Extract(CvText text);
}

/// <summary>Bounds on CV draft extraction (KTL-32 design D3). Section <c>CvDraft</c>.</summary>
public sealed class CvDraftOptions
{
    public const string SectionName = "CvDraft";

    public int MaxConcurrent { get; init; } = 4;

    public int MaxPdfPages { get; init; } = 5;

    public int MaxCharacters { get; init; } = 50_000;

    /// <summary>Budget for reading and extraction; the scanner keeps its own timeout.</summary>
    public int TimeBudgetSeconds { get; init; } = 20;

    public CvReadBounds Bounds => new(MaxPdfPages, MaxCharacters);

    public void Validate()
    {
        if (MaxConcurrent is < 1 or > 32
            || MaxPdfPages is < 1 or > 50
            || MaxCharacters is < 1_000 or > 500_000
            || TimeBudgetSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException("CvDraft configuration is unsafe.");
        }
    }
}
