using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// KTL-10: the saved-search table, its constraints, its index and the runtime role's
    /// privileges on it.
    /// </summary>
    /// <remarks>
    /// One deployment action, and a repeatable one: EF's migration history makes re-running
    /// it a no-op, and nothing here depends on the state of the data.
    ///
    /// It deliberately adds no candidate, relation or document index. Every predicate
    /// server-side search issues is already served: KTL-7's
    /// IX_CND_Candidates_IsActive_UpdatedAtUtc covers the base scan and its ordering, the
    /// relation tables' composite catalog foreign keys already carry indexes led by the
    /// catalog identifier the criteria filter on, UX_CAT_CatalogItems_Family_NameNormalized
    /// covers criterion resolution, and KTL-9's partial
    /// UX_CND_Documents_CandidateId_Primary covers the primary-CV existence test. Wider
    /// (catalog, candidate, level) relation indexes were written, measured against the
    /// KTL-7-scale dataset, and removed again: the planner never chose one. The evidence is
    /// retained in docs/ktl-10/query-plans.md.
    ///
    /// There is likewise no pg_trgm or tsvector. Free text is a leading-wildcard literal
    /// substring, which no B-tree can serve; at the measured scale the parameterized scan
    /// costs single-digit milliseconds, far cheaper than a new extension's operational
    /// weight and write amplification. If that evidence stops holding, the design is amended
    /// before an index is added — not the other way round.
    /// </remarks>
    public partial class AddSearchPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_SearchPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Filters = table.Column<string>(type: "jsonb", nullable: false),
                    FilterSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_SearchPresets", x => x.Id);
                    table.CheckConstraint("CK_ADM_SearchPresets_Filters", "jsonb_typeof(\"Filters\") = 'object'");
                    table.CheckConstraint("CK_ADM_SearchPresets_FilterSchemaVersion", "\"FilterSchemaVersion\" >= 1");
                    table.CheckConstraint("CK_ADM_SearchPresets_Name", "char_length(btrim(\"Name\")) > 0 AND char_length(\"NormalizedName\") > 0");
                    table.CheckConstraint("CK_ADM_SearchPresets_Owner", "char_length(\"OwnerId\") > 0");
                    table.CheckConstraint("CK_ADM_SearchPresets_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastUsedAtUtc\" IS NULL OR \"LastUsedAtUtc\" <= \"UpdatedAtUtc\")");
                });

            migrationBuilder.CreateIndex(
                name: "UX_ADM_SearchPresets_Owner_NormalizedName",
                table: "ADM_SearchPresets",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);

            // The runtime role gets exactly the four verbs saved searches need on the one
            // table it owns, and nothing else: no TRUNCATE, no DDL, no role management and
            // no schema-wide default privileges. Applying the migration remains the separate
            // migration role's job — this statement grants, it does not delegate granting.
            //
            // Search itself needs no new privilege: it reads candidate, catalog, relation
            // and document tables the runtime role already holds SELECT on.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE ON "ADM_SearchPresets" TO ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revoke before dropping, so a role never keeps a privilege on a name something
            // else could later re-create.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE ALL ON "ADM_SearchPresets" FROM ktl_runtime;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "ADM_SearchPresets");
        }
    }
}
