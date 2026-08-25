using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUD_Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUD_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CND_Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_Candidates", x => x.Id);
                    table.CheckConstraint("CK_CND_Candidates_Name", "char_length(\"FirstName\") > 0 AND char_length(\"LastName\") > 0");
                });

            migrationBuilder.CreateTable(
                name: "OPS_Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutcomeCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPS_Operations", x => x.Id);
                    table.CheckConstraint("CK_OPS_Operations_Attempts", "\"AttemptCount\" >= 0 AND \"MaxAttempts\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "CND_Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScanFailureCode = table.Column<string>(type: "text", nullable: true),
                    ScannerSignature = table.Column<string>(type: "text", nullable: true),
                    ScannedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CND_Documents", x => x.Id);
                    table.CheckConstraint("CK_CND_Documents_Size", "\"Size\" > 0 AND \"Size\" <= 20971520");
                    table.ForeignKey(
                        name: "FK_CND_Documents_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUD_Events_CorrelationId",
                table: "AUD_Events",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Candidates_LastName_FirstName",
                table: "CND_Candidates",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_CND_Documents_CandidateId",
                table: "CND_Documents",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CND_Documents_StorageKey",
                table: "CND_Documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OPS_Operations_IdempotencyKey",
                table: "OPS_Operations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OPS_Operations_Status_LeaseExpiresAtUtc",
                table: "OPS_Operations",
                columns: new[] { "Status", "LeaseExpiresAtUtc" });

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE CREATE ON SCHEMA public FROM ktl_runtime;
                        GRANT USAGE ON SCHEMA public TO ktl_runtime;
                        GRANT SELECT, INSERT, UPDATE, DELETE
                            ON "CND_Candidates", "CND_Documents", "OPS_Operations", "AUD_Events"
                            TO ktl_runtime;
                        GRANT SELECT ON "__EFMigrationsHistory" TO ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUD_Events");

            migrationBuilder.DropTable(
                name: "CND_Documents");

            migrationBuilder.DropTable(
                name: "OPS_Operations");

            migrationBuilder.DropTable(
                name: "CND_Candidates");
        }
    }
}
