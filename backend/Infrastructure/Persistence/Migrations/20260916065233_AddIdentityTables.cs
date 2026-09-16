using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates <c>ADM_Roles</c> and <c>ADM_Users</c>, seeds the five system roles, and grants the
    /// runtime role the DML it needs and nothing more.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grant is <c>SELECT, INSERT, UPDATE</c> and deliberately not <c>DELETE</c>: users and
    /// roles are deactivated, never deleted (non-negotiable 5), and the database enforcing that
    /// is a stronger guarantee than the application not offering it. <c>RevokeCandidateDelete</c>
    /// is the precedent; here the privilege is simply never granted in the first place.
    /// </para>
    /// <para>
    /// The five system roles are seeded here because they are fixed data with no configuration
    /// in them. The bootstrap administrator is not: it comes from
    /// <c>Authentication:BootstrapAdministrator</c>, which a migration cannot read, so the
    /// <c>--migrate</c> entry point seeds it after this runs and fails loudly when it is absent
    /// and no active administrator exists.
    /// </para>
    /// </remarks>
    public partial class AddIdentityTables : Migration
    {
        /// <summary>
        /// Fixed ids so re-running against a database that already holds the rows is a no-op
        /// rather than a duplicate. They are uuid v7 values generated once, not at migration time.
        /// </summary>
        private static readonly (string Id, string Name, string Label, string[] Permissions)[] SystemRoles =
        [
            ("01932f00-0000-7000-8000-000000000001", "rrhh_admin", "RRHH Admin",
            [
                "candidates.create", "candidates.delete", "candidates.export", "candidates.import",
                "candidates.read", "candidates.update", "catalogs.manage", "catalogs.read",
                "documents.download", "documents.upload", "presets.manage", "roles.manage",
                "users.manage",
            ]),
            ("01932f00-0000-7000-8000-000000000002", "rrhh_user", "RRHH User",
            [
                "candidates.create", "candidates.export", "candidates.read", "candidates.update",
                "catalogs.read", "documents.download", "documents.upload",
            ]),
            ("01932f00-0000-7000-8000-000000000003", "manager_reader", "Manager reader",
            [
                "candidates.read", "catalogs.read", "documents.download",
            ]),
            ("01932f00-0000-7000-8000-000000000004", "readonly", "Solo lectura",
            [
                "candidates.read", "catalogs.read",
            ]),
            ("01932f00-0000-7000-8000-000000000005", "system_admin", "System admin",
            [
                "candidates.read", "catalogs.manage", "catalogs.read", "presets.manage",
                "roles.manage", "users.manage",
            ]),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Permissions = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Roles", x => x.Id);
                    table.UniqueConstraint("AK_ADM_Roles_Name", x => x.Name);
                    table.CheckConstraint("CK_ADM_Roles_Label", "char_length(btrim(\"Label\")) > 0");
                    table.CheckConstraint("CK_ADM_Roles_Name", "\"Name\" ~ '^[a-z0-9_]+$'");
                    table.CheckConstraint("CK_ADM_Roles_Permissions", "jsonb_typeof(\"Permissions\") = 'array' AND jsonb_array_length(\"Permissions\") > 0");
                    table.CheckConstraint("CK_ADM_Roles_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                });

            migrationBuilder.CreateTable(
                name: "ADM_Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSignInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Users", x => x.Id);
                    table.CheckConstraint("CK_ADM_Users_DisplayName", "char_length(btrim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_ADM_Users_Email", "\"Email\" = lower(\"Email\") AND position('@' in \"Email\") > 1");
                    table.CheckConstraint("CK_ADM_Users_ExternalSubject", "\"ExternalSubject\" IS NULL OR char_length(btrim(\"ExternalSubject\")) > 0");
                    table.CheckConstraint("CK_ADM_Users_Timestamps", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"LastSignInAtUtc\" IS NULL OR \"LastSignInAtUtc\" >= \"CreatedAtUtc\")");
                    table.ForeignKey(
                        name: "FK_ADM_Users_ADM_Roles_RoleName",
                        column: x => x.RoleName,
                        principalTable: "ADM_Roles",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ADM_Roles_Name",
                table: "ADM_Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Users_RoleName",
                table: "ADM_Users",
                column: "RoleName");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_Users_Email",
                table: "ADM_Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_Users_ExternalSubject",
                table: "ADM_Users",
                column: "ExternalSubject",
                unique: true,
                filter: "\"ExternalSubject\" IS NOT NULL");

            // The permission array is written as a jsonb literal. It is sorted the same way
            // Role.ReplacePermissions sorts it, so a seeded row and an edited one are stored
            // identically and a diff of the column stays readable.
            foreach (var (id, name, label, permissions) in SystemRoles)
            {
                var permissionsJson = "[" + string.Join(",", permissions.Select(permission => $"\"{permission}\"")) + "]";
                migrationBuilder.Sql($"""
                    INSERT INTO "ADM_Roles"
                        ("Id", "Name", "Label", "IsSystem", "Permissions", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        ('{id}', '{name}', '{label}', TRUE, '{permissionsJson}'::jsonb, TRUE, NOW(), NOW())
                    ON CONFLICT ("Name") DO NOTHING;
                    """);
            }

            // SELECT, INSERT, UPDATE and no DELETE: see the remarks on this migration. Guarded
            // by the role's existence the way RevokeCandidateDelete is, so a developer database
            // created without the split roles still migrates.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        GRANT SELECT, INSERT, UPDATE ON "ADM_Users", "ADM_Roles" TO ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_Users");

            migrationBuilder.DropTable(
                name: "ADM_Roles");
        }
    }
}
