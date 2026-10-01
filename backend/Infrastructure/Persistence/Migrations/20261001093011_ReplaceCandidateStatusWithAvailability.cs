using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCandidateStatusWithAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_Status_Id",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_Status",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Availability",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CND_Candidates");

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityCheckedByUserId",
                table: "CND_Candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AvailabilityCheckedOn",
                table: "CND_Candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityState",
                table: "CND_Candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AvailabilityUntil",
                table: "CND_Candidates",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_AvailabilityCheckedByUserId",
                table: "CND_Candidates",
                column: "AvailabilityCheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_AvailabilityCheckedOn",
                table: "CND_Candidates",
                columns: new[] { "AvailabilityCheckedOn", "Id" },
                descending: new[] { true, false },
                filter: "\"IsActive\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityCheck",
                table: "CND_Candidates",
                sql: "(\"AvailabilityState\" = 'unknown' AND \"AvailabilityCheckedOn\" IS NULL AND \"AvailabilityCheckedByUserId\" IS NULL) OR (\"AvailabilityState\" <> 'unknown' AND \"AvailabilityCheckedOn\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityState",
                table: "CND_Candidates",
                sql: "\"AvailabilityState\" IN ('unknown', 'available', 'unavailable')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityUntil",
                table: "CND_Candidates",
                sql: "\"AvailabilityUntil\" IS NULL OR (\"AvailabilityState\" = 'unavailable' AND \"AvailabilityUntil\" >= \"AvailabilityCheckedOn\")");

            migrationBuilder.AddForeignKey(
                name: "FK_CND_Candidates_ADM_Users_AvailabilityCheckedByUserId",
                table: "CND_Candidates",
                column: "AvailabilityCheckedByUserId",
                principalTable: "ADM_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Filter schema version 2 (KTL-36 design D6): stored presets and position requirements
            // drop the candidate statuses and gain an unrestricted availability family. Only the
            // "text" member is ciphertext, and it is left untouched, so nothing is decrypted or
            // re-encrypted. The version member and its column change in one statement, so
            // CK_OPS_Positions_RequirementsVersion holds throughout.
            migrationBuilder.Sql(
                """
                UPDATE "ADM_SearchPresets"
                   SET "Filters" = ("Filters" - 'statusValues')
                                   || jsonb_build_object(
                                          'version', 2,
                                          'availabilityValues', '["unknown","available","unavailable"]'::jsonb,
                                          'availabilityCheckedFrom', ''),
                       "FilterSchemaVersion" = 2;
                """);
            migrationBuilder.Sql(
                """
                UPDATE "OPS_Positions"
                   SET "Requirements" = ("Requirements" - 'statusValues')
                                        || jsonb_build_object(
                                               'version', 2,
                                               'availabilityValues', '["unknown","available","unavailable"]'::jsonb,
                                               'availabilityCheckedFrom', ''),
                       "FilterSchemaVersion" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to filter schema version 1, with every candidate status selected: the
            // availability selection has no status equivalent, so the rollback is unrestricted.
            migrationBuilder.Sql(
                """
                UPDATE "ADM_SearchPresets"
                   SET "Filters" = ("Filters" - 'availabilityValues' - 'availabilityCheckedFrom')
                                   || jsonb_build_object(
                                          'version', 1,
                                          'statusValues', '["new","available","in_process","hired","rejected"]'::jsonb),
                       "FilterSchemaVersion" = 1;
                """);
            migrationBuilder.Sql(
                """
                UPDATE "OPS_Positions"
                   SET "Requirements" = ("Requirements" - 'availabilityValues' - 'availabilityCheckedFrom')
                                        || jsonb_build_object(
                                               'version', 1,
                                               'statusValues', '["new","available","in_process","hired","rejected"]'::jsonb),
                       "FilterSchemaVersion" = 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_CND_Candidates_ADM_Users_AvailabilityCheckedByUserId",
                table: "CND_Candidates");

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_AvailabilityCheckedByUserId",
                table: "CND_Candidates");

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_AvailabilityCheckedOn",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityCheck",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityState",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_AvailabilityUntil",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "AvailabilityCheckedByUserId",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "AvailabilityCheckedOn",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "AvailabilityState",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "AvailabilityUntil",
                table: "CND_Candidates");

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CND_Candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_Status_Id",
                table: "CND_Candidates",
                columns: new[] { "IsActive", "Status", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_Status",
                table: "CND_Candidates",
                sql: "\"Status\" IN ('new', 'available', 'in_process', 'hired', 'rejected')");
        }
    }
}
