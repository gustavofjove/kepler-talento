using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCandidateRelationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateSkills_CandidateId_SkillId",
                table: "CND_CandidateSkills",
                columns: new[] { "CandidateId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidatePrograms_CandidateId_ProgramId",
                table: "CND_CandidatePrograms",
                columns: new[] { "CandidateId", "ProgramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateLanguages_CandidateId_LanguageId",
                table: "CND_CandidateLanguages",
                columns: new[] { "CandidateId", "LanguageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CND_CandidateSkills_CandidateId_SkillId",
                table: "CND_CandidateSkills");

            migrationBuilder.DropIndex(
                name: "UX_CND_CandidatePrograms_CandidateId_ProgramId",
                table: "CND_CandidatePrograms");

            migrationBuilder.DropIndex(
                name: "UX_CND_CandidateLanguages_CandidateId_LanguageId",
                table: "CND_CandidateLanguages");
        }
    }
}
