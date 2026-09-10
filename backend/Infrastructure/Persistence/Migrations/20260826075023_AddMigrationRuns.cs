using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMigrationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OPS_MigrationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Verb = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BackupLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceRows = table.Column<int>(type: "integer", nullable: false),
                    LoadedRows = table.Column<int>(type: "integer", nullable: false),
                    RejectedRows = table.Column<int>(type: "integer", nullable: false),
                    SkippedRows = table.Column<int>(type: "integer", nullable: false),
                    UnmatchedTargetRecords = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedValues = table.Column<int>(type: "integer", nullable: false),
                    DocumentsLoaded = table.Column<int>(type: "integer", nullable: false),
                    DocumentsRejected = table.Column<int>(type: "integer", nullable: false),
                    ReportJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPS_MigrationRuns", x => x.Id);
                    table.CheckConstraint("CK_OPS_MigrationRuns_BackupLabel", "\"Verb\" <> 'load' OR \"BackupLabel\" IS NOT NULL");
                    table.CheckConstraint("CK_OPS_MigrationRuns_Outcome", "\"Outcome\" IN ('running', 'reconciled', 'not_reconciled', 'failed')");
                    table.CheckConstraint("CK_OPS_MigrationRuns_Verb", "\"Verb\" IN ('validate', 'load', 'report')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OPS_MigrationRuns_StartedAtUtc",
                table: "OPS_MigrationRuns",
                column: "StartedAtUtc");

            // Migration history belongs to the operator running the tool, not to the API.
            // The runtime role is granted nothing here; the REVOKE is explicit so that a
            // future blanket GRANT cannot quietly hand the API this table.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE ALL ON "OPS_MigrationRuns" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OPS_MigrationRuns");
        }
    }
}
