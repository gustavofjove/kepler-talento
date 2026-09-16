## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-17` from an up-to-date `main`, after KTL-16 has
      merged — this change needs a real actor to own a batch and to guard its endpoints
      (Waived by the user: KTL-16 is finished and the work continues on `feat/KTL-16`.)

## 1. Extract the shared row semantics (do this first, on its own)

Covers: Every row ends in exactly one outcome; Reference values are resolved, never created.
Design D1, D2.

- [x] 1.1 **Move** `RowProblem` and `StructuralProblem` from
      `Tools/DataMigration/Validation/RowProblem.cs` into `Application/Import/Rows/`, preserving the
      remark explaining that the absence of a value field is the control. Do not copy — the type must
      exist in exactly one place.
- [x] 1.2 Move `RowOutcome`, `LoadResult` and `UnmatchedTargetRecord` from
      `Tools/DataMigration/Loading/LoadResult.cs` into `Application/Import/Rows/`.
- [x] 1.3 Move `CatalogResolution`, `ResolutionStep` and `UnresolvedValue` from
      `Tools/DataMigration/Resolution/CatalogResolver.cs` into `Application/Import/Rows/`, leaving the
      Access-specific resolver behaviour in `Tools`.
- [x] 1.4 Rewire `Tools/DataMigration` to consume the moved types and confirm it still builds. The
      direction is `Tools → Application`, which `LoadResult` already used to reach `Domain.Operations`.
- [x] 1.5 Add a test asserting no moved type is defined under `Tools/`, so a future copy-paste
      reintroducing a parallel definition fails the build (design D1).
- [x] 1.6 Run `dotnet test backend/KeplerTalento.slnx --filter "FullyQualifiedName~Architecture"` and
      confirm no production project references `Tools/`.
- [x] 1.7 Run `npm run test:backend` and confirm the migration suite still passes unchanged.

## 2. Import domain and persistence

Covers: Batch history is server-owned; Validation and commit are durable, idempotent operations.

- [x] 2.1 Add `Domain/Import/ImportBatch.cs` with the state machine from design D3
      (`uploaded → scanning → scanned → validating → validated → committing → committed`, plus the
      `infected`, `unscannable`, `failed` and `expired` terminals), rejecting invalid transitions.
- [x] 2.2 Add `Domain/Import/ImportRowOutcome.cs`: batch id, 1-based source row number, outcome,
      failing field, reason code, and the created candidate id when loaded. **No value field.**
- [x] 2.3 Add `Domain/Import/ImportReasonCodes.cs` as a closed catalogue of stable reason codes,
      including `candidate.duplicate` for the skipped-duplicate rule (design D5).
- [x] 2.4 Add `IImportBatchRepository` to `Application/Abstractions`, with `ExpectVersion` and the
      claim transition that makes concurrent commits impossible (design D3).
- [x] 2.5 Add EF configurations for `ADM_ImportBatches` and `ADM_ImportRowOutcomes`: quoted
      identifiers, `Version` as `IsRowVersion()` over `xmin`, an index on batch id plus row number,
      a unique constraint preventing two outcomes for the same row, and check constraints on the
      state and outcome vocabularies.
- [x] 2.6 Generate the migration with
      `dotnet ef migrations add AddImportBatches --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`
      and extend it with grants: `SELECT, INSERT, UPDATE` on `ADM_ImportBatches`, `SELECT, INSERT`
      on `ADM_ImportRowOutcomes` and **no** `UPDATE`/`DELETE` there — an outcome is a fact about a
      run.
- [x] 2.7 Apply the migration against the local stack and inspect the schema, constraints and grants
      in PostgreSQL.

## 3. Upload, scanning and storage

Covers: Uploaded import files are private, scanned and quarantined; Documented import file contract.
Design D6, D9.

- [x] 3.1 Add import-file storage options: its own opaque-key prefix, its own size limit and its own
      allowlist containing `text/csv` only (design D8), reusing the KTL-9 private-storage mechanism
      rather than a second implementation.
- [x] 3.2 Implement the upload use case: store the file under an opaque key outside the webroot,
      record the SHA-256 (design D4), create the batch in `uploaded`, and queue the scan. The response
      carries the batch and reveals no storage key or path.
- [x] 3.3 Wire the ClamAV path so the batch moves `scanning → scanned` on `Clean`, and to the
      terminal `infected` or `unscannable` otherwise. Nothing may read the file before `scanned`.
- [x] 3.4 Add content sniffing after the scan and before parsing: refuse a file whose actual content
      is not the declared import type (design D6).
