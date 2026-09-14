using KeplerTalento.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Saved searches. The <c>ADM_</c> prefix is deliberate: this is administrator-curated
/// configuration, not candidate data, and storing it beside <c>CND_</c> tables would
/// misrepresent what it is.
/// </summary>
public sealed class SearchPresetConfiguration : IEntityTypeConfiguration<SearchPreset>
{
    public const string Table = "ADM_SearchPresets";
    public const string UniqueNameIndex = "UX_ADM_SearchPresets_NormalizedName";

    public void Configure(EntityTypeBuilder<SearchPreset> builder)
    {
        builder.ToTable(Table, table =>
        {
            // Bounds and blankness are the database's business as well as the domain's: a
            // check constraint holds whichever code path writes the row, including a future
            // one that forgets.
            table.HasCheckConstraint(
                $"CK_{Table}_Name",
                "char_length(btrim(\"Name\")) > 0 AND char_length(\"NormalizedName\") > 0");
            // The column is JSONB, so anything stored is valid JSON; what this adds is that
            // it is a JSON *object* rather than an array, a number or a bare null.
            table.HasCheckConstraint(
                $"CK_{Table}_Filters",
                "jsonb_typeof(\"Filters\") = 'object'");
            table.HasCheckConstraint(
                $"CK_{Table}_FilterSchemaVersion",
                "\"FilterSchemaVersion\" >= 1");
            table.HasCheckConstraint(
                $"CK_{Table}_Version",
                "\"Version\" >= 1");
            // Applying a preset records its use without changing its content (KTL-14), so
            // "last used" may be later than "last updated" but never earlier than creation.
            table.HasCheckConstraint(
                $"CK_{Table}_Timestamps",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastUsedAtUtc\" IS NULL OR \"LastUsedAtUtc\" >= \"CreatedAtUtc\")");
        });
        builder.HasKey(preset => preset.Id);
        builder.Property(preset => preset.Name).HasMaxLength(SearchPresetName.MaximumLength).IsRequired();
        builder.Property(preset => preset.NormalizedName)
            .HasMaxLength(SearchPresetName.MaximumLength)
            .IsRequired();
        builder.Property(preset => preset.Filters).HasColumnType("jsonb").IsRequired();
        builder.Property(preset => preset.FilterSchemaVersion).IsRequired();
        builder.Property(preset => preset.CreatedAtUtc).IsRequired();
        builder.Property(preset => preset.UpdatedAtUtc).IsRequired();

        // An application-maintained counter, not IsRowVersion()/xmin like the other aggregates:
        // see SearchPreset.Version and the KTL-14 design (D2). Recording a use must not make
        // an administrator's pending edit stale.
        builder.Property(preset => preset.Version).IsRequired().IsConcurrencyToken();

        // One name across the whole library, decided by the database. Two concurrent creations
        // can both find the name free; only one can store it, and the loser gets a conflict
        // rather than a duplicate. It is also the listing index.
        builder.HasIndex(preset => preset.NormalizedName)
            .IsUnique()
            .HasDatabaseName(UniqueNameIndex);
    }
}
