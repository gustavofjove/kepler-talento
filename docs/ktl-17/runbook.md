# Candidate import runbook (KTL-17)

For operators of the API. Users follow the page; this covers what happens underneath and what to
do when it does not.

## Moving parts

- **Tables.** `ADM_ImportBatches` (one row per upload: state, counts, SHA-256, failure code,
  timestamps, uploader) and `ADM_ImportRowOutcomes` (one row per data row per phase, write-once).
  `ktl_runtime` holds `SELECT, INSERT, UPDATE` on batches and `SELECT, INSERT` on outcomes, and no
  `DELETE` on either.
- **Files.** Stored through the document storage mechanism under `imports/<batch>/content`, first
  in `quarantine/`, promoted to `available/` only on a clean scan. Nothing reads an import file
  from quarantine.
- **Durable operations** in `OPS_Operations`, run by the API's operation worker
  (`OperationWorker:Enabled`): `import.scan`, `import.validate`, `import.commit`, `import.purge`.
  Idempotency key `<type>:<batch>:<attempt>`.
- **Maintenance service.** On start-up and every `Import:PurgeIntervalMinutes` it re-queues stuck
  batches (see _Recovery_) and queues a purge.

## Batch lifecycle

```
uploaded ─▶ scanning ─┬▶ infected        (terminal, file never read)
                      ├▶ unscannable     (terminal, file never read)
                      └▶ scanned ─▶ validating ─┬▶ failed (structural problem)
                                                └▶ validated ─▶ committing ─┬▶ committed
                                                                            └▶ failed (file missing/changed)
validated | committed | failed ─▶ expired      (file purged; counts and report kept)
infected | unscannable          ─▶ same state   (file purged; FileRetained = false)
```

| Step     | Triggered by                       | What it writes                                                                                                                                            |
| -------- | ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Upload   | `POST /api/import/batches`         | Quarantined file, batch `uploaded`, `import.scan` operation.                                                                                              |
| Scan     | worker                             | `scanning`, then `scanned` + promotion, or `infected`/`unscannable`.                                                                                      |
| Validate | `POST …/{id}/validation` (version) | `validating` + operation; then, in one transaction, every validation outcome and `validated` — or `failed`.                                               |
| Commit   | `POST …/{id}/commit` (version)     | `committing` + operation; then candidates, relations, `candidate.created` audit events and commit outcomes in transactions of 100 rows; then `committed`. |

The page polls `GET /api/import/batches/{id}` every 1.5 s for about two minutes while a batch is
`uploaded`, `scanning`, `validating` or `committing`, then stops and tells the user the batch is
still running. Nothing is lost by the page giving up; reopening the batch from the history
resumes polling.

## Idempotency and interruption

- **Commit twice.** The second request answers with the batch as it is and queues nothing. Two
  concurrent commits cannot both claim: only one `validated → committing` write matches the version.
- **API killed mid-commit.** Each chunk's candidates and their outcome rows commit together, so a
  kill loses at most the chunk in flight, which rolls back whole. When the worker's lease expires
  (`OperationWorker:LeaseSeconds`), the next worker reclaims the operation and the commit handler
  skips every row that already has a commit outcome. The result is exactly the candidates of one
  uninterrupted run; `ImportInterruptedCommitTests` proves this by killing the real process.
- **Operation out of attempts.** An operation that fails three times is marked failed while its
  batch stays transient. The maintenance service notices on its next tick (or at start-up), gives
  the batch a new attempt number and queues a fresh operation. A commit resumes; it never restarts
  from row one.

## Recovering a stuck batch

1. Look at the batch and its operation. Batch ids are safe to log and to paste; nothing in either
   row is personal data.

   ```sql
   SELECT "Id", "State", "OperationAttempt", "FailureCode", "UpdatedAtUtc"
   FROM "ADM_ImportBatches" WHERE "Id" = '<batch>';
   SELECT "Type", "Status", "AttemptCount", "OutcomeCode", "LeaseExpiresAtUtc", "UpdatedAtUtc"
   FROM "OPS_Operations" WHERE "IdempotencyKey" LIKE 'import.%:<batch-without-dashes>:%';
   ```

