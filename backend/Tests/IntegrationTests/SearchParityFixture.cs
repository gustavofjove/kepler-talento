using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Persistence;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>One relation as the reference evaluator sees it: a value and its level.</summary>
public sealed record ParityRelation(string Value, string Level);

/// <summary>
/// A fixture candidate, in the shape the pre-KTL-10 browser evaluator worked on. This is the
/// second opinion the PostgreSQL query is checked against, so it deliberately mirrors what
/// the old code had in memory rather than what the schema stores.
/// </summary>
public sealed record ParityCandidate(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    string Notes,
    string Status,
    bool IsActive,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ParityRelation> Skills,
    IReadOnlyList<ParityRelation> Languages,
    IReadOnlyList<ParityRelation> Programs,
    Guid? PrimaryDocumentId,
    DocumentScanState? PrimaryScanState);

/// <summary>
/// The shared dataset every parity assertion is made against.
/// </summary>
/// <remarks>
/// It is built to break a naive implementation rather than to look realistic. It contains
/// every candidate status, logically removed candidates that satisfy the filters anyway,
/// candidates holding the same value at two levels (which a joined query would return
/// twice), <c>ALL</c> matches that can only be satisfied across separate relation rows,
/// primary documents in the pending, clean and refused scan states, a candidate whose only
/// documents are non-primary, and text containing SQL wildcard characters.
/// </remarks>
public static class SearchParityFixture
{
    public const string SkillJava = "Java";
    public const string SkillPython = "Python";
    public const string SkillSql = "SQL";
    public const string SkillLevelBasic = "Básico";
    public const string SkillLevelAdvanced = "Avanzado";

    public const string LanguageEnglish = "Inglés";
    public const string LanguageFrench = "Francés";
    public const string LanguageLevelB2 = "B2";
    public const string LanguageLevelC1 = "C1";

    public const string ProgramExcel = "Excel";
    public const string ProgramAutoCad = "AutoCAD";
    public const string ProgramLevelMedium = "Medio";
    public const string ProgramLevelHigh = "Alto";

    /// <summary>
    /// A candidate whose notes contain <c>%</c> and <c>_</c>, so a query that forgets to
    /// escape the pattern matches candidates it should not.
    /// </summary>
    public const string WildcardNote = "Descuento 100% _ disponible";

    private static readonly DateTimeOffset Origin = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Writes the fixture and returns the same data as the reference evaluator's input. The
    /// two cannot drift, because there is only one description of the dataset.
    /// </summary>
    public static async Task<IReadOnlyList<ParityCandidate>> SeedAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var catalog = await SeedCatalogAsync(dbContext, cancellationToken);
        var built = new List<ParityCandidate>();

        // 1. Two levels of the same skill. A join-and-DISTINCT query returns this candidate
        //    twice for a value-only criterion; a correlated EXISTS cannot.
        built.Add(Add(dbContext, catalog, "Ana", "Duplicada", CandidateStatuses.Available, index: 1,
            skills: [new(SkillJava, SkillLevelBasic), new(SkillJava, SkillLevelAdvanced)],
            languages: [new(LanguageEnglish, LanguageLevelB2)],
            programs: [],
            primary: DocumentScanState.Clean));

        // 2. ALL across separate relation rows: Java and Python are never in one row.
        built.Add(Add(dbContext, catalog, "Bruno", "Completo", CandidateStatuses.InProcess, index: 2,
            skills: [new(SkillJava, SkillLevelAdvanced), new(SkillPython, SkillLevelBasic)],
            languages: [new(LanguageEnglish, LanguageLevelC1), new(LanguageFrench, LanguageLevelB2)],
            programs: [new(ProgramExcel, ProgramLevelHigh)],
            primary: DocumentScanState.PendingScan));

        // 3. Holds one of the ALL pair only, so an ALL search must exclude it while an ANY
        //    search must not.
        built.Add(Add(dbContext, catalog, "Carla", "Parcial", CandidateStatuses.New, index: 3,
            skills: [new(SkillJava, SkillLevelBasic)],
            languages: [new(LanguageFrench, LanguageLevelB2)],
            programs: [new(ProgramAutoCad, ProgramLevelMedium)],
            primary: DocumentScanState.Infected));

