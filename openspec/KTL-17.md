# KTL-17 — Move candidate import to the server as a durable, idempotent operation

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-16 (identity and access control — supplies `candidates.import` with a real
caller), KTL-8 (candidate writes), KTL-11 (catalog vocabulary), KTL-5 (durable operations, `OPS_`)

## Summary

This is workstream 2 of [KTL-16](./KTL-16.md), split out because it is a full slice on its own and
shares no code with the other two remaining workstreams ([KTL-18](./KTL-18.md),
[KTL-19](./KTL-19.md)).

CSV import runs entirely in the browser, and it does not actually import anything.
`src/app/features/admin/import/import.service.ts` parses the file, checks that `first_name` and
`last_name` are present and that `email` looks like an email, and then writes a batch record to
`localStorage` under `rrhh.import.batches.v1`. **No candidate is ever created.** "Confirmar commit"
calls `markCommitted`, which flips that local record's status and recomputes `loadedRows` — it
issues no write of any kind. There is no catalog reference resolution either.

So this ticket is not "move the import to the server". It is "build the import, on the server". The
existing page is a convincing-looking stub, and treating it as a working feature to be relocated
would badly underestimate the work.

`Tools/DataMigration` (`ktl-migrate`) already does this work correctly against PostgreSQL —
per-row outcomes, reference resolution that never silently creates values, idempotent re-runs. The
job is to move the browser's import onto that footing without letting a production project reference
`Tools/`.

## Context — what exists today

| Concern         | Today                                                                                        | Evidence                                                  |
| --------------- | -------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| Parsing         | A hand-rolled CSV splitter in the browser; no Excel support                                  | `import.service.ts` `parseCsv` / `parseCsvLine`           |
| Validation      | `first_name` and `last_name` non-blank, `email` matches a regex. Nothing else                | `import.service.ts` `validateCsvContent`                  |
| References      | Not resolved at all — no catalog column is read                                              | same file                                                 |
| Commit          | **Creates no candidate.** `markCommitted` rewrites a local record                            | `import.service.ts` `markCommitted`, `import-page.tsx:71` |
| Batch history   | `localStorage` key `rrhh.import.batches.v1`, ids from `Math.random()`                        | `import.service.ts` `appendBatch` / `generateId`          |
| Row limit       | 2000, enforced in the browser                                                                | `MAX_IMPORT_ROWS`                                         |
| Permission      | `candidates.import` gates the page and nothing else                                          | KTL-16 permission table                                   |
| The file itself | Never leaves the browser, so it is never scanned                                             | —                                                         |
| Server-side     | `Tools/DataMigration` has the real semantics but is operator-only and unreachable from `Web` | `Tests/UnitTests/Architecture/ProjectDependencyTests.cs`  |

## In scope

- **Two explicit steps, as today.** Upload a CSV or Excel file to the API and validate it as a
  **dry run** returning a per-row outcome report; then commit the validated batch as a second,
  separate call. The page's existing two-step shape stays; only the execution moves.
- **Durable operations.** Validation and commit run as `OPS_` operations, so a restart does not lose
  a batch, a commit is idempotent and re-runnable, and a commit interrupted halfway leaves no
  partial batch. `ktl-migrate` is the precedent to follow.
- **Shared row semantics.** Extract the row-outcome model and the reference-resolution rules from
  `Tools/DataMigration` into `Application` so both the operator tool and the API use one
  implementation. Reference values are **resolved, never silently created**. Do not reference
  `Tools/` from a production project — the architecture test will fail, and correctly.
- **The uploaded file is personal data.** It goes to private storage under an opaque key, is
  scanned by ClamAV and stays quarantined until `Clean` before anything parses it, and is purged on
  a documented schedule once the batch closes. The same rules as candidate documents (KTL-9).
- **Batch history on the server.** Store batches and their row outcomes in `ADM_` or `OPS_` tables
  and show the history on the Importación page. The `localStorage` key is evicted through
  `evict-legacy-storage.ts`.
- **Guarded.** Every endpoint checks `candidates.import` after authentication and before validating
  or dispatching. This is the change that finally gives that permission something to guard.
- **Safe reporting.** The row report carries row numbers, outcomes and stable error codes. Candidate
  personal data from the file never reaches a log, and the report itself is behind the same
  permission as the import.

## Out of scope

- Parsing CVs, or extracting candidate data from anything other than the documented CSV/Excel
  contract.
- Any import format beyond that contract.
- Export (its own ticket), and the audit trail (KTL-19), though an import obviously produces
  auditable events once KTL-19 lands.
- Changing the candidate write contract or the catalog families.

## Decisions to make in the design

1. **Where the shared row semantics live.** A new `Application/Features/Import` shared with
   `Tools/DataMigration`, or a dedicated module both reference. `Tools/` may reference production
   projects; the reverse may not happen.
2. **How a batch is keyed for idempotency.** A client-supplied batch id, a content hash of the
   uploaded file, or the operation id. Say which, and what happens when the same file is uploaded
   twice on purpose.
3. **Excel support.** Whether it ships in this ticket or the contract is CSV-only to start. Excel
   means a new runtime dependency, which needs a documented reason.
4. **Row limits.** The maximum number of rows and the maximum file size, and what the API answers
   above them. The existing upload size limit and multipart envelope are the starting point.
5. **Purge schedule.** How long a closed batch keeps its uploaded file and its row report, and what
   runs the purge.
6. **Partial commits.** Whether a batch with some failing rows commits the good ones or refuses
   wholesale. The current browser behaviour is the baseline; state whether it is kept.

## Acceptance criteria

- Importing a CSV of N rows produces the same outcome whether it is committed once or re-run, and a
  restart mid-commit leaves no partial batch.
- An unauthenticated caller gets 401 and an authenticated caller without `candidates.import` gets
  403 on every import endpoint, **before** validation runs.
- An uploaded file is not parsed until the scanner reports it clean, and an infected file is refused
  terminally.
- Reference values that do not exist are reported as row failures; no import run creates a catalog
  value.
- The `localStorage` key `rrhh.import.batches.v1` is gone and evicted on upgrade, and no new
  `localStorage` data path is introduced.
- No candidate personal data from an imported file appears in any log.
- No production project references `Tools/`, proven by the existing architecture test.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Security evidence required

This ticket touches personal data, permissions and private storage, so it is not done without tests
that fail closed:

- Per-endpoint integration tests for unauthenticated and unauthorized callers, refused before
  validation.
- A test proving an unscanned or infected upload is never parsed.
- A test proving the uploaded file is stored outside the webroot under an opaque key, and that no
  response contains a storage path.
- An idempotency test: the same batch committed twice yields one set of candidates.
- A crash-recovery test: a commit interrupted partway leaves no partial batch and completes on
  re-run.
- Log assertions: no name, email, phone or other row content from an imported file reaches the logs.

## Risks

- **Duplicated semantics.** If the extraction from `Tools/DataMigration` is half-done, the API and
  the operator tool will drift and disagree about what a valid row is. Extract fully or not at all.
- **Large files.** Parsing and committing thousands of rows inside a request will time out. The
  durable-operation shape exists to avoid that; use it rather than raising timeouts.
- **Scanner availability.** ClamAV needs ~3 GiB and can be degraded. Decide whether a degraded
  scanner blocks imports (it should) and make the refusal explicit rather than a hang.

## Documentation to update

- `docs/ktl-17/` — the import file contract, the row outcome and error codes, the batch lifecycle
  and the purge schedule.
- `README.md` (Spanish) — how to run an import locally.
- `AGENTS.md` — remove the import path from the legacy `localStorage` note.
