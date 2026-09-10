using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Takes DELETE on the candidate table away from the runtime role.
    /// </summary>
    /// <remarks>
    /// A candidate is removed logically and never physically, and KTL-8 makes that
    /// enforceable rather than merely intended: there is no DELETE verb anywhere in the
    /// candidate endpoint group, but "the application does not offer it" is a weaker
    /// guarantee than "the database will not permit it". The runtime role reached this
    /// point holding DELETE from the initial migration's blanket grant, which the
    /// candidate schema migration narrowed for the relation tables but not for
    /// <c>CND_Candidates</c> itself.
    ///
    /// This changes no schema — no table, column, constraint or index — so it does not
    /// re-own KTL-7's migration. It corrects a privilege.
    ///
    /// DELETE is deliberately retained on the relation and document tables: those
    /// collections are replaced as whole sets, so removing a language or detaching a
    /// document really does delete a row. What must never be destroyed is the candidate.
    /// </remarks>
    public partial class RevokeCandidateDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ktl_runtime') THEN
                        REVOKE DELETE, TRUNCATE ON "CND_Candidates" FROM ktl_runtime;
                        REVOKE TRUNCATE ON "CND_Documents" FROM ktl_runtime;
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
                        GRANT DELETE ON "CND_Candidates" TO ktl_runtime;
                    END IF;
                END
                $$;
                """);
        }
    }
}
