using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// KTL-19: records the actor on audit events, indexes the trail for its filters, makes it
    /// append-only to <c>ktl_runtime</c>, and grants <c>audit.read</c> to <c>system_admin</c>.
    /// </summary>
    /// <remarks>
    /// The actor columns are nullable so pre-existing rows keep no actor; no actor is inferred or
    /// backfilled for them. Once <c>DELETE</c> is revoked, purging old audit rows is a
    /// <c>ktl_migrator</c> job, not an application one (design D8).
    /// </remarks>
    public partial class AddAuditActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorKind",
                table: "AUD_Events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "AUD_Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AUD_Events_ActorUserId_CreatedAtUtc",
                table: "AUD_Events",
                columns: new[] { "ActorUserId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AUD_Events_CreatedAtUtc",
                table: "AUD_Events",
                column: "CreatedAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AUD_Events_EventType_CreatedAtUtc",
                table: "AUD_Events",
                columns: new[] { "EventType", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AUD_Events_SubjectId_CreatedAtUtc",
                table: "AUD_Events",
                columns: new[] { "SubjectId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AUD_Events_Actor",
                table: "AUD_Events",
                sql: "(\"ActorKind\" IS NULL AND \"ActorUserId\" IS NULL) OR (\"ActorKind\" = 'system' AND \"ActorUserId\" IS NULL) OR (\"ActorKind\" = 'user' AND \"ActorUserId\" IS NOT NULL)");

            // The trail becomes append-only to the application in the same migration that makes
            // it name actors (design D7): an attributed trail the runtime can still rewrite looks
            // authoritative and is not. Guarded like RevokeCandidateDelete.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE UPDATE, DELETE, TRUNCATE ON "AUD_Events" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);

            // audit.read is granted narrowly: to the seeded system_admin role only, never to the
            // recruiter roles. Kept sorted the way Role.ReplacePermissions stores it.
            migrationBuilder.Sql("""
                UPDATE "ADM_Roles"
                SET "Permissions" = (
                        SELECT jsonb_agg(value ORDER BY value)
                        FROM (SELECT jsonb_array_elements_text("Permissions") AS value
                              UNION SELECT 'audit.read') AS merged),
                    "UpdatedAtUtc" = NOW()
                WHERE "Name" = 'system_admin'
                  AND "IsSystem"
                  AND NOT "Permissions" ? 'audit.read';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Strip audit.read from every role: the previous API knows no such permission.
            migrationBuilder.Sql("""
                UPDATE "ADM_Roles"
                SET "Permissions" = "Permissions" - 'audit.read',
                    "UpdatedAtUtc" = NOW()
                WHERE "Permissions" ? 'audit.read'
                  AND jsonb_array_length("Permissions") > 1;
                """);

            // Restore the grant the previous code was written against, so a rollback returns the
            // prior state instead of leaving the old API unable to write (design, Migration Plan).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT UPDATE, DELETE ON "AUD_Events" TO ktl_runtime;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AUD_Events_ActorUserId_CreatedAtUtc",
                table: "AUD_Events");

            migrationBuilder.DropIndex(
                name: "IX_AUD_Events_CreatedAtUtc",
                table: "AUD_Events");

            migrationBuilder.DropIndex(
                name: "IX_AUD_Events_EventType_CreatedAtUtc",
                table: "AUD_Events");

            migrationBuilder.DropIndex(
                name: "IX_AUD_Events_SubjectId_CreatedAtUtc",
                table: "AUD_Events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AUD_Events_Actor",
                table: "AUD_Events");

            migrationBuilder.DropColumn(
                name: "ActorKind",
                table: "AUD_Events");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "AUD_Events");
        }
    }
}
