namespace KeplerTalento.Domain.Catalogs;

/// <summary>
/// The closed set of business catalog families. These map one-to-one onto the
/// <c>CatalogFamily</c> union in the frontend and onto candidate relation kinds.
/// </summary>
public static class CatalogFamilies
{
    public const string Language = "language";
    public const string Program = "program";
    public const string Skill = "skill";
    public const string LanguageLevel = "language_level";
    public const string ProgramLevel = "program_level";
    public const string SkillLevel = "skill_level";
    public const string EducationType = "education_type";
    public const string EducationStatus = "education_status";
    public const string Sector = "sector";

    public static readonly IReadOnlyList<string> All =
    [
        Language,
        Program,
        Skill,
        LanguageLevel,
        ProgramLevel,
        SkillLevel,
        EducationType,
        EducationStatus,
        Sector,
    ];

    public static bool IsKnown(string? family) =>
        family is not null && All.Contains(family, StringComparer.Ordinal);
}
