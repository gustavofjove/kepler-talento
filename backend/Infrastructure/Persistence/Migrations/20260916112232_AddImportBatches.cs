using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates <c>ADM_ImportBatches</c> and <c>ADM_ImportRowOutcomes</c> for the KTL-17 server-side
    /// candidate import, with their constraints, indexes and runtime grants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Batches get <c>SELECT, INSERT, UPDATE</c>: a batch moves through its state machine and its file
    /// is purged, but the record of the work is never removed. Row outcomes get only
    /// <c>SELECT, INSERT</c> — no <c>UPDATE</c> and no <c>DELETE</c> — because an outcome is a fact about a
    /// run: the commit writes a new row rather than rewriting the dry run's, and the unique key on
    /// (batch, phase, row) is what stops a resumed commit writing one twice.
    /// </para>
    /// <para>
    /// Neither table has a value column. Outcomes carry a row number, a column name and a reason
    /// code, which is the personal-data control, not an omission.
    /// </para>
    /// </remarks>
    public partial class AddImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorExternalKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RowCount = table.Column<int>(type: "integer", nullable: true),
                    LoadedRows = table.Column<int>(type: "integer", nullable: false),
                    RejectedRows = table.Column<int>(type: "integer", nullable: false),
                    SkippedRows = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureDetail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnresolvedValues = table.Column<string>(type: "jsonb", nullable: false),
                    OperationAttempt = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FilePurgedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_ImportBatches", x => x.Id);
                    table.CheckConstraint("CK_ADM_ImportBatches_Counts", "\"LoadedRows\" >= 0 AND \"RejectedRows\" >= 0 AND \"SkippedRows\" >= 0 AND \"OperationAttempt\" >= 0 AND (\"RowCount\" IS NULL AND \"LoadedRows\" + \"RejectedRows\" + \"SkippedRows\" = 0 OR \"RowCount\" >= 0 AND \"LoadedRows\" + \"RejectedRows\" + \"SkippedRows\" = \"RowCount\")");
                    table.CheckConstraint("CK_ADM_ImportBatches_Purge", "(\"State\" <> 'expired' OR \"FilePurgedAtUtc\" IS NOT NULL) AND (\"FilePurgedAtUtc\" IS NULL OR \"State\" IN ('expired', 'infected', 'unscannable'))");
                    table.CheckConstraint("CK_ADM_ImportBatches_Sha256", "\"Sha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_ADM_ImportBatches_SizeBytes", "\"SizeBytes\" > 0");
                    table.CheckConstraint("CK_ADM_ImportBatches_State", "\"State\" IN ('uploaded', 'scanning', 'scanned', 'validating', 'validated', 'committing', 'committed', 'infected', 'unscannable', 'failed', 'expired')");
                    table.CheckConstraint("CK_ADM_ImportBatches_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_ADM_ImportBatches_ADM_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "ADM_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ADM_ImportRowOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Field = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_ImportRowOutcomes", x => x.Id);
                    table.CheckConstraint("CK_ADM_ImportRowOutcomes_Candidate", "\"CandidateId\" IS NULL OR (\"Outcome\" = 'loaded' AND \"Phase\" = 'commit')");
                    table.CheckConstraint("CK_ADM_ImportRowOutcomes_Outcome", "\"Outcome\" IN ('loaded', 'rejected', 'skipped')");
                    table.CheckConstraint("CK_ADM_ImportRowOutcomes_Phase", "\"Phase\" IN ('validation', 'commit')");
                    table.CheckConstraint("CK_ADM_ImportRowOutcomes_ReasonCode", "(\"ReasonCode\" IS NULL OR \"ReasonCode\" IN ('field.required', 'field.too_long', 'email.invalid', 'date.invalid', 'status.unknown', 'reference.malformed', 'reference.unresolved', 'reference.duplicate', 'row.shape_invalid', 'candidate.refused', 'candidate.duplicate')) AND (\"Outcome\" = 'loaded' OR \"ReasonCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_ADM_ImportRowOutcomes_RowNumber", "\"RowNumber\" >= 1");
                    table.ForeignKey(
                        name: "FK_ADM_ImportRowOutcomes_ADM_ImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ADM_ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ADM_ImportRowOutcomes_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportBatches_CreatedAtUtc_Id",
                table: "ADM_ImportBatches",
                columns: new[] { "CreatedAtUtc", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportBatches_CreatedByUserId",
                table: "ADM_ImportBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportBatches_Sha256",
                table: "ADM_ImportBatches",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportBatches_State_ClosedAtUtc",
                table: "ADM_ImportBatches",
                columns: new[] { "State", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ADM_ImportBatches_StorageKey",
                table: "ADM_ImportBatches",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportRowOutcomes_BatchId_RowNumber",
                table: "ADM_ImportRowOutcomes",
                columns: new[] { "BatchId", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_ImportRowOutcomes_CandidateId",
                table: "ADM_ImportRowOutcomes",
                column: "CandidateId",
                filter: "\"CandidateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_ImportRowOutcomes_BatchId_Phase_RowNumber",
                table: "ADM_ImportRowOutcomes",
                columns: new[] { "BatchId", "Phase", "RowNumber" },
                unique: true);

            // Least privilege, guarded by the role's existence as the other grant migrations are, so
            // a developer database created without the split roles still migrates.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE ON "ADM_ImportBatches" TO ktl_runtime;
                        GRANT SELECT, INSERT ON "ADM_ImportRowOutcomes" TO ktl_runtime;
                        REVOKE DELETE, TRUNCATE ON "ADM_ImportBatches" FROM ktl_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON "ADM_ImportRowOutcomes" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_ImportRowOutcomes");

            migrationBuilder.DropTable(
                name: "ADM_ImportBatches");
        }
    }
}
