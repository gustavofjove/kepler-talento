## Context

See `proposal.md` for motivation and `specs/candidate-import/spec.md` for the required behaviour.
This design covers how an import is built on the server and how the existing row semantics are
shared rather than reimplemented.

Current state:

- **The browser "import" writes nothing.** `import.service.ts` has a hand-rolled `parseCsv` /
  `parseCsvLine`, checks `first_name` and `last_name` for blankness and `email` against a regex,
  enforces `MAX_IMPORT_ROWS = 2000`, and calls `appendBatch` to push a record into
  `localStorage['rrhh.import.batches.v1']` with an id from `Math.random()`. `markCommitted` rewrites
  that record. `import-page.tsx:71` is the whole commit path. No candidate write exists anywhere in
  the feature.
- **`Tools/DataMigration` has the semantics.** `Validation/RowProblem.cs` defines
  `RowProblem(Entity, SourceKey, Field, ReasonCode)` with an explicit remark that the absence of a
  _value_ field is the control keeping personal data out of reports, and `StructuralProblem(File, Detail)`
  which may carry detail because it describes files and columns. `Loading/LoadResult.cs` defines
  `RowOutcome { Loaded, Rejected, Skipped }` and accumulates outcomes keyed by `(Entity, SourceKey)`.
  `Resolution/CatalogResolver.cs` defines `CatalogResolution`, `ResolutionStep { None, ExactName,
NormalizedCode, OperatorMapping }` and `UnresolvedValue(Family, Value, Occurrences)`.
- **The dependency direction is enforced.** `Tests/UnitTests/Architecture/ProjectDependencyTests.cs`
  fails the build if a production project references `Tools/`. `LoadResult.cs` already references
  `KeplerTalento.Domain.Operations`, so `Tools/` → `Domain`/`Application` is an established
  direction.
- **Durable operations exist.** `OPS_Operations` with `queued → running → completed|failed|cancelled`,
  lease-safe single ownership, restart recovery and idempotent execution
  (`openspec/specs/durable-operations/spec.md`). `ktl-migrate` is the worked example.
- **Private storage and scanning exist.** KTL-9's document path: opaque keys outside the webroot,
  ClamAV quarantine until `Clean`, terminal refusal of unclean content, size limits configured
  through `DocumentStorageOptions` and reconciliation via `--reconcile`.

## Goals / Non-Goals

**Goals:**

- Build the import on top of what already works — durable operations, private storage, the scanner,
  the candidate domain — rather than beside it.
- Have exactly one implementation of "what is a valid row" and "what does this value resolve to",
  used by both the operator migration and the API.
- Make the commit genuinely re-runnable, because a bulk write that cannot be safely retried will be
  retried anyway, by hand, at the worst possible moment.

**Non-Goals:**

- Parsing CVs or extracting data from documents.
- Changing the legacy Access export contract or how `ktl-migrate` is operated.
- Updating existing candidates from a file. This change creates; an import-driven update is a
  separate decision with separate risks.
- A general-purpose bulk-write framework. This is the candidate import.

## Decisions

### D1 — Extract the row semantics into `Application/Import/Rows`, and rewire `Tools`

`RowProblem`, `StructuralProblem`, `RowOutcome`, `LoadResult`, `CatalogResolution`,
`ResolutionStep` and `UnresolvedValue` move into `Application`. `Tools/DataMigration` keeps its
Access-specific readers, its staging schema and its reconciliation report, and consumes the moved
types. The direction is `Tools → Application`, which is already how `LoadResult` reaches
`Domain.Operations`, so the architecture test stays green without being touched.

The move is a **move**, not a copy. Two parallel definitions of "rejected with a reason code" would
drift within one ticket, and the drift would be silent because neither side would fail.

`RowProblem`'s missing value field is preserved verbatim, remark and all. A future reviewer adding a
`Value` parameter to make a report "more helpful" would be removing the control that keeps candidate
data out of the report, and the remark is what tells them so.

