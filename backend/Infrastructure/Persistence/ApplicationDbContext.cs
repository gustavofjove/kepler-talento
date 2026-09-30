using KeplerTalento.Application.Import;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Domain.Import;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Domain.Positions;
using KeplerTalento.Domain.Search;
using KeplerTalento.Infrastructure.Encryption;
using KeplerTalento.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
    public DbSet<PositionCandidate> PositionCandidates => Set<PositionCandidate>();

    /// <summary>Stored envelopes for the encrypted search stage only (KTL-33).</summary>
    public DbSet<CandidateCiphertext> CandidateCiphertexts => Set<CandidateCiphertext>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEmailHashes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampEmailHashes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Keeps the e-mail blind index in step with the e-mail (KTL-33). Done here rather than in a
    /// repository so that no writer — API, import, legacy loader, test — can store an e-mail
    /// without its hash.
    /// </summary>
    private void StampEmailHashes()
    {
        foreach (var entry in ChangeTracker.Entries<Candidate>())
        {
            var emailChanged = entry.State == EntityState.Added
                || (entry.State == EntityState.Modified && entry.Property(candidate => candidate.Email).IsModified);
            if (!emailChanged)
            {
                continue;
            }
            var blindIndex = FieldEncryptionModel.BlindIndexFrom(options)
                ?? throw new InvalidOperationException("Field encryption is not configured for this database context.");
            entry.Property<string>(CandidateConfiguration.EmailHash).CurrentValue =
                blindIndex.Compute(CandidateImportRowEvaluator.NormalizeEmail(entry.Entity.Email));
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder
            .ReplaceService<IModelCacheKeyFactory, FieldEncryptionModelCacheKeyFactory>()
            .AddInterceptors(EncryptedColumnQueryGuard.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        // KTL-33: candidate personal data is stored encrypted. A context built without a
        // protector still gets converters, which refuse to read or write rather than pass
        // plaintext through.
        FieldEncryptionModel.Apply(modelBuilder, FieldEncryptionModel.ProtectorFrom(options));
    }
}
