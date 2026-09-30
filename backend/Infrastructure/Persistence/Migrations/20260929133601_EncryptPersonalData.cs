using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncryptPersonalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_IsActive_LastName_FirstName_Id",
                table: "CND_Candidates");

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_LastName_FirstName",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_Candidates_Name",
                table: "CND_Candidates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_CandidateNotes_Body",
                table: "CND_CandidateNotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_CandidateExperience_Company",
                table: "CND_CandidateExperience");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CND_CandidateEducation_Degree",
                table: "CND_CandidateEducation");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "CND_Documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Availability",
                table: "CND_Candidates",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<string>(
                name: "EmailHash",
                table: "CND_Candidates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "CND_CandidateNotes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Certification",
                table: "CND_CandidateLanguages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "CND_CandidateExperience",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "CND_CandidateExperience",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Specialty",
                table: "CND_CandidateEducation",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Institution",
                table: "CND_CandidateEducation",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Degree",
                table: "CND_CandidateEducation",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_EmailHash",
                table: "CND_Candidates",
                column: "EmailHash");

            // KTL-33: from here on nothing may write plaintext into an encrypted column.
            // NOT VALID means PostgreSQL enforces the check on every insert and update at once
            // but does not check the rows already there; the operator backfill encrypts those,
            // and a later migration validates the constraints once production has run it.
            foreach (var (table, column, nullable) in EncryptedColumns)
            {
                var check = nullable
                    ? $"\"{column}\" IS NULL OR \"{column}\" LIKE 'ktl1.%'"
                    : $"\"{column}\" LIKE 'ktl1.%'";
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{table}\" ADD CONSTRAINT \"{EnvelopeCheckName(table, column)}\" CHECK ({check}) NOT VALID;");
            }
            migrationBuilder.Sql(
                $"ALTER TABLE \"CND_Candidates\" ADD CONSTRAINT \"{EmailHashCheck}\" CHECK (\"EmailHash\" ~ '^[A-Za-z0-9_-]{{1,32}}\\.[0-9a-f]{{64}}$') NOT VALID;");

            // The runtime role's privileges on the altered table, re-stated in the same slice.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE ON "CND_Candidates" TO ktl_runtime;
                        REVOKE DELETE, TRUNCATE ON "CND_Candidates" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <summary>
        /// Every encrypted column, fixed at the time of this migration. Deliberately a literal list
        /// rather than read from the model: a migration must mean the same thing forever.
        /// </summary>
        internal static readonly (string Table, string Column, bool Nullable)[] EncryptedColumns =
        [
            ("CND_Candidates", "FirstName", false),
            ("CND_Candidates", "LastName", false),
            ("CND_Candidates", "Phone", false),
            ("CND_Candidates", "Email", false),
            ("CND_Candidates", "Location", false),
            ("CND_Candidates", "Province", false),
            ("CND_Candidates", "Country", false),
            ("CND_Candidates", "Availability", false),
            ("CND_Candidates", "Source", false),
            ("CND_Candidates", "Notes", false),
            ("CND_CandidateNotes", "Body", false),
            ("CND_CandidateEducation", "Degree", false),
            ("CND_CandidateEducation", "Specialty", true),
            ("CND_CandidateEducation", "Institution", false),
            ("CND_CandidateEducation", "Notes", true),
            ("CND_CandidateExperience", "Company", false),
            ("CND_CandidateExperience", "Position", false),
            ("CND_CandidateExperience", "Functions", true),
            ("CND_CandidateExperience", "Notes", true),
            ("CND_CandidateLanguages", "Certification", true),
            ("CND_CandidateLanguages", "Notes", true),
            ("CND_CandidatePrograms", "Notes", true),
            ("CND_CandidateSkills", "Notes", true),
            ("CND_CandidateTags", "Notes", true),
            ("CND_Documents", "OriginalFileName", false),
        ];

        internal const string EmailHashCheck = "CK_CND_Candidates_EmailHash_Format";

        internal static string EnvelopeCheckName(string table, string column) => $"CK_{table}_{column}_Encrypted";

        /// <inheritdoc />
        /// <remarks>
        /// Only meaningful before the backfill: once values are encrypted, narrowing the columns
        /// back fails, and the rollback is restoring the pre-rollout backup instead.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"ALTER TABLE \"CND_Candidates\" DROP CONSTRAINT IF EXISTS \"{EmailHashCheck}\";");
            foreach (var (table, column, _) in EncryptedColumns)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" DROP CONSTRAINT IF EXISTS \"{EnvelopeCheckName(table, column)}\";");
            }

            migrationBuilder.DropIndex(
                name: "IX_CND_Candidates_EmailHash",
                table: "CND_Candidates");

            migrationBuilder.DropColumn(
                name: "EmailHash",
                table: "CND_Candidates");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "CND_Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Province",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CND_Candidates",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "CND_Candidates",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "CND_Candidates",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "CND_Candidates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Availability",
                table: "CND_Candidates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "CND_CandidateNotes",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Certification",
                table: "CND_CandidateLanguages",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Position",
                table: "CND_CandidateExperience",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Company",
                table: "CND_CandidateExperience",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Specialty",
                table: "CND_CandidateEducation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Institution",
                table: "CND_CandidateEducation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Degree",
                table: "CND_CandidateEducation",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_IsActive_LastName_FirstName_Id",
                table: "CND_Candidates",
                columns: new[] { "IsActive", "LastName", "FirstName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_LastName_FirstName",
                table: "CND_Candidates",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_Candidates_Name",
                table: "CND_Candidates",
                sql: "char_length(\"FirstName\") > 0 AND char_length(\"LastName\") > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_CandidateNotes_Body",
                table: "CND_CandidateNotes",
                sql: "char_length(btrim(\"Body\")) BETWEEN 1 AND 4000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_CandidateExperience_Company",
                table: "CND_CandidateExperience",
                sql: "char_length(\"Company\") > 0 AND char_length(\"Position\") > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CND_CandidateEducation_Degree",
                table: "CND_CandidateEducation",
                sql: "char_length(\"Degree\") > 0 AND char_length(\"Institution\") > 0");
        }
    }
}
