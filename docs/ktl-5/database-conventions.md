# KTL-5 database conventions

PostgreSQL is application-owned. EF Core migrations in `Infrastructure` are the only
schema-authoring mechanism for the new backend; existing Supabase SQL is historical intent
for features not migrated yet and is not replayed into this database.

## Physical table registry

| Prefix | Owner                                  | Examples                              |
| ------ | -------------------------------------- | ------------------------------------- |
| `CND_` | candidate/profile/document metadata    | `"CND_Candidates"`, `"CND_Documents"` |
| `CAT_` | business-maintained catalogs           | `"CAT_CatalogItems"` (KTL-6)          |
| `OPS_` | durable background/import/export state | `"OPS_Operations"`                    |
| `AUD_` | append-only audit records              | `"AUD_Events"`                        |
| `ADM_` | future administration/authorization    | reserved; authentication is deferred  |

Every prefixed physical identifier is explicitly quoted and therefore case-sensitive.
C# entity and property names remain idiomatic and do not repeat database prefixes. New
prefixes require an architecture decision and an update to this registry.

KTL-7 added the candidate relation tables, the migration-run history, and the composite
catalog foreign key. See [`../ktl-7/database-notes.md`](../ktl-7/database-notes.md).

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

## Encrypted columns (KTL-33)

Candidate free-text columns hold AES-GCM envelopes (`ktl1.<keyId>.<payload>`), not plaintext. See
[`../ktl-33/field-encryption.md`](../ktl-33/field-encryption.md). Rules for any slice touching them:

- Mark the property `.IsEncrypted(Table, limit)` in its configuration. The column becomes `text`;
  the length limit moves to `CandidateTextLimits` and the validators.
- Ship a `CK_<Table>_<Column>_Encrypted` check (`LIKE 'ktl1.%'`, `IS NULL OR` when nullable) in the
  same migration. Do not add `char_length` checks or indexes on encrypted columns: they would see
  ciphertext.
- Never filter, order, group, join or pattern-match on an encrypted column. `EncryptedColumnQueryGuard`
  throws when a query does. Equality on the e-mail uses `EmailHash`.
- Operator SQL cannot read these columns. Scripts that must recognise candidates by content decrypt
  with the key file (see `scripts/e2e-cleanup.js`) or go through `ktl-migrate encryption`.

# Position workflow ownership (KTL-15)

Recruitment workflow records use the registered `OPS_` prefix. `OPS_Positions` is owned by the
application and uses `pg_trgm` GIN indexes named `IX_OPS_Positions_NormalizedTitle_Trgm` and
`IX_OPS_Positions_NormalizedLocation_Trgm` for normalized contains filters. The runtime role has
only `SELECT`, `INSERT`, and `UPDATE`; `DELETE` and `TRUNCATE` are explicitly revoked. The
`pg_trgm` extension is shared infrastructure and is never removed by a feature rollback.

`OPS_PositionCandidates` (KTL-30) links candidates to positions with a stage. Both foreign keys
are `ON DELETE RESTRICT`, and the `(PositionId, CandidateId)` pair is unique. It is the one
position workflow table on which the runtime role holds `DELETE`, because removing a link is a
correction, not a retirement, and is audited. `TRUNCATE` stays revoked. Operator scripts must
delete link rows before the positions or candidates they reference.