_Alternative considered:_ leaving the types in `Tools` and having the API define its own. Rejected —
that is the duplication the ticket exists to avoid, and it would put the personal-data control in
only one of the two places.

### D2 — `SourceKey` becomes the row number for imports

`RowProblem` is keyed by `(Entity, SourceKey)`, where the Access migration's source key is a stable
legacy identifier. An import file has no such key, so the import supplies the **1-based data row
number** as the source key, rendered as a string.

This keeps the shared type unchanged and gives the report the identifier a user actually needs
("row 47 failed"). It also means the key carries no personal data, which a natural key like the
email would.

### D3 — Two operations, not one, and the batch is the unit of idempotency

Validation and commit are separate durable operations against one `ADM_ImportBatches` row, which
carries the state machine:

```
uploaded → scanning → (infected|unscannable) terminal
         → scanned  → validating → validated → committing → committed
                                 → failed                 → failed
validated|committed|failed → expired   (file purged, counts retained)
```

The batch id is the idempotency key. A commit claims the batch by transitioning
`validated → committing` with an optimistic-concurrency check, so two concurrent commits cannot both
proceed and a restart finds the batch in `committing` and resumes it.

Within a commit, each row's write is idempotent through the row-outcome table: a row is marked
`Loaded` with the created candidate id **in the same transaction** as the candidate insert, so a
resumed commit skips rows already marked and never writes one twice. This is the same
per-record-transactional shape `ktl-migrate` uses, and it is why "no partial batch" is achievable
without a single giant transaction over 2000 rows.

_Alternative considered:_ one operation doing validate-then-commit. It collapses the two-step UX the
page already has and removes the human checkpoint between "here is what would happen" and "do it".

### D4 — Content hash recorded, but the batch id is the idempotency key

The upload records a SHA-256 of the file. It is **not** used to deduplicate automatically: uploading
the same file twice on purpose is a legitimate act (a corrected re-run after fixing catalog values),
and silently doing nothing would be the wrong answer.

Instead the hash surfaces in the batch list, so the page can warn that an identical file was
imported on a given date, and the actor decides. The spec's "same file uploaded twice deliberately"
scenario is satisfied by the documented duplicate rule at the _row_ level (D5), not by refusing the
upload.

### D5 — Duplicate rows are `Skipped` with a reason, not `Rejected`

A row describing a candidate the contract considers already present — within the same file, or
already in the database — ends as **`Skipped`** with reason `candidate.duplicate`, not `Rejected`.
`Skipped` means "understood and deliberately not loaded"; `Rejected` means "could not be understood".
Conflating them would make a re-run of an already-committed file look like 2000 validation failures.

The duplicate rule itself — which columns identify the same person — is part of the documented file
contract, so it is reviewable rather than implicit.

### D6 — Import files reuse the storage and scanner path with their own allowlist

Import files go through the same private-storage and ClamAV infrastructure as CVs, under opaque
keys outside the webroot, quarantined until `Clean`. What differs is configuration, not mechanism:
a separate allowlist (`text/csv`, and the spreadsheet type if D8 lands), a separate size limit, and
a separate purge schedule.

Content sniffing matters more here than for CVs: a `.csv` that is actually a spreadsheet macro
container is exactly the file an attacker sends to an HR inbox. The spec requires refusing a file
whose content does not match its declared type, and that check runs after the scan, before parsing.

### D7 — The uploaded file is purged 30 days after the batch reaches a terminal state

An import file is a bulk collection of candidate identity and contact data — a far larger exposure
per file than a single CV. It is kept only as long as someone might need to re-examine what was
imported, then removed. The counts and row outcomes survive the purge, because they carry no
personal data and are what an auditor actually needs.

Thirty days is a starting value in configuration, documented in the runbook, not a constant in code.
The purge runs as a durable operation on the same footing as `--reconcile`.

