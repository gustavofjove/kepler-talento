## Why

The Importación page looks like a working feature and is not one. `import.service.ts` parses a CSV
in the browser, checks that `first_name` and `last_name` are non-blank and that `email` matches a
regex, and writes a batch record to `localStorage`. **No candidate is ever created.** "Confirmar
commit" calls `markCommitted`, which rewrites that local record's status and recomputes `loadedRows`
without issuing a single write. No catalog reference is resolved, the file is never scanned because
it never leaves the browser, and `candidates.import` guards a page and nothing else.

So this change does not relocate an import. It builds one — on the server, with the per-row
semantics `Tools/DataMigration` already got right (brief: `openspec/KTL-17.md`).

## What Changes

- **BREAKING** Import moves to the API. The browser uploads a file and reads a report; it no longer
  parses, validates or decides anything.
- **BREAKING** The `localStorage` key `rrhh.import.batches.v1` is gone and is evicted on upgrade.
  The batch records it holds are fabrications — they describe candidates that were never created —
  so nothing is migrated out of it.
- **New capability: importing candidates actually creates them.** A committed batch writes
  candidates through the same domain rules as any other candidate write.
- The two-step shape the page already uses stays: upload and **validate as a dry run**, returning a
  per-row outcome report; then **commit** the validated batch as a separate, explicit call.
- Validation and commit run as durable `OPS_` operations, so a restart does not lose a batch, a
  commit is idempotent and re-runnable, and a commit interrupted partway leaves no partial batch.
- The row-outcome model and the reference-resolution rules are **extracted** from
  `Tools/DataMigration` into `Application` and shared by the operator tool and the API. Reference
  values are resolved, never silently created. No production project references `Tools/`.
- The uploaded file is treated as personal data: private storage under an opaque key, ClamAV scanned
  and quarantined until `Clean` before anything parses it, and purged on a documented schedule after
  the batch closes.
- Batch history and per-row outcomes move to server tables and are shown on the Importación page.
- Every import endpoint checks `candidates.import` after authentication and before validating or
  dispatching. This is the change that finally gives that permission something to guard, closing the
  exception KTL-16 recorded against it.

**Actors:**

- HR administrators holding `candidates.import`, who upload a file, review the report and commit.
- Unauthenticated callers, refused before validation; authenticated callers without
  `candidates.import`, refused as forbidden before validation.
- The scanner, which decides whether an uploaded file may be parsed at all.
- Operators, who read a batch's outcome without seeing the candidate data in it.

**Key entities:**

- **Import batch** — id, uploaded file's storage key, original filename, declared row count,
  lifecycle state (`uploaded`, `scanning`, `validating`, `validated`, `committing`, `committed`,
  `failed`, `expired`), counts of loaded/rejected/skipped, timestamps, the actor, a version.
- **Import row outcome** — batch id, source row number, outcome, failing field, stable reason code.
  Deliberately no field for the offending value.
- **Import file** — the uploaded binary in private storage, quarantined until clean, purged on
  schedule.
- **Durable operation** — the existing `OPS_Operations` record that carries validation and commit.

**Assumptions:**

- The import file contract is the one this change documents; the legacy Access export contract
  (`Tools/DataMigration`) stays separate and unchanged.
- The 2000-row limit carried over from the browser is the starting point and becomes a server-side
  contract value rather than a browser constant.
- Catalog values referenced by an import must already exist. An import never creates catalog
  vocabulary, matching the migration rule.
- A candidate created by import is an ordinary candidate: same validation, same audit events, same
  optimistic concurrency, no import-only fields.
- KTL-16 has shipped, so a real actor exists to own a batch and to guard the endpoints.

**Edge cases:**

- A file that is not CSV or Excel, or is one with the wrong extension.
- A file the scanner reports infected, or cannot scan, or is still scanning when commit is called.
- An empty file, a header-only file, a file with a missing required column, a file over the row
  limit, a file with duplicate rows.
- A row whose catalog reference resolves to nothing.
- Two rows in the same file describing the same person.
- A row describing a candidate who already exists.
- Commit called on a batch that was never validated, already committed, or has failing rows.
- The API restarting midway through a commit.
- The same file uploaded twice deliberately.
- A batch abandoned after validation and never committed.

**Success criteria:**

