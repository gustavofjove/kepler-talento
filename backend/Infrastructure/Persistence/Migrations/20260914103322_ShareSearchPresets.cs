using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// KTL-14: saved searches become one shared, administrator-curated library.
    /// </summary>
    /// <remarks>
    /// Existing presets are deleted, not promoted into the library. Each was private to one
    /// person: its name and free-text filters may hold search terms that person never meant to
    /// share, and names collide between owners. The spec requirement "Existing private presets
    /// are discarded" and the KTL-14 release notes record this. Down cannot bring them back.
    ///
    /// Grants are deliberately untouched. ktl_runtime keeps exactly SELECT, INSERT, UPDATE and
    /// DELETE on this one table, as KTL-10 granted; nothing here broadens them.
    ///
    /// Repeatable in the project's sense: EF's migration history makes re-running it a no-op.
    /// </remarks>
    public partial class ShareSearchPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, before any structural change: an owner-scoped row must never exist in the
            // shared shape, not even for the duration of this transaction.
            migrationBuilder.Sql("""DELETE FROM "ADM_SearchPresets";""");

            migrationBuilder.DropIndex(
                name: "UX_ADM_SearchPresets_Owner_NormalizedName",
                table: "ADM_SearchPresets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ADM_SearchPresets_Owner",
                table: "ADM_SearchPresets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ADM_SearchPresets_Timestamps",
                table: "ADM_SearchPresets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ADM_SearchPresets");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ADM_SearchPresets",
                type: "integer",
                nullable: false,
                // Versions start at 1 (CK_ADM_SearchPresets_Version); the table is empty here, so
                // the default only has to be a value the check accepts.
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_SearchPresets_NormalizedName",
                table: "ADM_SearchPresets",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ADM_SearchPresets_Timestamps",
                table: "ADM_SearchPresets",
                sql: "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastUsedAtUtc\" IS NULL OR \"LastUsedAtUtc\" >= \"CreatedAtUtc\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ADM_SearchPresets_Version",
                table: "ADM_SearchPresets",
                sql: "\"Version\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Shared presets have no owner to give back, so the owner-scoped shape is restored
            // empty. The private presets deleted by Up are not recoverable.
            migrationBuilder.Sql("""DELETE FROM "ADM_SearchPresets";""");

            migrationBuilder.DropIndex(
                name: "UX_ADM_SearchPresets_NormalizedName",
                table: "ADM_SearchPresets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ADM_SearchPresets_Timestamps",
                table: "ADM_SearchPresets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ADM_SearchPresets_Version",
                table: "ADM_SearchPresets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ADM_SearchPresets");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "ADM_SearchPresets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_SearchPresets_Owner_NormalizedName",
                table: "ADM_SearchPresets",
                columns: new[] { "OwnerId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ADM_SearchPresets_Owner",
                table: "ADM_SearchPresets",
                sql: "char_length(\"OwnerId\") > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ADM_SearchPresets_Timestamps",
                table: "ADM_SearchPresets",
                sql: "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastUsedAtUtc\" IS NULL OR \"LastUsedAtUtc\" <= \"UpdatedAtUtc\")");
        }
    }
}