        // 4. A refused primary document still counts as having a CV: hasCv is about the
        //    record, and downloadability is KTL-9's decision, not search's.
        built.Add(Add(dbContext, catalog, "Diego", "Rechazado", CandidateStatuses.Hired, index: 4,
            skills: [new(SkillSql, SkillLevelAdvanced)],
            languages: [],
            programs: [new(ProgramExcel, ProgramLevelMedium)],
            primary: DocumentScanState.ScanFailed));

        // 5. Documents but none primary: hasCv=no must find it.
        built.Add(Add(dbContext, catalog, "Elena", "SinPrincipal", CandidateStatuses.Rejected, index: 5,
            skills: [new(SkillPython, SkillLevelAdvanced)],
            languages: [new(LanguageEnglish, LanguageLevelB2)],
            programs: [],
            primary: null,
            secondaryDocuments: 2));

        // 6. No documents at all.
        built.Add(Add(dbContext, catalog, "Fermín", "SinDocumentos", CandidateStatuses.Available, index: 6,
            skills: [],
            languages: [new(LanguageEnglish, LanguageLevelC1)],
            programs: [new(ProgramAutoCad, ProgramLevelHigh)],
            primary: null));

        // 7. Logically removed, and otherwise a match for almost every filter. It must never
        //    appear on a page nor be counted.
        built.Add(Add(dbContext, catalog, "Gabriel", "Eliminado", CandidateStatuses.Available, index: 7,
            skills: [new(SkillJava, SkillLevelAdvanced), new(SkillPython, SkillLevelAdvanced)],
            languages: [new(LanguageEnglish, LanguageLevelC1)],
            programs: [new(ProgramExcel, ProgramLevelHigh)],
            primary: DocumentScanState.Clean,
            isActive: false));

        // 8. Wildcard characters in the notes.
        built.Add(Add(dbContext, catalog, "Helena", "Comodín", CandidateStatuses.New, index: 8,
            skills: [],
            languages: [],
            programs: [],
            primary: DocumentScanState.Clean,
            notes: WildcardNote));

        // 9. Shares candidate 8's update instant, so the identifier tie-breaker is exercised
        //    rather than assumed.
        built.Add(Add(dbContext, catalog, "Iván", "Empatado", CandidateStatuses.New, index: 8,
            skills: [new(SkillSql, SkillLevelBasic)],
            languages: [],
            programs: [],
            primary: null));