### D8 — CSV only in this change; Excel is a later follow-up

**Decided: CSV only.** CSV needs no dependency; Excel needs one (ClosedXML or similar), and
non-negotiable 2 requires a documented reason for every new runtime dependency. The interesting work
— scanning, durable commits, row outcomes, resolution — is format-independent, and a spreadsheet
parser is the part most likely to introduce a parsing vulnerability on an attacker-supplied file.

The file-type abstraction is an `IImportRowReader` chosen by content type, with one implementation
for now. Adding Excel later is one more implementation, one dependency and no change to the
pipeline, the storage model or the spec — which is why deferring it costs nothing.

The documented contract therefore accepts `text/csv` only, and the allowlist in D6 has one entry.

### D9 — Parsing happens in the API process, streamed, never fully buffered

A 2000-row CSV is small, but the maximum file size — not the row count — is what an attacker
controls. The reader streams and stops at the documented row and byte limits rather than reading the
file into memory and then checking. The row limit is enforced server-side; `MAX_IMPORT_ROWS` leaves
the browser entirely, since a limit the client enforces is not a limit.

### D10 — The frontend keeps its two-step shape and gains polling

`import.service.ts` becomes an API gateway: `upload(file)` returns a batch, `getBatch(id)` reports
state, `validate(id)` and `commit(id)` start operations, `getRowReport(id)` pages the outcomes. The
page polls `getBatch` while the batch is in a transient state (`scanning`, `validating`,
`committing`) with a bounded interval and a give-up.

Its CSV parser, validation rules, `localStorage` store and `Math.random()` id generator are deleted
rather than kept as a fallback. A fallback here would mean a screen that sometimes validates against
the real rules and sometimes against the old stub ones.

All the Spanish copy currently inline in `import-page.tsx` and in the thrown `AppError` messages
moves to `es.json`, and the file leaves `LEGACY_HARDCODED_COPY`.

## Risks / Trade-offs

- **The extraction in D1 is half-done.** If some types move and others are copied, the API and
  `ktl-migrate` will disagree about what a valid row is, silently. → The move is a single task with
  a compile-error-driven check: `Tools/DataMigration` must not define any of the moved types
  afterwards, asserted by a test that greps the namespace.
- **"No partial batch" is the hardest requirement here.** → D3's per-row transactional marking is
  the mechanism, and the interrupted-commit test kills the process mid-batch rather than simulating
  it, because a simulated interruption tests the simulation.
- **An import is an unauthenticated-looking bulk write path.** A caller who can import can create
  2000 candidates in one call. → It is guarded by its own permission, held by no default role except
  the administrative ones, every batch records its actor, and the row limit is server-side.
- **Attacker-supplied files.** The scanner is the first control, content sniffing the second, and
  parser limits the third. → D8's recommendation to defer Excel is partly this: a spreadsheet parser
  on hostile input is a larger attack surface than a CSV reader.
- **Purging loses evidence.** After 30 days, what exactly was in a file is unrecoverable. → That is
  the intended trade for not retaining bulk personal data indefinitely; the counts and reason codes
  survive, and the runbook says plainly what is lost and when.
- **Scanner unavailability blocks imports.** ClamAV needs ~3 GiB and can be degraded. → Deliberate:
  the spec refuses an unscannable file rather than admitting it. The page must say _why_ the batch
  is stuck rather than appearing to hang, and `/api/health/scanner` already reports the condition.
- **The page's apparent behaviour changes for the better, which will read as a regression.**
  Committing now really creates candidates and now really takes time and can really fail. Users who
  learned that "Confirmar commit" always succeeds instantly will file bugs. → Release notes.

## Migration Plan

