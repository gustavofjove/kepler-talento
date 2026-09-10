using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateProgramConfiguration : IEntityTypeConfiguration<CandidateProgram>
{
    private const string Table = "CND_CandidatePrograms";

    public void Configure(EntityTypeBuilder<CandidateProgram> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_ProgramFamily",
                CandidateRelationMapping.FamilyCheck("ProgramFamily", CatalogFamilies.Program));
            table.HasCheckConstraint(
                $"CK_{Table}_LevelFamily",
                CandidateRelationMapping.FamilyCheck("LevelFamily", CatalogFamilies.ProgramLevel));
            table.HasCheckConstraint(
                $"CK_{Table}_YearsExperience",
                "\"YearsExperience\" IS NULL OR \"YearsExperience\" >= 0");
        });
        builder.ConfigureRelation(Table);
        builder.HasCatalogReference(
            program => new { program.ProgramId, program.ProgramFamily },
            program => program.ProgramFamily);
        builder.HasCatalogReference(
            program => new { program.LevelId, program.LevelFamily },
            program => program.LevelFamily);
    }
}