- [x] 3.5 Add `IImportRowReader` with a streaming CSV implementation that stops at the documented row
      and byte limits rather than buffering the file and checking afterwards (design D9). The row
      limit moves server-side; `MAX_IMPORT_ROWS` leaves the browser.

## 4. Validation and commit as durable operations

Covers: Import is a two-step operation; Validation and commit are durable, idempotent operations;
Imported candidates are ordinary candidates.

- [x] 4.1 Implement the validation operation: read rows, apply the field rules, resolve catalog
      references through the shared resolver, mark duplicates as `Skipped` with
      `candidate.duplicate` (design D5), and write one row outcome per data row so the three counts
      sum to the row count.
- [x] 4.2 Implement the commit operation following `ktl-migrate`: claim the batch
      `validated → committing` under optimistic concurrency, then per row write the candidate and
      mark the outcome `Loaded` **in the same transaction**, so a resumed commit skips what is
      already marked (design D3).
- [x] 4.3 Route candidate creation through the same domain rules as a direct candidate write — same
      validation, status constraints, consent and retention handling, and the same audit event. No
      import-only field and no bypassed rule.
- [x] 4.4 Add restart recovery: a batch found in `committing` on startup resumes rather than being
      abandoned or restarted from row one.
- [x] 4.5 Implement the purge operation removing an uploaded file 30 days after the batch reaches a
      terminal state, retaining counts and row outcomes, and leaving no orphan in either direction
      (design D7).

## 5. Import endpoints

Covers: Import authorization fails closed; Batch history is server-owned.

- [x] 5.1 Add `Application/Features/Import/ImportGuards.cs` (`RequireImport`) and the error types in
      `Application/Common/Errors` for the structural, state and duplicate refusals, each with a
      stable code.
- [x] 5.2 Add the use cases under `Application/Features/Import/`, one file per use case — upload,
      validate, commit, list batches, get batch, get row report — each a sealed record request, a
      FluentValidation validator and a sealed handler repeating the guard **first**.
- [x] 5.3 Add `Web/Features/Import/ImportEndpoints.cs` as a `MapGroup("/api/import")` extension with
      nested request records, `.WithName()` and explicit `.Produces*` metadata, dispatching through
      `ISender`; register it in `Program.cs`.
- [x] 5.4 Confirm every refusal surfaces through `GlobalExceptionHandler` as ProblemDetails with its
      stable code, and that a forbidden caller cannot distinguish an existing batch from a missing one.
- [x] 5.5 Extend `PersonalDataRedactionEnricher` to cover the original filename and any imported
      value, and confirm the row report carries no value.
      Covers: Imported personal data stays out of diagnostics.

## 6. Backend tests and security evidence

- [x] 6.1 Unit tests for the batch state machine: every valid transition, and every invalid one
      rejected.
- [x] 6.2 Unit tests for the shared row semantics after the move: counts sum to the row count, every
      rejected row carries a reason code, and no outcome carries a value.
- [x] 6.3 Integration tests per endpoint for unauthenticated and unauthorized callers, each sending a
      malformed body to prove the refusal precedes validation.
      Covers: Import authorization fails closed.
- [x] 6.4 Integration test proving an uploaded file is never parsed before the scanner reports it
      clean, and that an infected file is terminally refused and no retry admits it.
- [x] 6.5 Idempotency test: commit a batch of N rows twice and assert the same candidates exist, none
      duplicated.
- [x] 6.6 **Interrupted-commit test: kill the process partway through a commit** — not a simulated
      interruption — restart, and assert the resumed commit yields exactly the candidates of a single
      uninterrupted run, with no partial batch (design D3).
- [x] 6.7 Test proving no import run creates, renames or reactivates a catalog entry, by comparing the
      catalog before and after a run containing unresolved values.
- [x] 6.8 Test proving a row whose reference resolves to nothing is rejected and does not load without
      the value.
- [x] 6.9 Grants test: `ktl_runtime` has no `UPDATE` or `DELETE` on `ADM_ImportRowOutcomes` and no
      `DELETE` on `ADM_ImportBatches`, asserted by querying the catalog.
- [x] 6.10 Log assertions: no imported value, no original filename and no file content reaches the
      logs, on success and on every failure path.
- [x] 6.11 Purge test: a terminal batch past its window loses its file and keeps its counts and row
      outcomes, and no orphan remains.
- [x] 6.12 Review and update the existing `Tools/DataMigration` tests affected by the extraction.
- [x] 6.13 **Run** `npm run test:backend` with Docker running and inspect the output.

