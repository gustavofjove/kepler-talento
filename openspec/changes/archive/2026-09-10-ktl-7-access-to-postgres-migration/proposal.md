## Why

The authoritative candidate dataset still lives in the legacy Access database
(`BBDD CVs.accdb`), while the SPA only looks populated because
[candidate.service.ts:173](src/app/features/candidates/services/candidate.service.ts#L173)
re-seeds demo data whenever `localStorage` is empty. Point the app at an empty PostgreSQL
and it shows an empty product, so every later per-slice cutover has nothing real to
validate against. Running the migration now also surfaces data-quality problems —
free-text values that resolve to no catalog entry, missing consent dates, malformed
contact details — while the schema can still be shaped around them, instead of after four
shipped slices have fixed it.

## What Changes

- **New candidate persistence schema.** KTL-7 runs ahead of KTL-8, so this change ships
  the full `CND_` candidate schema and its relation tables (languages, programs,
  education, experience, skills, documents) as an explicit migration. KTL-8 then adds
  write slices, endpoints and the frontend cutover on top of an existing schema rather
  than expanding it. **BREAKING** for the KTL-5 stub `Candidate` entity, which grows from
  8 fields to the full set.
- **A standalone migration CLI** (`backend/Tools/DataMigration`), run by an operator
  against a target database. Not an API endpoint, so the privileged whole-dataset path
  never becomes production request surface.
- **Extraction via a pre-exported intermediate format.** An operator exports the Access
  tables to UTF-8 delimited files on Windows using the documented procedure; the CLI
  consumes those. The Windows-only ACE/ODBC driver stays out of the tool, so CI exercises
  the same code path production does.
- **A staging representation** loaded from the export, validated, then loaded into the
  `CND_` and relation tables in one transaction per candidate aggregate. Staging state is
  destroyed after a successful load.
- **Idempotent re-runs** keyed on a stable source identifier per row, so re-running
  corrects rather than duplicates.
- **Reference-value resolution** mapping Access free-text language/program/skill/
  education/sector values onto `CAT_` catalog entries from KTL-6. Unmatched values are
  reported and require an explicit decision — never silently dropped, never silently
  auto-created.
- **Consent and retention metadata carried across exactly** (`receivedAt`, `consentAt`,
  `reviewDueAt`). A row whose consent metadata cannot be established is rejected and
  reported, never defaulted to a permissive value.
- **Document migration through the existing quarantine and scanning pipeline.** Migrated
  binaries are not trusted content: they pass the same allowlist, size, quarantine and
  scan gates as uploads, and only clean documents become available.
- **A reconciliation report** accounting for every source row as loaded, rejected or
  skipped, with per-entity counts, unresolved reference values, rejection reasons, and
  document hash verification against stored keys.
- **A documented and exercised rollback** returning PostgreSQL to its pre-migration state.

## Capabilities

### New Capabilities

- `candidate-persistence`: the full `CND_` candidate aggregate and relation schema in
  PostgreSQL — field set, constrained status value, logical-deletion state, consent and
  retention metadata, keys, constraints, indexes, and least-privilege runtime grants.
  Persistence only; the write slices and API surface belong to KTL-8.
- `legacy-data-migration`: the one-directional Access-to-PostgreSQL migration — export
  contract, staging, idempotent load, reference-value resolution, row rejection,
  reconciliation reporting, personal-data handling during migration, and rollback.

### Modified Capabilities

- `private-document-storage`: documents entering the system through migration, rather
  than through an interactive upload, must traverse the same allowlist, size, quarantine
  and scanning gates, and their stored hashes must be verifiable against the source.

## Impact

- **Backend — new:** `backend/Tools/DataMigration` (console project), staging and load
  services, reference resolver, reconciliation reporter.
- **Backend — modified:** `backend/Domain/Candidates/Candidate.cs` (full field set), new
  relation entities under `backend/Domain/Candidates/`, EF Core configurations and a new
  migration under `backend/Infrastructure/Persistence/`, `ApplicationDbContext`,
  `DatabaseInitializer` grants.
- **Depends on:** KTL-6 catalogs (`CAT_` entries must exist to resolve reference values),
  KTL-5 private document storage and scanning pipeline.
- **Sequencing:** KTL-8 is re-scoped to consume this schema instead of creating it.
- **Docs:** the Access export procedure, the migration runbook, and the rollback
  procedure; `docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md` gains a post-migration
  reconciliation check.
- **No frontend change.**

## Personal data and security

**This is the highest personal-data-exposure change in the sequence** — it moves the real
candidate dataset, including CV documents and consent metadata, into the new store.

- **Principle 1 (protected by design).** Consent and retention metadata is preserved
  exactly; rows lacking it are rejected, never defaulted. Logical-deletion state in the
  source is carried across as logical state, not as physical absence. The reconciliation
  report and all logs identify a rejected row by source identifier and field name only —
  never by field value. No candidate personal data reaches logs.
- **Principle 3 (least privilege, private storage).** Migrated documents land outside the
  webroot under opaque application-generated keys; no host path, drive letter or storage
  root appears in the report or in any output. Documents fail closed: unscannable or
  infected content never becomes available. The staging representation is destroyed after
  a successful load and is never left unprotected on disk. The migration CLI uses the
  migration database role, not the runtime role; the runtime role's grants ship with the
  same migration.
- **Development and test runs use synthetic fixtures only.** The production Access file is
  never used outside an operator-run production migration, and the entire process —
  including reconciliation and rollback — is provable against synthetic data in automated
  tests.

## Assumptions

- The Access export can be produced as UTF-8 delimited files with a stable per-row source
  identifier; where the source has no natural key, the export procedure defines one.
- KTL-6 catalog entries are seeded before the migration runs.
- Dataset size is small enough for an operator-run batch job; no incremental or resumable
  streaming is required.

## Success criteria

1. The migration runs end to end against a disposable PostgreSQL instance using synthetic
   fixtures, in automated tests.
2. Re-running the migration produces no duplicate rows.
3. The reconciliation report accounts for every source row as loaded, rejected or skipped,
   with a reason for each non-loaded row.
4. Unresolved catalog values are reported and require an explicit decision; none are
   auto-created silently.
5. Consent and retention metadata round-trips; rows lacking it are rejected.
6. Migrated documents exist in private storage under opaque keys, their stored hashes
   match the source, and no host path appears in any output.
7. No candidate personal data appears in logs or in the reconciliation report.
8. The rollback procedure is documented and exercised at least once in tests.
9. Backup/restore validation from KTL-5 still reconciles after the migration.
