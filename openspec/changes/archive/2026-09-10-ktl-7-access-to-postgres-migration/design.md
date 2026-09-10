## Context

See `proposal.md` — Why. The constraints that shape this design:

- **KTL-7 runs ahead of KTL-8.** `backend/Domain/Candidates/Candidate.cs` is still the KTL-5
  stub (id, names, `IsActive`, timestamps, `DeletedAtUtc`, `Version`) and there are no
  relation tables at all. There is nothing to load into, so this change ships the schema.
- **KTL-6 catalogs are delivered and archived.** `business-catalogs` is a main spec,
  `CAT_CatalogItems` holds nine families in one table with a `Family` discriminator, and
  `CatalogName.DeriveCode` already normalizes a display name to a code. That normalization is
  the natural basis for reference-value resolution.
- **`CND_Documents` already exists** from KTL-5, carrying `CandidateId`, an opaque
  `StorageKey`, `Sha256`, scan state and a row version. Document migration extends that table
  rather than introducing a second one.
- **The document pipeline exists.** `FileSystemDocumentStorage` already writes to quarantine
  under an opaque `DocumentStorageKey`, returns a SHA-256 of what it wrote, and
  `PromoteAsync` moves clean content to available storage. `ClamAvScanner` and
  `FakeMalwareScanner` are both registered. Migration needs no new storage machinery — only a
  second caller of the existing one.
- **Durable operations exist** (`OPS_` tables, `DurableOperationWorker`, leases, retries), so
  a worker-hosted migration was genuinely available as an option. It was not chosen; see
  Decisions.
- **Integration tests already run real PostgreSQL** via `PostgreSqlFixture`. The migration
  test suite reuses it rather than inventing a harness.
- The ACE/OLEDB provider that reads `.accdb` is Windows-only and is not installable in the
  Linux containers CI runs in.

## Goals / Non-Goals

**Goals:**

- Make the whole migration — extract contract, load, resolution, reconciliation, rollback —
  demonstrable end to end in CI against synthetic fixtures, with no Windows dependency and no
  production data.
- Leave a candidate schema that KTL-8 can build write slices on without a second migration
  over already-loaded personal data.
- Keep the privileged bulk-data path out of the API's request surface entirely.

**Non-Goals:**

- A general-purpose ETL framework, mapping DSL, or plugin model. One source, one target, one
  shape.
- Streaming or resumable partial runs. The dataset is an internal HR CV database; a run that
  fails is rolled back and re-run.
- Any change to how candidates are read or written by the application. This change creates
  tables and fills them; `CandidateReader` and the KTL-5 reference slice are untouched.

## Decisions

### KTL-7 ships the candidate schema; KTL-8 is re-scoped

This change creates `CND_Candidates` (expanded) plus `CND_CandidateLanguages`,
`CND_CandidatePrograms`, `CND_CandidateEducation`, `CND_CandidateExperience` and
`CND_CandidateSkills`, extends the existing `CND_Documents`, and ships their constraints,
indexes and runtime grants in one EF Core migration. KTL-8 then delivers only the write
slices, endpoints, auditing, and frontend cutover.

**Alternative considered — run KTL-8's schema work first, keep KTL-7 pure ETL.** Rejected: it
inverts the dependency the ticket was resequenced to create. KTL-8's value is a validated
cutover against real data, and its acceptance criteria (consent round-trip, logical deletion
excluded from reads) are far easier to prove when real rows already exist.

**Alternative considered — migrate only the columns Access populates, extend later.**
Rejected: the second migration would run over the full production candidate dataset, which is
exactly the operation this design is trying to make rare and reviewable.

**Already reflected downstream:** the KTL-8 change (`ktl-8-candidate-core-writes-api-cutover`) has
since been planned against this decision. Its proposal names KTL-7 a hard prerequisite,
states "no new tables — this change consumes the `CND_` schema, constraints, indexes and
runtime grants delivered by KTL-7", and scopes itself to slices, endpoints, auditing and the
frontend cutover. Nothing about KTL-8 needs re-scoping; what this change owes it is a schema
that satisfies its `candidate-management` spec — see "What KTL-8 expects of this schema".

