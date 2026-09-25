using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OPS_PositionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPS_PositionCandidates", x => x.Id);
                    table.CheckConstraint("CK_OPS_PositionCandidates_Stage", "\"Stage\" IN ('new', 'shortlisted', 'interview', 'hired', 'rejected')");
                    table.CheckConstraint("CK_OPS_PositionCandidates_Timestamps", "\"UpdatedAtUtc\" >= \"AddedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_OPS_PositionCandidates_CND_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CND_Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OPS_PositionCandidates_OPS_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "OPS_Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OPS_PositionCandidates_CandidateId",
                table: "OPS_PositionCandidates",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "UX_OPS_PositionCandidates_PositionId_CandidateId",
                table: "OPS_PositionCandidates",
                columns: new[] { "PositionId", "CandidateId" },
                unique: true);

            // KTL-30 design D7: a link is correction-grade association data, so the runtime may
            // delete it. DELETE is scoped to this table; positions and candidates keep theirs revoked.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE ON "OPS_PositionCandidates" TO ktl_runtime;
                        REVOKE TRUNCATE ON "OPS_PositionCandidates" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE ALL ON "OPS_PositionCandidates" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);
            migrationBuilder.DropTable(
                name: "OPS_PositionCandidates");
        }
    }
}
