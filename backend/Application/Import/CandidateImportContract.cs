namespace KeplerTalento.Application.Import;

/// <summary>
/// The documented candidate import file contract (<c>docs/ktl-17/import-file-contract.md</c>).
/// Column names are matched after trimming and lower-casing the header.
/// </summary>
public static class CandidateImportContract
{
    public const string Entity = "candidate";

    public const string FirstName = "first_name";
    public const string LastName = "last_name";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Location = "location";
    public const string Province = "province";
    public const string Country = "country";
    public const string Availability = "availability";
    public const string Status = "status";
    public const string Source = "source";
    public const string Notes = "notes";
    public const string ReceivedAt = "received_at";
    public const string ConsentAt = "consent_at";
    public const string ReviewDueAt = "review_due_at";

    /// <summary>
    /// <c>Idioma:Nivel</c> pairs separated by <c>;</c>, both resolved against the language and
    /// language-level catalogs. The catalog-backed column of the contract.
    /// </summary>
    public const string Languages = "languages";

    public const string AcceptedContentType = "text/csv";
    public const string AcceptedExtension = ".csv";

    /// <summary>The starting limit, carried over from the browser stub and now enforced here.</summary>
    public const int DefaultMaximumRows = 2000;

    public static readonly IReadOnlyList<string> RequiredColumns = [FirstName, LastName, Email];

    public static readonly IReadOnlyList<string> KnownColumns =
    [
        FirstName,
        LastName,
        Email,
        Phone,
        Location,
        Province,
        Country,
        Availability,
        Status,
        Source,
        Notes,
        ReceivedAt,
        ConsentAt,
        ReviewDueAt,
        Languages,
    ];

    /// <summary>
    /// The candidate column bounds, matching <c>CND_Candidates</c>. A value over its bound is a
    /// row rejection rather than a database error halfway through a commit.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> MaximumLengths = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [FirstName] = 120,
        [LastName] = 180,
        [Phone] = 40,
        [Email] = 255,
        [Location] = 160,
        [Province] = 120,
        [Country] = 120,
        [Availability] = 120,
        [Source] = 120,
        [Notes] = 4000,
        [Languages] = 1000,
    };
}
