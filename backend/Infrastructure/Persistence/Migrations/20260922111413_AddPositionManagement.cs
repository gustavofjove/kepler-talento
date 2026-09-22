using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.CreateTable(
                name: "OPS_Positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Requirements = table.Column<string>(type: "jsonb", nullable: false),
                    FilterSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPS_Positions", x => x.Id);
                    table.CheckConstraint("CK_OPS_Positions_Description", "char_length(\"Description\") <= 20000");
                    table.CheckConstraint("CK_OPS_Positions_FilterSchemaVersion", "\"FilterSchemaVersion\" >= 1");
                    table.CheckConstraint("CK_OPS_Positions_Location", "char_length(\"Location\") <= 200");
                    table.CheckConstraint("CK_OPS_Positions_Requirements", "jsonb_typeof(\"Requirements\") = 'object'");
                    table.CheckConstraint("CK_OPS_Positions_RequirementsVersion", "\"Requirements\" @> jsonb_build_object('version', \"FilterSchemaVersion\")");
                    table.CheckConstraint("CK_OPS_Positions_Status", "\"Status\" IN ('open', 'closed')");
                    table.CheckConstraint("CK_OPS_Positions_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_OPS_Positions_Title", "char_length(btrim(\"Title\")) > 0 AND char_length(\"Title\") <= 200");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OPS_Positions_Status_NormalizedLocation_Id",
                table: "OPS_Positions",
                columns: new[] { "Status", "NormalizedLocation", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OPS_Positions_Status_NormalizedTitle_Id",
                table: "OPS_Positions",
                columns: new[] { "Status", "NormalizedTitle", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OPS_Positions_Status_UpdatedAtUtc_Id",
                table: "OPS_Positions",
                columns: new[] { "Status", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_OPS_Positions_NormalizedTitle",
                table: "OPS_Positions",
                column: "NormalizedTitle",
                unique: true);

            migrationBuilder.Sql("""
                CREATE INDEX "IX_OPS_Positions_NormalizedTitle_Trgm"
                    ON "OPS_Positions" USING gin ("NormalizedTitle" gin_trgm_ops);
                CREATE INDEX "IX_OPS_Positions_NormalizedLocation_Trgm"
                    ON "OPS_Positions" USING gin ("NormalizedLocation" gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                UPDATE "ADM_Roles"
                SET "Permissions" = (
                        SELECT jsonb_agg(value ORDER BY value)
                        FROM (SELECT jsonb_array_elements_text("Permissions") AS value
                              UNION SELECT 'positions.read') AS merged),
                    "UpdatedAtUtc" = NOW()
                WHERE "IsSystem" AND NOT "Permissions" ? 'positions.read';

                UPDATE "ADM_Roles"
                SET "Permissions" = (
                        SELECT jsonb_agg(value ORDER BY value)
                        FROM (SELECT jsonb_array_elements_text("Permissions") AS value
                              UNION SELECT 'positions.manage') AS merged),
                    "UpdatedAtUtc" = NOW()
                WHERE "IsSystem" AND "Name" IN ('rrhh_admin', 'rrhh_user')
                  AND NOT "Permissions" ? 'positions.manage';
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE ON "OPS_Positions" TO ktl_runtime;
                        REVOKE DELETE, TRUNCATE ON "OPS_Positions" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ADM_Roles"
                SET "Permissions" = ("Permissions" - 'positions.read') - 'positions.manage',
                    "UpdatedAtUtc" = NOW()
                WHERE "Permissions" ?| ARRAY['positions.read', 'positions.manage'];

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE ALL ON "OPS_Positions" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
            migrationBuilder.DropTable(
                name: "OPS_Positions");
        }
    }
}
