using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public static readonly string FamilyCheckConstraint =
        "\"Family\" IN (" + string.Join(", ", CatalogFamilies.All.Select(family => $"'{family}'")) + ")";

    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("CAT_CatalogItems", table =>
        {
            table.HasCheckConstraint("CK_CAT_CatalogItems_Family", FamilyCheckConstraint);
            table.HasCheckConstraint(
                "CK_CAT_CatalogItems_Name",
                "char_length(\"NameEs\") > 0 AND char_length(\"NameNormalized\") > 0 AND char_length(\"Code\") > 0");
            table.HasCheckConstraint("CK_CAT_CatalogItems_SortOrder", "\"SortOrder\" > 0");
        });
        builder.HasKey(item => item.Id);
        // Candidate relations reference a catalog entry by (Id, Family) so a language
        // reference cannot resolve to a sector. That composite foreign key needs this
        // alternate key as its principal.
        builder.HasAlternateKey(item => new { item.Id, item.Family })
            .HasName(CandidateRelationMapping.CatalogAlternateKey);
        builder.Property(item => item.Family).HasMaxLength(40).IsRequired();
        builder.Property(item => item.Code).HasMaxLength(80).IsRequired();
        builder.Property(item => item.NameEs).HasMaxLength(160).IsRequired();
        builder.Property(item => item.NameNormalized).HasMaxLength(160).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(160);
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => new { item.Family, item.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_CAT_CatalogItems_Family_NameNormalized");
        builder.HasIndex(item => new { item.Family, item.Code })
            .IsUnique()
            .HasDatabaseName("UX_CAT_CatalogItems_Family_Code");
        builder.HasIndex(item => new { item.Family, item.SortOrder });
    }
}