**Consequence to handle:** `DatabaseInitializer.SeedSyntheticReferenceAsync` constructs a
`Candidate` with four arguments. The expanded constructor requires consent metadata and status,
so the seed and `GetReferenceCandidateTests` are updated in this change.

### Catalog references are enforced by composite foreign key, not by convention

Relation rows reference `CAT_CatalogItems.Id`. A plain foreign key cannot stop a language
relation from pointing at a `sector` entry. Each relation table therefore carries a `Family`
column fixed by a check constraint to its one legal value, and the foreign key is composite —
`(LanguageId, LanguageFamily) → CAT_CatalogItems (Id, Family)` — backed by a unique index on
`CAT_CatalogItems (Id, Family)`.

**Alternative considered — application-level validation only.** Rejected: the migration is a
bulk writer that bypasses the application's slice validation, and principle 3 puts
business-critical invariants in the database.

**Note:** free-text fields that are genuinely free text in the product — `degree`,
`institution`, `company`, `position`, `functions`, `notes` — stay free text. Only the seven
catalog-backed fields are constrained.

### Documents extend `CND_Documents`; no second document table

KTL-5 already ships `CandidateDocument` / `CND_Documents` with `CandidateId`, a unique opaque
`StorageKey`, `Sha256`, `ScanState`, a size check constraint and a row version — which is
exactly the record a migrated document produces. This change adds `DocumentType`, `IsPrimary`
and `SourceKey` to it rather than creating a `CND_CandidateDocuments` alongside.

**Alternative considered — a separate metadata table linking to `CND_Documents`.** Rejected:
two tables for one document means two places for scan state to disagree, and a candidate's
document collection would have to join through a link row that carries nothing the document
row could not.

`IsPrimary` gets a partial unique index (`WHERE "IsPrimary"`), so "at most one primary
document per candidate" — which KTL-8's `candidate-management` spec requires — is a database
invariant rather than something each write slice has to remember.

### What KTL-8 expects of this schema

KTL-8's `candidate-management` spec is already written, so the schema has a known consumer.
The obligations it places here, all satisfied above: the closed status set as a database
constraint; consent and retention metadata that stays absent when absent; logical removal
that keeps a candidate retrievable and its relations intact; a concurrency token on the
candidate that relation-collection writes can advance; relation values that cannot reference
a catalog entry of the wrong family; and at most one primary document per candidate. Anything
KTL-8 needs that this schema does not provide is a defect in this change, not a KTL-8 task.

**Verified against the delivered schema.** Each obligation is asserted by a passing test in
`CandidateSchemaTests`: the status check constraint, consent metadata staying absent, logical
removal preserving the candidate and its relations, the `Version` row version carried forward
from KTL-5, the composite catalog foreign key, and the partial unique index for the primary
document. The three requirements KTL-8 owns outright — per-operation authorization, auditing,
and the error contract — need no schema support beyond this. No gap was found.

### Relationship to the existing in-app "Importación" feature

`src/app/features/admin/import/` presents an import screen, and its name invites the
question of whether this change duplicates it. It does not, for two independent reasons.

First, that feature does not import. `ImportService.validateCsvContent` parses a CSV, checks
`first_name` / `last_name` / `email`, and appends a batch record to `localStorage`; its
`loadedRows` is `totalRows - erroredRows.size`, arithmetic rather than a count of rows
written. The screen's commit flips a status string. The Supabase edge function it pairs with
returns zeroed counts. No candidate is created anywhere in that path. It is a validation
shell that reports a load it never performs — worth knowing about independently of this
change, and out of scope to fix here.

Second, even a working version of it could not carry this dataset: three meaningful columns,
a 2,000-row cap, browser-side, with no relations, documents, catalog resolution, consent
metadata, or reconciliation. Routing the whole candidate dataset through a browser is what
principle 2 forbids.

**Naming:** the runbook must say plainly that the KTL-7 tool is not the admin screen, or an
operator will go looking for it there.

