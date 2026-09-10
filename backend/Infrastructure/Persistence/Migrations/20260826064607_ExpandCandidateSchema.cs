using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCandidateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "CND_Documents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "CND_Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "CND_Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ConsentAt",
                table: "CND_Candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "CND_Candidates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "CND_Candidates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "CND_Candidates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceivedAt",
                table: "CND_Candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewDueAt",
                table: "CND_Candidates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                table: "CND_Candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceLoadedAtUtc",
                table: "CND_Candidates",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill rows that predate the column with a legal status, then drop the
            // default: "" would violate CK_CND_Candidates_Status, and the application
            // always sets a status explicitly, so no column default should survive.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CND_Candidates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.Sql("ALTER TABLE \"CND_Candidates\" ALTER COLUMN \"Status\" DROP DEFAULT;");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CAT_CatalogItems_Id_Family",
                table: "CAT_CatalogItems",
                columns: new[] { "Id", "Family" });

            migrationBuilder.CreateTable(
                name: "CND_CandidateEducation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationTypeFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Degree = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EndYear = table.Column<int>(type: "integer", nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateEducation", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateEducation_Degree", "char_length(\"Degree\") > 0 AND char_length(\"Institution\") > 0");
                    table.CheckConstraint("CK_CND_CandidateEducation_EducationTypeFamily", "\"EducationTypeFamily\" = 'education_type'");
                    table.CheckConstraint("CK_CND_CandidateEducation_EndYear", "\"EndYear\" IS NULL OR (\"EndYear\" BETWEEN 1900 AND 2200)");
                    table.CheckConstraint("CK_CND_CandidateEducation_StatusFamily", "\"StatusFamily\" = 'education_status'");
                    table.ForeignKey(
                        name: "FK_CND_CandidateEducation_CAT_CatalogItems_EducationTypeId_Edu~",
                        columns: x => new { x.EducationTypeId, x.EducationTypeFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateEducation_CAT_CatalogItems_StatusId_StatusFami~",
                        columns: x => new { x.StatusId, x.StatusFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateEducation_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CND_CandidateExperience",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectorFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Functions = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    YearsExperience = table.Column<int>(type: "integer", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateExperience", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateExperience_Company", "char_length(\"Company\") > 0 AND char_length(\"Position\") > 0");
                    table.CheckConstraint("CK_CND_CandidateExperience_Period", "(\"StartDate\" IS NULL OR \"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\") AND (NOT \"IsCurrent\" OR \"EndDate\" IS NULL)");
                    table.CheckConstraint("CK_CND_CandidateExperience_SectorFamily", "\"SectorFamily\" = 'sector'");
                    table.CheckConstraint("CK_CND_CandidateExperience_YearsExperience", "\"YearsExperience\" IS NULL OR \"YearsExperience\" >= 0");
                    table.ForeignKey(
                        name: "FK_CND_CandidateExperience_CAT_CatalogItems_SectorId_SectorFam~",
                        columns: x => new { x.SectorId, x.SectorFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateExperience_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CND_CandidateLanguages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Certification = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateLanguages", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateLanguages_LanguageFamily", "\"LanguageFamily\" = 'language'");
                    table.CheckConstraint("CK_CND_CandidateLanguages_LevelFamily", "\"LevelFamily\" = 'language_level'");
                    table.ForeignKey(
                        name: "FK_CND_CandidateLanguages_CAT_CatalogItems_LanguageId_Language~",
                        columns: x => new { x.LanguageId, x.LanguageFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateLanguages_CAT_CatalogItems_LevelId_LevelFamily",
                        columns: x => new { x.LevelId, x.LevelFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateLanguages_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CND_CandidatePrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    YearsExperience = table.Column<int>(type: "integer", nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidatePrograms", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidatePrograms_LevelFamily", "\"LevelFamily\" = 'program_level'");
                    table.CheckConstraint("CK_CND_CandidatePrograms_ProgramFamily", "\"ProgramFamily\" = 'program'");
                    table.CheckConstraint("CK_CND_CandidatePrograms_YearsExperience", "\"YearsExperience\" IS NULL OR \"YearsExperience\" >= 0");
                    table.ForeignKey(
                        name: "FK_CND_CandidatePrograms_CAT_CatalogItems_LevelId_LevelFamily",
                        columns: x => new { x.LevelId, x.LevelFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidatePrograms_CAT_CatalogItems_ProgramId_ProgramFam~",
                        columns: x => new { x.ProgramId, x.ProgramFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidatePrograms_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CND_CandidateSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_CandidateSkills", x => x.Id);
                    table.CheckConstraint("CK_CND_CandidateSkills_LevelFamily", "\"LevelFamily\" = 'skill_level'");
                    table.CheckConstraint("CK_CND_CandidateSkills_SkillFamily", "\"SkillFamily\" = 'skill'");
                    table.ForeignKey(
                        name: "FK_CND_CandidateSkills_CAT_CatalogItems_LevelId_LevelFamily",
                        columns: x => new { x.LevelId, x.LevelFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateSkills_CAT_CatalogItems_SkillId_SkillFamily",
                        columns: x => new { x.SkillId, x.SkillFamily },
                        principalTable: "CAT_CatalogItems",
                        principalColumns: new[] { "Id", "Family" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CND_CandidateSkills_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_CND_Documents_CandidateId_Primary",
                table: "CND_Documents",
                column: "CandidateId",
                unique: true,
                filter: "\"IsPrimary\"");

            migrationBuilder.CreateIndex(
                name: "UX_CND_Documents_SourceKey",
                table: "CND_Documents",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_UpdatedAtUtc",
                table: "CND_Candidates",
                columns: new[] { "IsActive", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_Candidates_SourceKey",
                table: "CND_Candidates",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_Deleted",
                table: "CND_Candidates",
                sql: "(\"IsActive\" AND \"DeletedAtUtc\" IS NULL) OR (NOT \"IsActive\" AND \"DeletedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_SourceLoaded",
                table: "CND_Candidates",
                sql: "\"SourceLoadedAtUtc\" IS NULL OR \"SourceKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_Status",
                table: "CND_Candidates",
                sql: "\"Status\" IN ('new', 'available', 'in_process', 'hired', 'rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateEducation_CandidateId",
                table: "CND_CandidateEducation",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateEducation_EducationTypeId_EducationTypeFamily",
                table: "CND_CandidateEducation",
                columns: new[] { "EducationTypeId", "EducationTypeFamily" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateEducation_StatusId_StatusFamily",
                table: "CND_CandidateEducation",
                columns: new[] { "StatusId", "StatusFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateEducation_SourceKey",
                table: "CND_CandidateEducation",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateExperience_CandidateId",
                table: "CND_CandidateExperience",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateExperience_SectorId_SectorFamily",
                table: "CND_CandidateExperience",
                columns: new[] { "SectorId", "SectorFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateExperience_SourceKey",
                table: "CND_CandidateExperience",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateLanguages_CandidateId",
                table: "CND_CandidateLanguages",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateLanguages_LanguageId_LanguageFamily",
                table: "CND_CandidateLanguages",
                columns: new[] { "LanguageId", "LanguageFamily" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateLanguages_LevelId_LevelFamily",
                table: "CND_CandidateLanguages",
                columns: new[] { "LevelId", "LevelFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateLanguages_SourceKey",
                table: "CND_CandidateLanguages",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidatePrograms_CandidateId",
                table: "CND_CandidatePrograms",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidatePrograms_LevelId_LevelFamily",
                table: "CND_CandidatePrograms",
                columns: new[] { "LevelId", "LevelFamily" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidatePrograms_ProgramId_ProgramFamily",
                table: "CND_CandidatePrograms",
                columns: new[] { "ProgramId", "ProgramFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidatePrograms_SourceKey",
                table: "CND_CandidatePrograms",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateSkills_CandidateId",
                table: "CND_CandidateSkills",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateSkills_LevelId_LevelFamily",
                table: "CND_CandidateSkills",
                columns: new[] { "LevelId", "LevelFamily" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_CandidateSkills_SkillId_SkillFamily",
                table: "CND_CandidateSkills",
                columns: new[] { "SkillId", "SkillFamily" });

            migrationBuilder.CreateIndex(
                name: "UX_CND_CandidateSkills_SourceKey",
                table: "CND_CandidateSkills",
                column: "SourceKey",
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            // Least privilege for the new tables ships with the migration that creates
            // them. Relation collections are replaced as whole sets, so DELETE is needed
            // there; the runtime role still gets no schema-modification privilege, which
            // the initial migration's REVOKE CREATE ON SCHEMA already denies.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE
                            ON "CND_CandidateLanguages", "CND_CandidatePrograms",
                               "CND_CandidateEducation", "CND_CandidateExperience",
                               "CND_CandidateSkills"
                            TO ktl_runtime;
                        REVOKE TRUNCATE
                            ON "CND_CandidateLanguages", "CND_CandidatePrograms",
                               "CND_CandidateEducation", "CND_CandidateExperience",
                               "CND_CandidateSkills"
                            FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CND_CandidateEducation");

            migrationBuilder.DropTable(
                name: "CND_CandidateExperience");

            migrationBuilder.DropTable(
                name: "CND_CandidateLanguages");

            migrationBuilder.DropTable(
                name: "CND_CandidatePrograms");

            migrationBuilder.DropTable(
                name: "CND_CandidateSkills");

            migrationBuilder.DropIndex(
                name: "UX_CND_Documents_CandidateId_Primary",
                table: "CND_Documents");

            migrationBuilder.DropIndex(
                name: "UX_CND_Documents_SourceKey",
                table: "CND_Documents");

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_UpdatedAtUtc",
                table: "CND_Candidates");

            migrationBuilder.DropIndex(
                name: "UX_CND_Candidates_SourceKey",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_Deleted",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_SourceLoaded",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_Status",
                table: "CND_Candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CAT_CatalogItems_Id_Family",
                table: "CAT_CatalogItems");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "CND_Documents");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "CND_Documents");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "CND_Documents");

            migrationBuilder.DropColumn(
                name: "Availability",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "ConsentAt",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "ReviewDueAt",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "SourceLoadedAtUtc",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CND_Candidates");
        }
    }
}
