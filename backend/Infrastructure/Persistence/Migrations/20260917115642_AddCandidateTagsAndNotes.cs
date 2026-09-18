using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateTagsAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CAT_CatalogItems_Family",
                table: "CAT_CatalogItems");

            migrationBuilder.CreateTable(
                name: "CND_CandidateNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateNotes", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateNotes_Body", "char_length(btrim(\"Body\")) BETWEEN 1 AND 4000");
                    table.CheckConstraint("CK_CND_CandidateNotes_Deleted", "(\"IsActive\" AND \"DeletedAtUtc\" IS NULL) OR (NOT \"IsActive\" AND \"DeletedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CND_CandidateNotes_ADM_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "ADM_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateNotes_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CND_CandidateTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateTags", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateTags_TagFamily", "\"TagFamily\" = 'tag'");
                    table.ForeignKey(
                        name: "FK_CND_CandidateTags_CAT_CatalogItems_TagId_TagFamily",
                        columns: x => new { x.TagId, x.TagFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateTags_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CAT_CatalogItems_Family",
                table: "CAT_CatalogItems",
                sql: "\"Family\" IN ('language', 'program', 'skill', 'language_level', 'program_level', 'skill_level', 'education_type', 'education_status', 'sector', 'tag')");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateNotes_AuthorUserId",
                table: "CND_CandidateNotes",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateNotes_CandidateId_CreatedAtUtc_Active",
                table: "CND_CandidateNotes",
                columns: new[] { "CandidateId", "CreatedAtUtc" },
                descending: new[] { false, true },
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateTags_CandidateId",
                table: "CND_CandidateTags",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateTags_TagId_TagFamily",
                table: "CND_CandidateTags",
                columns: new[] { "TagId", "TagFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateTags_CandidateId_TagId",
                table: "CND_CandidateTags",
                columns: new[] { "CandidateId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateTags_SourceKey",
                table: "CND_CandidateTags",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE ON "CND_CandidateTags" TO ktl_runtime;
                        GRANT SELECT, INSERT, UPDATE ON "CND_CandidateNotes" TO ktl_runtime;
                        REVOKE DELETE, TRUNCATE ON "CND_CandidateNotes" FROM ktl_runtime;
                        REVOKE TRUNCATE ON "CND_CandidateTags" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CND_CandidateNotes");

            migrationBuilder.DropTable(
                name: "CND_CandidateTags");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CAT_CatalogItems_Family",
                table: "CAT_CatalogItems");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CAT_CatalogItems_Family",
                table: "CAT_CatalogItems",
                sql: "\"Family\" IN ('language', 'program', 'skill', 'language_level', 'program_level', 'skill_level', 'education_type', 'education_status', 'sector')");
        }
    }
}
