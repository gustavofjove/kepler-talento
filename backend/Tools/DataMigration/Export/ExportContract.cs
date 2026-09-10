namespace KeplerTalento.Tools.DataMigration.Export;

/// <summary>
/// The export set described by <c>docs/ktl-7/access-export-procedure.md</c>. This type is
/// the executable copy of that document; if the two disagree the document is wrong.
/// </summary>
public static class ExportContract
{
    public const string Candidates = "candidates.csv";
    public const string Languages = "languages.csv";
    public const string Programs = "programs.csv";
    public const string Education = "education.csv";
    public const string Experience = "experience.csv";
    public const string Skills = "skills.csv";
    public const string Documents = "documents.csv";

    public const string SourceKeyColumn = "SourceKey";
    public const string CandidateSourceKeyColumn = "CandidateSourceKey";
    public const string FilesDirectory = "files";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Columns =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Candidates] =
            [
                "SourceKey", "FirstName", "LastName", "Phone", "Email", "Location", "Province",
                "Country", "Availability", "Status", "Source", "Notes", "ReceivedAt", "ConsentAt",
                "ReviewDueAt", "IsActive", "DeletedAt",
            ],
            [Languages] =
            [
                "SourceKey", "CandidateSourceKey", "Language", "Level", "Certification", "Notes",
            ],
            [Programs] =
            [
                "SourceKey", "CandidateSourceKey", "Program", "Level", "YearsExperience", "Notes",
            ],
            [Education] =
            [
                "SourceKey", "CandidateSourceKey", "EducationType", "Degree", "Specialty",
                "Institution", "Status", "EndYear", "Notes",
            ],
            [Experience] =
            [
                "SourceKey", "CandidateSourceKey", "Company", "Position", "Sector", "Functions",
                "StartDate", "EndDate", "YearsExperience", "IsCurrent", "Notes",
            ],
            [Skills] =
            [
                "SourceKey", "CandidateSourceKey", "Skill", "Level", "Notes",
            ],
            [Documents] =
            [
                "SourceKey", "CandidateSourceKey", "RelativePath", "DocumentType",
                "OriginalFileName", "ContentType", "Sha256", "IsPrimary",
            ],
        };

    public static IReadOnlyList<string> Files => [.. Columns.Keys];

    /// <summary>Files whose rows belong to a candidate.</summary>
    public static IReadOnlyList<string> ChildFiles =>
        [Languages, Programs, Education, Experience, Skills, Documents];
}