**Left open for the import ticket:** the Stack Blueprint's adoption step 7 turns imports into
durable server-side operations with `OPS_ImportBatches` / `OPS_ImportErrors`, on a
dry-run-to-commit contract that resembles this tool's `validate` / `load`. Two histories for
"bulk row loading" is a smell. This change does not pre-empt that decision — it records that
`OPS_MigrationRuns` and `OPS_ImportBatches` should be reconciled when the import ticket is
planned, rather than allowed to diverge silently.

### A standalone CLI, not a durable worker operation

`backend/Tools/DataMigration` is a console project referencing `Infrastructure`. It is run by
an operator with an explicit connection string, export directory, and output path.

**Alternative considered — a durable operation on the KTL-5 worker.** The machinery fits well
and would let HR re-run without a deployment. Rejected on security surface: it requires an
authenticated endpoint that, when called, rewrites the entire candidate dataset. That is the
single most dangerous endpoint the product could have, for a benefit (self-service re-runs) HR
needs a handful of times ever. The durable-operations spec keeps covering scanning; nothing
about this decision closes the door if re-runs later prove frequent.

**Alternative considered — a CLI over a reusable library, structured for later worker
hosting.** Rejected as speculative. Should re-runs ever prove frequent enough to justify
hosting, the phases are ordinary classes and moving them costs a handler and an endpoint,
not a rewrite — but nothing is shaped for that eventuality today.

**Where the ETL lives:** in the tool project, not in `Infrastructure`. `Web` references
`Infrastructure`, so anything placed there is reachable in-process from an endpoint, and
this change chose a CLI precisely so that the whole-dataset path is not. The tool consumes
`Infrastructure` for persistence and document storage; nothing consumes the tool.
`ProjectDependencyTests` asserts that in both directions.

The CLI connects with the **migration** database role, never the runtime role. It has three
verbs: `validate` (read the export, resolve, report, write nothing), `load` (validate then
write), and `report` (re-emit the report for a recorded run).

### Extraction is a documented export, not a live Access read

The CLI reads a UTF-8 export set from a directory. `docs/ktl-7/access-export-procedure.md`
documents exactly how an operator produces it from `BBDD CVs.accdb` on Windows, including the
per-entity source-identifier column and, for documents, a manifest of relative paths with
SHA-256 hashes.

**Alternative considered — direct ACE/OLEDB read.** Rejected: it splits the code into a half
CI can exercise and a half it cannot, and the extraction half is where encoding and type
coercion bugs live. Requiring an export means CI runs the same reader production does.

**Alternative considered — a pluggable extractor with both.** Rejected as unused generality;
the ODBC branch would still be the untested one.

**Trade-off accepted:** one manual operator step, and the export becomes a personal-data
artifact the operator must handle and destroy. The procedure says so explicitly.

### Staging lives in PostgreSQL, in a dedicated schema, dropped on success

The CLI creates a `migration_staging` schema in the target database, copies the export into it
verbatim as text, runs validation and resolution as set-based queries there, then loads into
`CND_` tables. On a successful load the schema is dropped.

**Alternative considered — in-memory staging.** Rejected: reconciliation wants to compare
source and target counts by query, and the report needs per-row outcomes that survive a crash
long enough to be read.

**Alternative considered — temporary files on disk.** Rejected: it puts unprotected personal
data on the operator's filesystem, which the ticket forbids. The database is already the
protected location, with its own access control and its own backup story.

On failure the schema is retained for diagnosis, and the CLI's final line tells the operator it
exists and must be dropped. Retention is never silent.

### Idempotency: a source key column, unique per entity

Every `CND_` table gains a nullable `SourceKey` text column with a unique partial index (`WHERE
"SourceKey" IS NOT NULL`). Load is an upsert on `SourceKey`. Rows created later by the
application have no source key and are never touched by a re-run.

**Alternative considered — deriving deterministic GUIDs from the source key.** Rejected: it
hides the provenance the reconciliation report needs to cite, and a hash collision or a source
key change becomes undiagnosable.

