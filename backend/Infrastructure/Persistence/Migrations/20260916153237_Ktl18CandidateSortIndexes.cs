using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Ktl18CandidateSortIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_LastName_FirstName_Id",
                table: "CND_Candidates",
                columns: new[] { "IsActive", "LastName", "FirstName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_Status_Id",
                table: "CND_Candidates",
                columns: new[] { "IsActive", "Status", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_LastName_FirstName_Id",
                table: "CND_Candidates");

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_Status_Id",
                table: "CND_Candidates");
        }
    }
}
