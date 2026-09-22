using KeplerTalento.Domain.Positions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public const string Table = "OPS_Positions";
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("CK_OPS_Positions_Title", "char_length(btrim(\"Title\")) > 0 AND char_length(\"Title\") <= 200");
            table.HasCheckConstraint("CK_OPS_Positions_Location", "char_length(\"Location\") <= 200");
            table.HasCheckConstraint("CK_OPS_Positions_Description", "char_length(\"Description\") <= 20000");
            table.HasCheckConstraint("CK_OPS_Positions_Status", "\"Status\" IN ('open', 'closed')");
            table.HasCheckConstraint("CK_OPS_Positions_Requirements", "jsonb_typeof(\"Requirements\") = 'object'");
            table.HasCheckConstraint("CK_OPS_Positions_RequirementsVersion", "\"Requirements\" @> jsonb_build_object('version', \"FilterSchemaVersion\")");
            table.HasCheckConstraint("CK_OPS_Positions_FilterSchemaVersion", "\"FilterSchemaVersion\" >= 1");
            table.HasCheckConstraint("CK_OPS_Positions_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.HasKey(position => position.Id);
        builder.Property(position => position.Title).HasMaxLength(PositionText.MaximumTitleLength).IsRequired();
        builder.Property(position => position.NormalizedTitle).HasMaxLength(PositionText.MaximumTitleLength).IsRequired();
        builder.Property(position => position.Description).HasMaxLength(PositionText.MaximumDescriptionLength).IsRequired();
        builder.Property(position => position.Location).HasMaxLength(PositionText.MaximumLocationLength).IsRequired();
        builder.Property(position => position.NormalizedLocation).HasMaxLength(PositionText.MaximumLocationLength).IsRequired();
        builder.Property(position => position.Status).HasMaxLength(10).IsRequired();
        builder.Property(position => position.Requirements).HasColumnType("jsonb").IsRequired();
        builder.Property(position => position.Version).IsRowVersion();
        builder.HasIndex(position => position.NormalizedTitle).IsUnique().HasDatabaseName("UX_OPS_Positions_NormalizedTitle");
        builder.HasIndex(position => new { position.Status, position.UpdatedAtUtc, position.Id }).HasDatabaseName("IX_OPS_Positions_Status_UpdatedAtUtc_Id");
        builder.HasIndex(position => new { position.Status, position.NormalizedTitle, position.Id }).HasDatabaseName("IX_OPS_Positions_Status_NormalizedTitle_Id");
        builder.HasIndex(position => new { position.Status, position.NormalizedLocation, position.Id }).HasDatabaseName("IX_OPS_Positions_Status_NormalizedLocation_Id");
    }
}