A candidate and all of its relation rows are written in **one transaction per candidate**. A
relation failure rejects the whole candidate; nothing partial persists.

### A newer export must not silently undo work done in the application

`SourceKey` alone makes a re-run idempotent, but idempotent is not the same as safe. Between
this change and KTL-8's cutover, HR keeps using Access, so a second and newer export is the
normal path, not an exception. After the cutover, a re-run over a newer export would
overwrite every candidate a user had edited through the app — with an older value, and
without the row version noticing, because the migration writes directly rather than through
a slice.

`CND_Candidates` therefore carries `SourceLoadedAtUtc` alongside `SourceKey`, stamped with
the same instant the load stamps `UpdatedAtUtc`. Any later application write moves
`UpdatedAtUtc` past it, so `UpdatedAtUtc > SourceLoadedAtUtc` means "something other than the
migration wrote this". A re-run reports those rows and **skips** them; overwriting requires
an explicit `--overwrite-app-edits`. A check constraint keeps the two columns honest: a row
cannot carry a load moment without the source key that says where it came from.

The stamp lives on the candidate only. KTL-8 writes relation collections against the owning
candidate's concurrency token and advances the candidate with them, so the aggregate's
timestamp already covers edits to its collections — and the load is per-candidate
transactional anyway.

**Alternative considered — block all re-runs once the cutover has happened.** A one-way
latch is simpler and needs no column, but it also blocks the corrective re-runs this ticket
exists to support, and pushes the problem into KTL-8.

**Alternative considered — report the overwrite after the fact.** Cheaper, but the report is
read after the damage; skipping by default and overwriting on request puts the decision
before the write.

### Records the export no longer contains are reported, never deleted

A re-run accounts for source rows. It says nothing about the opposite direction: a candidate
deleted in Access still has a row in PostgreSQL, matched by nothing in the new export. The
run reports these as **unmatched target records**, counted separately from rejections and
skips, and changes nothing about them.

Automatic deactivation was considered and rejected. An export query that accidentally omits
a `WHERE` clause would become a mass deactivation of live candidates, and the standing domain
rule keeps removal a deliberate act. Reporting gives the operator the same information
without giving a malformed export that power.

### Reference resolution: exact, then normalized, then operator mapping — never automatic

For each catalog-backed source value, in order:

1. exact ordinal match on an existing entry's `Name`;
2. match on `CatalogName.DeriveCode(value)` against existing entry codes — the same
   normalization the catalog itself uses, so it introduces no new matching semantics;
3. an operator-supplied mapping file (`mappings.csv`: family, source value, target catalog
   code) passed on the command line.

Anything still unresolved is reported with its family and occurrence count, and every row
carrying it is **rejected**, not loaded with the field blank. No fuzzy or similarity matching
is applied: a near-match that silently picks the wrong skill is worse than a rejection an
operator resolves in the mapping file.

The mapping file is the "explicit decision" the spec requires. Adding a genuinely new catalog
value is done by editing catalogs through the KTL-6 admin screens, then re-running — the
migration never inserts into `CAT_CatalogItems`.

### Documents reuse the existing pipeline, synchronously, per document

For each manifest entry the CLI streams the file into `WriteQuarantineAsync` (which returns the
SHA-256 it computed), compares that to the manifest hash, runs the registered
`IMalwareScanner`, and calls `PromoteAsync` on a clean result, driving the existing
`CandidateDocument.MarkClean` / `MarkUnavailable` transitions rather than setting scan state
directly. Any mismatch, rejection, or scan
failure rejects the document row and is reported by document source key only.

Scanning runs inline rather than through the durable worker: the CLI is a foreground operator
action that must report a final reconciled state, and inline scanning is what makes "loaded"
mean "actually available". `ClamAvScanner` is used in production runs, `FakeMalwareScanner` in
tests, exactly as elsewhere.

### Reconciliation report: machine-readable plus human-readable, both PII-free