## 7. Frontend cutover

Covers: Batch history is server-owned; the transport eviction and cutover deltas.

- [x] 7.1 Rewrite `src/app/features/admin/import/import.service.ts` as an API gateway: `upload`,
      `getBatch`, `validate`, `commit`, `listBatches`, `getRowReport`. **Delete** `parseCsv`,
      `parseCsvLine`, `validateCsvContent`, `markCommitted`, `appendBatch`, `readBatches`,
      `writeBatches`, `generateId` and `MAX_IMPORT_ROWS`. No fallback to the old path.
- [x] 7.2 Restate `import.models.ts` against the API contract: batch state, counts, row outcomes with
      reason codes, scan state.
- [x] 7.3 Update `import-page.tsx`: keep the two-step shape, add batch history, scan state, a paged
      row report, and bounded polling with a give-up while a batch is in a transient state
      (design D10).
- [x] 7.4 Add `rrhh.import.batches.v1` to `SUPERSEDED_KEYS` in `evict-legacy-storage.ts` with a
      comment recording that the entries it held described candidates that were never created.
- [x] 7.5 Move every Spanish literal in `import-page.tsx` and the service's thrown errors into
      `src/assets/i18n/es.json` under flat `admin.import.*` keys, using whole sentences with
      interpolation and `formatNumber` for counts. Remove the file from `LEGACY_HARDCODED_COPY` in
      `eslint.config.js`. The list only shrinks.
- [x] 7.6 Keep the `name=` attribute and `data-testid` on every control the Playwright suite binds to.

## 8. Frontend tests

- [x] 8.1 Review and update the existing import unit tests — they assert the stub's behaviour and
      will be asserting the wrong thing after the rewrite.
- [x] 8.2 New specs for the rewritten service: upload, poll, validate, commit, and the error paths.
- [x] 8.3 Specs for the page: the two-step flow, the row report rendering, the scan-blocked state,
      and each distinct Spanish refusal message. Locate elements by role, accessible name or
      `data-testid`.
- [x] 8.4 Spec for `evictSupersededStorage` removing the import key.
- [x] 8.5 **Run** `npm test` and inspect the output.

## 9. End-to-end verification

- [x] 9.1 Add `tests/e2e/candidate-import.spec.ts`: upload a small CSV with deliberate row failures,
      read the report, upload a corrected file, commit, and confirm the candidates appear in the
      candidate list. No selector may hardcode Spanish text.
- [x] 9.2 **Run** `npm run e2e` with `docker compose up` running, inspect the output, and restore seed
      data afterwards.
- [x] 9.3 **Run** the existing candidate and navigation e2e specs to confirm nothing regressed.

## 10. Security gates and documentation

- [x] 10.1 Add `tests/security/ktl-17-import-boundary.spec.ts` asserting the import surface fails
      closed for unauthenticated and unauthorized callers and exposes no storage path.
- [x] 10.2 **Run** `npm run security:rls` and `npm run security:storage` and inspect the output.
- [x] 10.3 Write `docs/ktl-17/import-file-contract.md`: accepted type, required and optional columns,
      value formats, the duplicate rule (design D5), the row and size limits.
- [x] 10.4 Write `docs/ktl-17/row-reason-codes.md`: every stable reason code and what it means.
- [x] 10.5 Write `docs/ktl-17/runbook.md`: the batch lifecycle, the 30-day purge and what it destroys,
      what to do when the scanner is degraded, and how to recover a stuck batch.
- [x] 10.6 Write `docs/ktl-17/release-notes.md` stating plainly that the previous import created no
      candidates, that local batch history is discarded, and that committing now really writes and
      can really fail.
- [x] 10.7 Update `README.md` (Spanish) with how to run an import locally.
- [x] 10.8 Update `AGENTS.md`: remove the import path from the legacy `localStorage` note.
- [x] 10.9 Update `docs/ktl-16/authentication-and-authorization.md` — `candidates.import` now guards
      a real server-side operation, so its pending-permission exception is removed.

## 11. Done checks

- [x] 11.1 **Run** `npm run build:all` and confirm it is clean (warnings are errors).
- [x] 11.2 **Run** `npm run lint` and `npm run format:check`.
- [x] 11.3 **Run** `npm test` and `npm run test:backend` once more against the final tree and inspect
      both outputs.
- [x] 11.4 Confirm no `localStorage` or Supabase data path remains in the import feature, and that
      `rrhh.import.batches.v1` is absent after a first load of the new build.
- [x] 11.5 Run `openspec validate ktl-17-server-side-candidate-import --strict`.
