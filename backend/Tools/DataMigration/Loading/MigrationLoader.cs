using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Resolution;
using KeplerTalento.Tools.DataMigration.Validation;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Tools.DataMigration.Loading;

public sealed record LoadOptions(bool OverwriteApplicationEdits, DateTimeOffset LoadedAtUtc)
{
    /// <summary>Validate and resolve, but write nothing.</summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// Loads a validated export into PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// A candidate and its relation rows are written in one transaction, on a context of their
/// own, so a failure anywhere in the aggregate leaves none of it behind.
/// </para>
/// <para>
/// A problem in a relation row rejects the whole candidate. Loading the person while
/// silently dropping one of their languages would lose data the migration exists to
/// preserve, and the operator cannot see what they were not told about. Documents are the
/// deliberate exception: a document is content attached to the person, not a field of them,
/// so a corrupt or infected file fails closed on its own without rejecting the candidate.
/// </para>
/// </remarks>
public sealed class MigrationLoader(
    Func<ApplicationDbContext> contextFactory,
    CatalogResolver resolver)
{
    private static readonly IReadOnlyDictionary<string, string> RelationFileEntities =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExportContract.Languages] = MigrationEntities.Language,
            [ExportContract.Programs] = MigrationEntities.Program,
            [ExportContract.Education] = MigrationEntities.Education,
            [ExportContract.Experience] = MigrationEntities.Experience,
            [ExportContract.Skills] = MigrationEntities.Skill,
        };

    public async Task<LoadResult> LoadAsync(
        ExportSet exportSet,
        LoadOptions options,
        CancellationToken cancellationToken)
    {
        var result = new LoadResult();

        // 1. Everything wrong with the data, in one pass, before anything is written.
        foreach (var problem in new RowValidator().Validate(exportSet))
        {
            result.AddProblem(problem);
        }

        // 2. Resolve every catalog reference. A row whose reference resolves to nothing is
        //    rejected, and so is its candidate.
        var rejectedCandidates = result.Problems
            .Where(problem => problem.Entity == MigrationEntities.Candidate)
            .Select(problem => problem.SourceKey)
            .ToHashSet(StringComparer.Ordinal);
        var relations = ResolveRelations(exportSet, result, rejectedCandidates);
        result.UnresolvedValues = resolver.UnresolvedValues;
        result.BrokenMappings = resolver.BrokenMappings;

        // 3. Load each surviving candidate as one aggregate.
        foreach (var row in exportSet[ExportContract.Candidates].Rows)
        {
            var sourceKey = row[ExportContract.SourceKeyColumn].Trim();
            if (rejectedCandidates.Contains(sourceKey))
            {
                continue;
            }
            var outcome = await LoadCandidateAsync(
                row,
                relations.TryGetValue(sourceKey, out var owned) ? owned : [],
                options,
                result,
                cancellationToken);
            result.Record(MigrationEntities.Candidate, sourceKey, outcome);
            foreach (var relation in relations.GetValueOrDefault(sourceKey, []))
            {
                result.Record(relation.Entity, relation.SourceKey, outcome);
            }
        }

        await RecordUnmatchedAsync(exportSet, result, cancellationToken);
        return result;
    }

    /// <summary>
    /// The database identifiers of the candidates this run loaded, keyed by source key, so
    /// the document phase knows what to attach its files to.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, Guid>> LoadedCandidateIdsAsync(
        LoadResult result,
        CancellationToken cancellationToken)
    {
        var loaded = result.SourceKeys(MigrationEntities.Candidate, RowOutcome.Loaded)
            .ToHashSet(StringComparer.Ordinal);
        await using var dbContext = contextFactory();
        return await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.SourceKey != null && loaded.Contains(candidate.SourceKey))
            .ToDictionaryAsync(candidate => candidate.SourceKey!, candidate => candidate.Id, cancellationToken);
    }

    private sealed record ResolvedRelation(
        string Entity,
        string SourceKey,
        CsvRow Row,
        Guid PrimaryCatalogId,
        Guid? SecondaryCatalogId);

    private Dictionary<string, List<ResolvedRelation>> ResolveRelations(
        ExportSet exportSet,
        LoadResult result,
        HashSet<string> rejectedCandidates)
    {
        var byCandidate = new Dictionary<string, List<ResolvedRelation>>(StringComparer.Ordinal);

        foreach (var (file, entity) in RelationFileEntities)
        {
            foreach (var row in exportSet[file].Rows)
            {
                var sourceKey = row[ExportContract.SourceKeyColumn].Trim();
                var candidateKey = row[ExportContract.CandidateSourceKeyColumn].Trim();
                if (rejectedCandidates.Contains(candidateKey))
                {
                    continue;
                }

                var (primaryFamily, primaryColumn, secondaryFamily, secondaryColumn) = ReferencesOf(file);
                var primary = resolver.Resolve(primaryFamily, row[primaryColumn]);
                // Experience references only a sector; the others carry a level as well.
                var secondary = secondaryColumn is null
                    ? (CatalogResolution?)null
                    : resolver.Resolve(secondaryFamily!, row[secondaryColumn]);

                if (!primary.Resolved || secondary is { Resolved: false })
                {
                    var failing = primary.Resolved ? secondaryColumn! : primaryColumn;
                    result.AddProblem(new RowProblem(entity, sourceKey, failing, ReasonCodes.ReferenceUnresolved));
                    // The aggregate is transactional: loading the candidate without this
                    // relation would drop data without telling anyone.
                    rejectedCandidates.Add(candidateKey);
                    result.AddProblem(new RowProblem(
                        MigrationEntities.Candidate,
                        candidateKey,
                        failing,
                        ReasonCodes.ReferenceUnresolved));
                    continue;
                }

                if (!byCandidate.TryGetValue(candidateKey, out var owned))
                {
                    owned = [];
                    byCandidate[candidateKey] = owned;
                }
                owned.Add(new ResolvedRelation(
                    entity,
                    sourceKey,
                    row,
                    primary.CatalogItemId!.Value,
                    secondary?.CatalogItemId));
            }
        }

        // A candidate rejected by a later relation may already have earlier ones collected.
        foreach (var rejected in rejectedCandidates)
        {
            if (byCandidate.Remove(rejected, out var orphaned))
            {
                foreach (var relation in orphaned)
                {
                    result.AddProblem(new RowProblem(
                        relation.Entity,
                        relation.SourceKey,
                        ExportContract.CandidateSourceKeyColumn,
                        ReasonCodes.CandidateRejected));
                }
            }
        }

        return byCandidate;
    }

    private static (string Primary, string PrimaryColumn, string? Secondary, string? SecondaryColumn)
        ReferencesOf(string file) => file switch
        {
            ExportContract.Languages =>
                (CatalogFamilies.Language, "Language", CatalogFamilies.LanguageLevel, "Level"),
            ExportContract.Programs =>
                (CatalogFamilies.Program, "Program", CatalogFamilies.ProgramLevel, "Level"),
            ExportContract.Skills =>
                (CatalogFamilies.Skill, "Skill", CatalogFamilies.SkillLevel, "Level"),
            ExportContract.Education =>
                (CatalogFamilies.EducationType, "EducationType", CatalogFamilies.EducationStatus, "Status"),
            ExportContract.Experience =>
                (CatalogFamilies.Sector, "Sector", null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(file)),
        };

    private async Task<RowOutcome> LoadCandidateAsync(
        CsvRow row,
        IReadOnlyList<ResolvedRelation> relations,
        LoadOptions options,
        LoadResult result,
        CancellationToken cancellationToken)
    {
        var sourceKey = row[ExportContract.SourceKeyColumn].Trim();

        await using var dbContext = contextFactory();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var candidate = await dbContext.Candidates
            .SingleOrDefaultAsync(value => value.SourceKey == sourceKey, cancellationToken);

        if (candidate is not null
            && candidate.HasApplicationChangesSinceLoad
            && !options.OverwriteApplicationEdits)
        {
            // A newer export must not undo work done in the application.
            result.AddProblemWithoutRejecting(new RowProblem(
                MigrationEntities.Candidate,
                sourceKey,
                nameof(Candidate.UpdatedAtUtc),
                ReasonCodes.ApplicationChanged));
            await transaction.RollbackAsync(cancellationToken);
            return RowOutcome.Skipped;
        }

        if (candidate is not null && candidate.HasApplicationChangesSinceLoad)
        {
            result.OverwrittenApplicationEdits++;
        }

        try
        {
            return await WriteAggregateAsync(dbContext, transaction, candidate, row, relations, options, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The aggregate is transactional, so nothing partial survives. The constraint
            // name is the most detail that may be recorded: the driver's message can quote
            // parameter values, which for this table are candidate personal data.
            await transaction.RollbackAsync(cancellationToken);
            result.AddProblem(new RowProblem(
                MigrationEntities.Candidate,
                sourceKey,
                ConstraintNameOf(exception) ?? "aggregate",
                ReasonCodes.LoadFailed));
            return RowOutcome.Rejected;
        }
    }

    private static string? ConstraintNameOf(DbUpdateException exception) =>
        (exception.InnerException as Npgsql.PostgresException)?.ConstraintName;

    private static async Task<RowOutcome> WriteAggregateAsync(
        ApplicationDbContext dbContext,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Candidate? candidate,
        CsvRow row,
        IReadOnlyList<ResolvedRelation> relations,
        LoadOptions options,
        CancellationToken cancellationToken)
    {
        var sourceKey = row[ExportContract.SourceKeyColumn].Trim();
        var loadedAtUtc = options.LoadedAtUtc;
        if (candidate is null)
        {
            candidate = new Candidate(
                Guid.CreateVersion7(),
                row["FirstName"].Trim(),
                row["LastName"].Trim(),
                loadedAtUtc);
            candidate.SetSourceKey(sourceKey);
            dbContext.Candidates.Add(candidate);
        }
        else
        {
            candidate.SetIdentity(row["FirstName"], row["LastName"], loadedAtUtc);
        }

        candidate.SetDetails(
            row["Phone"],
            row["Email"],
            row["Location"],
            row["Province"],
            row["Country"],
            row["Availability"],
            row["Status"].Trim(),
            row["Source"],
            row["Notes"],
            loadedAtUtc);

        // Consent and retention metadata is carried across exactly. Validation has already
        // rejected any row whose consent date could not be established, so nothing here
        // substitutes a value for an absent one.
        FieldParsers.TryDate(row["ReceivedAt"], out var receivedAt);
        FieldParsers.TryDate(row["ConsentAt"], out var consentAt);
        FieldParsers.TryDate(row["ReviewDueAt"], out var reviewDueAt);
        candidate.SetConsent(receivedAt, consentAt, reviewDueAt, loadedAtUtc);

        // Logical removal in the source arrives as logical state, not as absence.
        FieldParsers.TryBoolean(row["IsActive"], out var isActive);
        if (isActive)
        {
            candidate.Reactivate(loadedAtUtc);
        }
        else
        {
            FieldParsers.TryDate(row["DeletedAt"], out var deletedAt);
            candidate.Deactivate(
                deletedAt is null
                    ? loadedAtUtc
                    : new DateTimeOffset(deletedAt.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        await ReplaceRelationsAsync(dbContext, candidate.Id, relations, cancellationToken);

        // Stamped last, so UpdatedAtUtc and SourceLoadedAtUtc agree and any later
        // application write moves the first past the second.
        candidate.MarkSourceLoaded(loadedAtUtc);

        if (options.DryRun)
        {
            await transaction.RollbackAsync(cancellationToken);
            return RowOutcome.Loaded;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RowOutcome.Loaded;
    }

    /// <summary>
    /// Replaces the candidate's migrated relation rows as a whole set, leaving any row the
    /// application created — which carries no source key — untouched.
    /// </summary>
    private static async Task ReplaceRelationsAsync(
        ApplicationDbContext dbContext,
        Guid candidateId,
        IReadOnlyList<ResolvedRelation> relations,
        CancellationToken cancellationToken)
    {
        dbContext.CandidateLanguages.RemoveRange(await dbContext.CandidateLanguages
            .Where(value => value.CandidateId == candidateId && value.SourceKey != null)
            .ToListAsync(cancellationToken));
        dbContext.CandidatePrograms.RemoveRange(await dbContext.CandidatePrograms
            .Where(value => value.CandidateId == candidateId && value.SourceKey != null)
            .ToListAsync(cancellationToken));
        dbContext.CandidateEducation.RemoveRange(await dbContext.CandidateEducation
            .Where(value => value.CandidateId == candidateId && value.SourceKey != null)
            .ToListAsync(cancellationToken));
        dbContext.CandidateExperience.RemoveRange(await dbContext.CandidateExperience
            .Where(value => value.CandidateId == candidateId && value.SourceKey != null)
            .ToListAsync(cancellationToken));
        dbContext.CandidateSkills.RemoveRange(await dbContext.CandidateSkills
            .Where(value => value.CandidateId == candidateId && value.SourceKey != null)
            .ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var relation in relations)
        {
            AddRelation(dbContext, candidateId, relation);
        }
    }

    private static void AddRelation(
        ApplicationDbContext dbContext,
        Guid candidateId,
        ResolvedRelation relation)
    {
        var row = relation.Row;
        var id = Guid.CreateVersion7();
        switch (relation.Entity)
        {
            case MigrationEntities.Language:
            {
                var language = new CandidateLanguage(
                    id, candidateId, relation.PrimaryCatalogId, relation.SecondaryCatalogId!.Value);
                language.SetCertification(row["Certification"]);
                Finish(language, row, relation);
                dbContext.CandidateLanguages.Add(language);
                break;
            }
            case MigrationEntities.Program:
            {
                var program = new CandidateProgram(
                    id, candidateId, relation.PrimaryCatalogId, relation.SecondaryCatalogId!.Value);
                FieldParsers.TryInteger(row["YearsExperience"], out var years);
                program.SetYearsExperience(years);
                Finish(program, row, relation);
                dbContext.CandidatePrograms.Add(program);
                break;
            }
            case MigrationEntities.Skill:
            {
                var skill = new CandidateSkill(
                    id, candidateId, relation.PrimaryCatalogId, relation.SecondaryCatalogId!.Value);
                Finish(skill, row, relation);
                dbContext.CandidateSkills.Add(skill);
                break;
            }
            case MigrationEntities.Education:
            {
                var education = new CandidateEducation(
                    id,
                    candidateId,
                    relation.PrimaryCatalogId,
                    relation.SecondaryCatalogId!.Value,
                    row["Degree"],
                    row["Institution"]);
                education.SetSpecialty(row["Specialty"]);
                FieldParsers.TryInteger(row["EndYear"], out var endYear);
                education.SetEndYear(endYear);
                Finish(education, row, relation);
                dbContext.CandidateEducation.Add(education);
                break;
            }
            case MigrationEntities.Experience:
            {
                var experience = new CandidateExperience(
                    id, candidateId, relation.PrimaryCatalogId, row["Company"], row["Position"]);
                experience.SetFunctions(row["Functions"]);
                FieldParsers.TryDate(row["StartDate"], out var startDate);
                FieldParsers.TryDate(row["EndDate"], out var endDate);
                FieldParsers.TryBoolean(row["IsCurrent"], out var isCurrent);
                experience.SetPeriod(startDate, endDate, isCurrent);
                FieldParsers.TryInteger(row["YearsExperience"], out var years);
                experience.SetYearsExperience(years);
                Finish(experience, row, relation);
                dbContext.CandidateExperience.Add(experience);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(relation));
        }
    }

    private static void Finish(CandidateRelation relation, CsvRow row, ResolvedRelation resolved)
    {
        relation.SetNotes(row["Notes"]);
        relation.SetSourceKey(resolved.SourceKey);
    }

    /// <summary>
    /// Records target rows carrying a source key the export does not contain. Nothing is
    /// modified: see <see cref="UnmatchedTargetRecord"/>.
    /// </summary>
    private async Task RecordUnmatchedAsync(
        ExportSet exportSet,
        LoadResult result,
        CancellationToken cancellationToken)
    {
        var exported = exportSet[ExportContract.Candidates].Rows
            .Select(row => row[ExportContract.SourceKeyColumn].Trim())
            .ToHashSet(StringComparer.Ordinal);

        await using var dbContext = contextFactory();
        var stored = await dbContext.Candidates
            .AsNoTracking()
            .Where(candidate => candidate.SourceKey != null)
            .Select(candidate => candidate.SourceKey!)
            .ToListAsync(cancellationToken);

        foreach (var sourceKey in stored.Where(key => !exported.Contains(key)))
        {
            result.AddUnmatched(new UnmatchedTargetRecord(MigrationEntities.Candidate, sourceKey));
        }
    }
}
