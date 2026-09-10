using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateEducationConfiguration : IEntityTypeConfiguration<CandidateEducation>
{
    private const string Table = "CND_CandidateEducation";

    public void Configure(EntityTypeBuilder<CandidateEducation> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_EducationTypeFamily",
                CandidateRelationMapping.FamilyCheck("EducationTypeFamily", CatalogFamilies.EducationType));
            table.HasCheckConstraint(
                $"CK_{Table}_StatusFamily",
                CandidateRelationMapping.FamilyCheck("StatusFamily", CatalogFamilies.EducationStatus));
            table.HasCheckConstraint(
                $"CK_{Table}_Degree",
                "char_length(\"Degree\") > 0 AND char_length(\"Institution\") > 0");
            table.HasCheckConstraint(
                $"CK_{Table}_EndYear",
                "\"EndYear\" IS NULL OR (\"EndYear\" BETWEEN 1900 AND 2200)");
        });
        builder.ConfigureRelation(Table);
        builder.Property(education => education.Degree).HasMaxLength(200).IsRequired();
        builder.Property(education => education.Specialty).HasMaxLength(200);
        builder.Property(education => education.Institution).HasMaxLength(200).IsRequired();
        builder.HasCatalogReference(
            education => new { education.EducationTypeId, education.EducationTypeFamily },
            education => education.EducationTypeFamily);
        builder.HasCatalogReference(
            education => new { education.StatusId, education.StatusFamily },
            education => education.StatusFamily);
    }
}
