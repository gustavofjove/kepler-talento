using KeplerTalento.Domain.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Saved searches. The <c>ADM_</c> prefix is deliberate: this is user-owned configuration,
/// not candidate data, and storing it beside <c>CND_</c> tables would misrepresent what it is.
/// </summary>
public sealed class SearchPresetConfiguration : IEntityTypeConfiguration<SearchPreset>
{
    public const string Table = "ADM_SearchPresets";
    public const string UniqueNameIndex = "UX_ADM_SearchPresets_Owner_NormalizedName";

    public void Configure(EntityTypeBuilder<SearchPreset> builder)
    {
        builder.ToTable(Table, table =>
        {
            // Bounds and blankness are the database's business as well as the domain's: a
            // check constraint holds whichever code path writes the row, including a future
            // one that forgets.
            table.HasCheckConstraint(
                $"CK_{Table}_Owner",
                "char_length(\"OwnerId\") > 0");
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
            // "Last used" describes this row, so it cannot be newer than the row itself.
            table.HasCheckConstraint(
                $"CK_{Table}_Timestamps",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastUsedAtUtc\" IS NULL OR \"LastUsedAtUtc\" <= \"UpdatedAtUtc\")");
        });
        builder.HasKey(preset => preset.Id);
        builder.Property(preset => preset.OwnerId).HasMaxLength(200).IsRequired();
        builder.Property(preset => preset.Name).HasMaxLength(SearchPresetName.MaximumLength).IsRequired();
        builder.Property(preset => preset.NormalizedName)
            .HasMaxLength(SearchPresetName.MaximumLength)
            .IsRequired();
        builder.Property(preset => preset.Filters).HasColumnType("jsonb").IsRequired();
        builder.Property(preset => preset.FilterSchemaVersion).IsRequired();
        builder.Property(preset => preset.CreatedAtUtc).IsRequired();
        builder.Property(preset => preset.UpdatedAtUtc).IsRequired();

        // One name per owner, decided by the database. Two concurrent creations can both
        // find the name free; only one can store it, and the loser gets a conflict rather
        // than a duplicate.
        //
        // This is also the listing index. Owner-then-normalized-name is exactly the shape
        // the owner-scoped, case-insensitively alphabetical list reads, so a second index
        // over the same columns would be a duplicate that only costs writes.
        builder.HasIndex(preset => new { preset.OwnerId, preset.NormalizedName })
            .IsUnique()
            .HasDatabaseName(UniqueNameIndex);
    }
}