- Importing a CSV of N rows produces the same candidates whether it is committed once or re-run,
  and a restart mid-commit leaves no partial batch — proven by an interrupted-commit test.
- Every import endpoint refuses unauthenticated and unauthorized callers **before** validation,
  proven per endpoint.
- An uploaded file is never parsed before the scanner reports it clean, and an infected file is
  refused terminally.
- A row whose reference resolves to nothing is reported as a row failure, and no catalog value is
  created by any import run.
- The sum of loaded, rejected and skipped rows equals the file's row count for every batch — no row
  is unaccounted for.
- No candidate personal data from an imported file reaches any log or any row-outcome record.
- `rrhh.import.batches.v1` is absent after first run of the new build.
- The architecture test still shows no production project referencing `Tools/`.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Capabilities

### New Capabilities

- `candidate-import`: uploading an import file, scanning and quarantining it, validating it as a dry
  run with per-row outcomes, committing the batch idempotently as a durable operation, the batch and
  row-outcome history, the purge of the uploaded file, and the authorization that governs all of it.

### Modified Capabilities

- `frontend-api-transport`: the eviction list gains `rrhh.import.batches.v1`, and the incremental
  persistence cutover gains the import slice — which was the last feature service named as retaining
  its legacy data path.

## Impact

- **Backend:**
  - New `Application/Features/Import/` — upload, validate, commit, list batches, read a batch's row
    report — each a sealed record request, validator and handler repeating
    `ImportGuards.RequireImport(actor)`.
  - **Extraction:** `RowProblem`, `StructuralProblem`, `RowOutcome`, `LoadResult`,
    `CatalogResolution` and `ResolutionStep` move from `Tools/DataMigration/{Validation,Loading,Resolution}`
    into `Application`, and `Tools/DataMigration` is rewired to consume them. `RowProblem`'s
    deliberate absence of a value field is preserved — that absence is the control.
  - New `Domain/Import/` (`ImportBatch`, `ImportRowOutcome`, the batch state machine).
  - `Infrastructure/Persistence`: repositories, EF configurations, and a migration creating the
    batch and row-outcome tables with constraints, indexes and `ktl_runtime` grants.
  - `Infrastructure/Documents`: import files reuse the private-storage and ClamAV path with their own
    allowlist and their own purge schedule.
  - A durable-operation handler for validation and for commit, following `ktl-migrate`.
  - `Web/Features/Import/ImportEndpoints.cs` and its registration in `Program.cs`.
- **Frontend:**
  - `import.service.ts` becomes an API gateway: upload, poll the batch, read the report, commit. Its
    CSV parser, its validation rules, its `localStorage` batch store and `generateId` are deleted.
  - `import-page.tsx` keeps its two-step shape and gains batch history, scan state and a row report.
  - `import.models.ts` is restated against the API contract.
  - `evict-legacy-storage.ts` gains the key.
- **Tests:** backend unit tests for the extracted row semantics and the batch state machine;
  integration tests for authorization, scanning, idempotency and interrupted commits; Vitest specs
  for the rewritten service and page; a Playwright import journey; a `tests/security/` check.
- **Docs:** new `docs/ktl-17/` (file contract, row reason codes, batch lifecycle, purge schedule);
  `README.md` (Spanish); `AGENTS.md` loses the import path from the legacy `localStorage` note.
- **Dependencies:** Excel support, if it ships in this change, needs one new NuGet package with a
  documented reason. CSV needs none.
- **Personal data, storage and roles:**
  - This change touches personal data heavily: an import file is a bulk collection of candidate
    identity and contact details.
  - Principle 1 is upheld as follows: the file lives outside the webroot under an opaque key and no
    response reveals its path; row outcomes carry a row number, a field name and a reason code and
    never the offending value; no imported value reaches a log; the file is purged on a documented
    schedule rather than kept indefinitely; the report is behind the same permission as the import.
  - Principle 3 is upheld as follows: every endpoint checks authentication and `candidates.import`
    before validating; the file is quarantined until the scanner reports it clean; runtime grants on
    the new tables follow least privilege with no `DELETE` on outcomes; the browser reaches none of
    this except through the API.
  - No role definition changes — `candidates.import` already exists from KTL-16. What changes is
    that it now guards something.
  - No RLS policy changes.