1. `AddImportBatches` migration: `ADM_ImportBatches` and `ADM_ImportRowOutcomes` with their
   constraints, indexes and `ktl_runtime` grants — `SELECT, INSERT, UPDATE` on batches,
   `SELECT, INSERT` on row outcomes and no `UPDATE`/`DELETE`, since an outcome is a fact about a run.
   Runs through the existing `--migrate` entry point as `ktl_migrator`.
2. Deploy the API. The endpoints exist but nothing calls them yet.
3. Deploy the SPA. `evictSupersededStorage` removes `rrhh.import.batches.v1` on first load. Server
   batch history starts empty; the fabricated local history is not migrated (spec: _Entry describes
   work that never happened_).
4. Verify with a small file end to end: upload, scan, validate with deliberate row failures, fix,
   re-upload, commit, confirm the candidates exist and the audit events were written.

**Rollback.** The SPA rolls back to the previous build, whose import stub writes to `localStorage`
again — harmless, because it never wrote anything real. The API rolls back by redeploying the
previous image; `Down` drops both tables. Candidates already created by a committed import are
ordinary candidates and are **not** removed by the rollback, which is correct: they are real records
that real people may already have edited.

## Open Questions

- ~~The exact column set of the import file contract beyond the current `first_name`, `last_name`
  and `email`.~~ Resolved during implementation: the candidate core fields, the three consent and
  retention dates, and `languages` as the catalog-backed column. See
  `docs/ktl-17/import-file-contract.md`.

## Implementation notes

Decisions taken while implementing, recorded so the design stays true to the code.

- **Outcomes are per phase (refines D3).** `ADM_ImportRowOutcomes` carries a `Phase`
  (`validation` | `commit`), and the unique key is (batch, phase, row). Validation writes the dry-run
  outcomes; commit inserts its own. This keeps outcomes write-once — the runtime role has no
  `UPDATE` on the table — instead of "marking" a validation outcome `Loaded`.
- **Commit writes chunks of 100 rows per transaction (refines D3).** A candidate, its relations, its
  audit events and its outcome still always share one transaction; rows are simply grouped. One
  round trip per row made a 1500-row commit take minutes against PostgreSQL in Docker Desktop. If a
  chunk is refused it is rolled back whole and replayed one row per transaction, so the refused row
  is rejected with `candidate.refused` and the rest load. Resumption is unchanged: rows with a commit
  outcome are skipped.
- **Commit is refused while any row is rejected.** Fix-and-re-upload is the documented path; a
  partial commit would leave the rejected people to be re-imported into a file whose other rows are
  now duplicates.
- **`email` is required**, because the duplicate rule (D5) keys on it.
- **The shared resolver moved too (D1).** `CatalogResolver` itself moved to `Application/Import/Rows`
  alongside the result types; only `MappingFile` (the Access operator mapping) stayed in `Tools`.
  `LoadResult.ToCounts` stayed in `Tools` as an extension, because document counts are a migration
  concept.
- **Durable operation handlers are dispatched by type.** The worker previously knew only
  `document.scan`; it now resolves `IOperationHandler` by `Type`. The worker also renews a claim's
  lease through its own scope: renewing through the handler's `DbContext` failed any handler that ran
  longer than half a lease, which the interrupted-commit test exposed.
- **Recovery (4.4)** runs at start-up and on every maintenance tick, and re-queues a transient batch
  whose operation is missing, failed or cancelled under a new attempt number.
- **Import files are excluded from document reconciliation** (`--reconcile`) by their `imports/`
  prefix and are reconciled by the purge instead, which also removes orphan files and marks batches
  whose file has vanished.
- **Upload has no `.Accepts<IFormFile>` metadata.** That metadata makes routing answer 415 before
  authorization, which would disclose request-shape validation to an unauthorized caller.
- **The KTL-7 evidence test `No_endpoint_in_the_api_is_a_migration_endpoint`** forbade any endpoint
  type named "Import". It now allows exactly `ImportEndpoints` and additionally asserts the Web
  assembly references no migration tool assembly; the migration-endpoint prohibition is unchanged.
