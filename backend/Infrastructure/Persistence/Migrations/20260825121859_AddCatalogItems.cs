using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CAT_CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Family = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NameEs = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameNormalized = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAT_CatalogItems", x => x.Id);
                    table.CheckConstraint("CK_CAT_CatalogItems_Family", "\"Family\" IN ('language', 'program', 'skill', 'language_level', 'program_level', 'skill_level', 'education_type', 'education_status', 'sector')");
                    table.CheckConstraint("CK_CAT_CatalogItems_Name", "char_length(\"NameEs\") > 0 AND char_length(\"NameNormalized\") > 0 AND char_length(\"Code\") > 0");
                    table.CheckConstraint("CK_CAT_CatalogItems_SortOrder", "\"SortOrder\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CAT_CatalogItems_Family_SortOrder",
                table: "CAT_CatalogItems",
                columns: new[] { "Family", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_CAT_CatalogItems_Family_Code",
                table: "CAT_CatalogItems",
                columns: new[] { "Family", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CAT_CatalogItems_Family_NameNormalized",
                table: "CAT_CatalogItems",
                columns: new[] { "Family", "NameNormalized" },
                unique: true);

            // Catalog values are never physically deleted: the runtime role is granted no
            // DELETE, so the rule holds at the database even if an endpoint were added.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE ON "CAT_CatalogItems" TO ktl_runtime;
                        REVOKE DELETE, TRUNCATE ON "CAT_CatalogItems" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CAT_CatalogItems");
        }
    }
}
