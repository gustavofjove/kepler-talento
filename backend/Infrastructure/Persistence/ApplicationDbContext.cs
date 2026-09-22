using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Domain.Search;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateLanguage> CandidateLanguages => Set<CandidateLanguage>();
    public DbSet<CandidateProgram> CandidatePrograms => Set<CandidateProgram>();
    public DbSet<CandidateEducation> CandidateEducation => Set<CandidateEducation>();
    public DbSet<CandidateExperience> CandidateExperience => Set<CandidateExperience>();
    public DbSet<CandidateSkill> CandidateSkills => Set<CandidateSkill>();
    public DbSet<CandidateTag> CandidateTags => Set<CandidateTag>();
    public DbSet<CandidateNote> CandidateNotes => Set<CandidateNote>();
    public DbSet<CandidateDocument> Documents => Set<CandidateDocument>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<MigrationRun> MigrationRuns => Set<MigrationRun>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<SearchPreset> SearchPresets => Set<SearchPreset>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRowOutcome> ImportRowOutcomes => Set<ImportRowOutcome>();
    public DbSet<Position> Positions => Set<Position>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
