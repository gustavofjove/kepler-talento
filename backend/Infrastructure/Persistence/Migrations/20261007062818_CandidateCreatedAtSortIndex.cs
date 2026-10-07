using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateCreatedAtSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_CreatedAtUtc",
                table: "CND_Candidates",
                columns: new[] { "CreatedAtUtc", "Id" },
                descending: new[] { true, false },
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_CreatedAtUtc",
                table: "CND_Candidates");
        }
    }
}