Each run writes `reconciliation-<runId>.json` and a `.md` rendering of it. Contents: per-entity
source / loaded / rejected / skipped counts, rejections as `(entity, sourceKey, field,
reasonCode)`, unresolved values as `(family, value, occurrences)`, and document hash results as
`(documentSourceKey, matched|mismatched|rejected, reasonCode)`.

The unresolved-values list is the one place a source _value_ appears, and deliberately so — an
operator cannot write the mapping file without it. Those values are catalog vocabulary
(languages, sectors, skills), not candidate personal data. Everything else cites identifiers
and field names only. The report writer's API accepts no free-form value parameter for
rejections, so there is no path by which a field value can reach it.

A run record goes to `OPS_MigrationRuns` (run id, started/finished, verb, counts, outcome, the
pre-migration backup label) so `report` can re-emit and so rollback has something to name.

### Rollback is restore-from-backup

The documented rollback is: restore the pre-migration backup taken per
`docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md`. `load` refuses to run unless given
`--pre-migration-backup <label>`, which it records on the run; the report repeats the label in
its header, so the artifact that proves the migration also names the thing that undoes it.

**Alternative considered — a `rollback --run <id>` verb deleting that run's rows.** Rejected:
it needs a per-run change journal to revert _updates_ (not just inserts), which means retaining
staging indefinitely — contradicting the destroy-staging requirement — and it would be the only
code path in the product that physically deletes candidate rows, against the standing domain
rule. Restore is coarser, already documented, already exercised by KTL-5's backup validation,
and correct.

Rollback is exercised in an integration test: snapshot → migrate → restore → assert the schema
state matches the snapshot and a subsequent `load` proceeds from it.

### Proving no personal data leaks

Synthetic fixtures embed sentinel strings in every personal-data field. An integration test
runs a migration whose fixture set includes rows rejected on each reason code, captures the
CLI's log output and both report files, and asserts no sentinel, storage key, or filesystem
path appears in any of them. This is the test that makes acceptance criterion 7 real rather
than aspirational.

## Risks / Trade-offs

- **The export step is manual and outside CI.** → The procedure is documented with exact
  columns and encoding; `validate` fails loudly and specifically on any structural deviation,
  so a malformed export is caught before any write.
- **The export set is unprotected personal data on an operator's machine.** → The procedure
  requires it be produced in a protected location and destroyed after a reconciled run; the
  report's closing checklist repeats it. This is a process control, not a technical one, and
  is the main residual exposure of the chosen extraction approach.
- **Rejecting a whole candidate for one unresolvable skill may reject many rows on the first
  run.** → That is the intended failure mode: `validate` is run first and reports every
  unresolved value at once, so the operator fills the mapping file in one pass rather than
  discovering values one run at a time.
- **The schema is being fixed before KTL-8 has written a line against it.** → It is derived
  from `candidate.models.ts`, which the shipped frontend already exercises, and KTL-8 remains
  free to add columns; what this avoids is a _migration over loaded personal data_, which
  additive columns do not require.
- **Inline ClamAV scanning makes a large document set slow.** → Acceptable for a one-time
  operator-run job; the CLI reports progress by count. If it ever becomes a problem, the
  documents phase is separable from the row load.
- **`SourceKey` is a permanent column carrying legacy provenance.** → It is nullable, indexed
  only where present, and is not personal data. It is what makes re-runs safe and the report
  citable, and is worth keeping after the migration.

## Migration Plan

1. Ship the schema migration and the CLI. Existing environments apply the migration as the
   normal explicit deployment action; no data moves.
2. Operator seeds/reviews catalogs through the KTL-6 admin screens.
3. Operator produces the export set per the documented procedure, on Windows.
4. Operator runs `validate`, reviews the reconciliation report, fills `mappings.csv`, repeats
   until unresolved values are zero and rejections are understood and accepted.
5. Operator takes the pre-migration backup per the runbook and notes its label.
6. Operator runs `load --pre-migration-backup <label>`, reviews the report, confirms counts
   reconcile and document hashes match.
7. Operator destroys the export set.
8. Backup/restore validation from KTL-5 is re-run against the migrated database.

**Rollback:** restore the backup named in the report header, then re-run from step 4.
