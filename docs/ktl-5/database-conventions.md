# KTL-5 database conventions

PostgreSQL is application-owned. EF Core migrations in `Infrastructure` are the only
schema-authoring mechanism for the new backend; existing Supabase SQL is historical intent
for features not migrated yet and is not replayed into this database.

## Physical table registry

| Prefix | Owner                                  | Examples                              |
| ------ | -------------------------------------- | ------------------------------------- |
| `CND_` | candidate/profile/document metadata    | `"CND_Candidates"`, `"CND_Documents"` |
| `CAT_` | business-maintained catalogs           | reserved for later slices             |
| `OPS_` | durable background/import/export state | `"OPS_Operations"`                    |
| `AUD_` | append-only audit records              | `"AUD_Events"`                        |
| `ADM_` | future administration/authorization    | reserved; authentication is deferred  |

Every prefixed physical identifier is explicitly quoted and therefore case-sensitive.
C# entity and property names remain idiomatic and do not repeat database prefixes. New
prefixes require an architecture decision and an update to this registry.

## Migration and runtime ownership

`ktl_migrator` owns deployment-time DDL and is used only by the explicit `--migrate`
command/container. The normal API and worker connect as `ktl_runtime`. The initial
migration revokes schema creation and grants only `SELECT`, `INSERT`, `UPDATE`, `DELETE`
plus required sequence usage on approved objects, and read-only access to EF's migration
history so readiness can reject a partially migrated deployment. The runtime role is not a database
owner, superuser, role creator, or RLS-bypass identity.

Authorization is enforced at the API/application actor boundary rather than by Supabase
RLS. Production business routes stay unregistered until the real identity adapter is
selected; a Development/Test actor cannot be enabled in Production.