2. **`Running` with a future lease:** a worker is on it. For a commit, watch progress with
   `SELECT count(*) FROM "ADM_ImportRowOutcomes" WHERE "BatchId" = '<batch>' AND "Phase" = 'commit';`.
3. **`Running` with an expired lease, or `Failed`, and the batch still transient:** restart the API
   or wait one `Import:PurgeIntervalMinutes`; recovery re-queues it. Check the API log for the
   warning `Durable operation polling failed` and the batch id.
4. **Scanning forever:** the scanner is degraded — see below.
5. **Never** edit `ADM_ImportRowOutcomes` (the runtime role cannot) and never move a batch backwards
   by hand. A batch that cannot complete is closed by uploading the file again as a new batch; its
   already-loaded rows then skip as duplicates.

## Scanner degraded

ClamAV needs about 3 GiB of RAM. When `/api/health/scanner` reports degraded, new uploads either
wait in `scanning` until the scanner answers or end `unscannable`. This is deliberate: an import
file is never admitted without a verdict. The page shows the state and, for `unscannable`, tells the
user to check the scanner and upload again. Once the scanner is healthy, upload the file again as a
new batch; an `unscannable` batch is terminal and cannot be retried.

## The 30-day purge — and what it destroys

An import file is a bulk collection of candidate identity and contact data, so it is not kept
indefinitely (design D7).

- **When:** `Import:RetentionDays` (default **30**) after the batch closes — `validated`, `committed`,
  `failed`, `infected` or `unscannable`. An abandoned validated batch counts from its validation.
- **How:** the worker runs `import.purge` every `Import:PurgeIntervalMinutes` (default 60). To run it
  on demand, the same way as `--reconcile`:

  ```powershell
  docker compose run --rm api --purge-imports
  # or, outside Compose
  dotnet backend/Web/bin/Debug/net10.0/Web.dll --purge-imports
  ```

  It prints `{"FilesPurged":n,"MissingFilesRecorded":n,"OrphansRemoved":n}`.

- **What is destroyed:** the uploaded file, in quarantine and available storage. After that nobody
  can say exactly what a row contained. A purged `validated` batch becomes `expired` and can no longer
  be committed.
- **What survives:** the batch row (state, counts, SHA-256, failure code, unresolved catalog values,
  timestamps, uploader) and every row outcome. The page still renders the report.
- **No orphans either way:** an `imports/` file with no batch, older than `Import:OrphanGraceHours`
  (default 24), is deleted; a closed batch whose file is already gone is marked purged so it says so.
- `--reconcile` ignores the `imports/` prefix; import files are reconciled by the purge, not against
  candidate documents.

## Configuration

| Setting                       | Default   | Bounds         |
| ----------------------------- | --------- | -------------- |
| `Import:MaximumBytes`         | 5 242 880 | 1 – 20 971 520 |
| `Import:MaximumRows`          | 2000      | 1 – 50 000     |
| `Import:RetentionDays`        | 30        | 1 – 3650       |
| `Import:PurgeIntervalMinutes` | 60        | 1 – 1440       |
| `Import:OrphanGraceHours`     | 24        | 1 – 720        |

The API refuses to start with a value outside its bounds.

## Diagnostics

Import logs carry the batch id, row number, column, reason code, counts and correlation id — never
a value, the original filename or the storage key. `PersonalDataRedactionEnricher` also masks the
import column names (`first_name`, `email`, …), `originalFileName`, `fileName` and `storageKey`
wherever they appear, as a second line of defence.

## Rollback

The SPA rolls back to its previous build, whose import stub writes to `localStorage` again —
harmless, it never wrote anything real. The API rolls back by redeploying the previous image; the
migration's `Down` drops both tables. Candidates already created by a committed import are ordinary
candidates and are **not** removed by a rollback: they are real records people may already have
edited. Deactivate them individually if needed.
