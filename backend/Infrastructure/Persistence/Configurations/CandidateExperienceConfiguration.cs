using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    private const string Table = "CND_CandidateExperience";

    public void Configure(EntityTypeBuilder<CandidateExperience> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_SectorFamily",
                CandidateRelationMapping.FamilyCheck("SectorFamily", CatalogFamilies.Sector));
            table.HasCheckConstraint(
                $"CK_{Table}_Company",
                "char_length(\"Company\") > 0 AND char_length(\"Position\") > 0");
            table.HasCheckConstraint(
                $"CK_{Table}_Period",
                "(\"StartDate\" IS NULL OR \"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\") "
                    + "AND (NOT \"IsCurrent\" OR \"EndDate\" IS NULL)");
            table.HasCheckConstraint(
                $"CK_{Table}_YearsExperience",
                "\"YearsExperience\" IS NULL OR \"YearsExperience\" >= 0");
        });
        builder.ConfigureRelation(Table, candidate => candidate.Experience);
        builder.Property(experience => experience.Company).HasMaxLength(200).IsRequired();
        builder.Property(experience => experience.Position).HasMaxLength(200).IsRequired();
        builder.HasCatalogReference(
            experience => new { experience.SectorId, experience.SectorFamily },
            experience => experience.SectorFamily);
    }
}