        await dbContext.SaveChangesAsync(cancellationToken);
        return built;
    }

    private static ParityCandidate Add(
        ApplicationDbContext dbContext,
        IReadOnlyDictionary<(string Family, string Name), Guid> catalog,
        string firstName,
        string lastName,
        string status,
        int index,
        IReadOnlyList<ParityRelation> skills,
        IReadOnlyList<ParityRelation> languages,
        IReadOnlyList<ParityRelation> programs,
        DocumentScanState? primary,
        bool isActive = true,
        string? notes = null,
        int secondaryDocuments = 0)
    {
        var id = Guid.CreateVersion7();
        var updatedAt = Origin.AddMinutes(index);
        var candidate = new Candidate(id, firstName, lastName, Origin);
        candidate.SetDetails(
            phone: $"+34 600 000 {index:000}",
            email: $"{firstName.ToLowerInvariant()}.{index}@ejemplo.test",
            location: "Madrid",
            province: "Madrid",
            country: "España",
            availability: "Inmediata",
            status: status,
            source: "fixture",
            notes: notes ?? $"Perfil de prueba {index}",
            updatedAtUtc: updatedAt);
        dbContext.Candidates.Add(candidate);

        foreach (var skill in skills)
        {
            var relation = new CandidateSkill(
                Guid.CreateVersion7(),
                id,
                catalog[(CatalogFamilies.Skill, skill.Value)],
                catalog[(CatalogFamilies.SkillLevel, skill.Level)]);
            dbContext.CandidateSkills.Add(relation);
        }
        foreach (var language in languages)
        {
            dbContext.CandidateLanguages.Add(new CandidateLanguage(
                Guid.CreateVersion7(),
                id,
                catalog[(CatalogFamilies.Language, language.Value)],
                catalog[(CatalogFamilies.LanguageLevel, language.Level)]));
        }
        foreach (var program in programs)
        {
            dbContext.CandidatePrograms.Add(new CandidateProgram(
                Guid.CreateVersion7(),
                id,
                catalog[(CatalogFamilies.Program, program.Value)],
                catalog[(CatalogFamilies.ProgramLevel, program.Level)]));
        }

        Guid? primaryDocumentId = null;
        if (primary is not null)
        {
            primaryDocumentId = AddDocument(dbContext, id, isPrimary: true, primary.Value, Origin);
        }
        for (var extra = 0; extra < secondaryDocuments; extra++)
        {
            AddDocument(dbContext, id, isPrimary: false, DocumentScanState.Clean, Origin);
        }

        if (!isActive)
        {
            candidate.Deactivate(updatedAt);
        }

        return new ParityCandidate(
            id,
            candidate.FirstName,
            candidate.LastName,
            candidate.Phone,
            candidate.Email,
            candidate.Notes,
            candidate.Status,
            candidate.IsActive,
            candidate.UpdatedAtUtc,
            skills,
            languages,
            programs,
            primaryDocumentId,
            primary);
    }

    private static Guid AddDocument(
        ApplicationDbContext dbContext,
        Guid candidateId,
        bool isPrimary,
        DocumentScanState scanState,
        DateTimeOffset createdAtUtc)
    {
        var documentId = Guid.CreateVersion7();
        var document = new CandidateDocument(
            documentId,
            candidateId,
            $"{candidateId:N}/{documentId:N}.bin",
            "cv.pdf",
            "application/pdf",
            size: 1024,
            sha256: new string('a', 64),
            createdAtUtc: createdAtUtc);
        document.SetDocumentType("cv");
        document.SetPrimary(isPrimary, createdAtUtc);
        switch (scanState)
        {
            case DocumentScanState.Clean:
                document.MarkClean("fixture", createdAtUtc);
                break;
            case DocumentScanState.PendingScan:
                break;
            default:
                document.MarkUnavailable(scanState, "fixture.refused", createdAtUtc);
                break;
        }
        dbContext.Documents.Add(document);
        return documentId;
    }

    private static async Task<IReadOnlyDictionary<(string, string), Guid>> SeedCatalogAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var wanted = new (string Family, string Name)[]
        {
            (CatalogFamilies.Skill, SkillJava),
            (CatalogFamilies.Skill, SkillPython),
            (CatalogFamilies.Skill, SkillSql),
            (CatalogFamilies.SkillLevel, SkillLevelBasic),
            (CatalogFamilies.SkillLevel, SkillLevelAdvanced),
            (CatalogFamilies.Language, LanguageEnglish),
            (CatalogFamilies.Language, LanguageFrench),
            (CatalogFamilies.LanguageLevel, LanguageLevelB2),
            (CatalogFamilies.LanguageLevel, LanguageLevelC1),
            (CatalogFamilies.Program, ProgramExcel),
            (CatalogFamilies.Program, ProgramAutoCad),
            (CatalogFamilies.ProgramLevel, ProgramLevelMedium),
            (CatalogFamilies.ProgramLevel, ProgramLevelHigh),
        };
        var resolved = new Dictionary<(string, string), Guid>();
        var sortOrder = 1;
        foreach (var (family, name) in wanted)
        {
            var normalized = CatalogName.Normalize(name);
            var existing = dbContext.CatalogItems.FirstOrDefault(item =>
                item.Family == family && item.NameNormalized == normalized);
            if (existing is not null)
            {
                resolved[(family, name)] = existing.Id;
                continue;
            }
            var item = new CatalogItem(
                Guid.CreateVersion7(),
                family,
                CatalogName.DeriveCode(name),
                name,
                nameEn: null,
                sortOrder: sortOrder++,
                createdAtUtc: Origin);
            dbContext.CatalogItems.Add(item);
            resolved[(family, name)] = item.Id;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return resolved;
    }
}
