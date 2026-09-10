# KTL-5 operator runbook

## Configuration and startup

Copy `.env.example` to an untracked `.env`, replace the development migration
password, and run `docker compose up --build`. Only Nginx publishes a host port.
PostgreSQL and `clamd` remain on the internal `backend` network. The migration
container uses the schema-owner identity; the API uses `ktl_runtime` and cannot
change schema.

Health semantics:

- `/api/health/live`: API process only.
- `/api/health/ready`: PostgreSQL, applied migrations, and writable private storage.
- `/api/health/scanner`: separate malware-scanner availability. A scanner outage
  blocks promotion of new documents but does not invalidate previously clean files.

ClamAV requires 3 GiB of free host memory, with 4 GiB preferred during signature
updates. Inputs are limited to 20 MB, expanded content to 100 MB, recursion to 16,
and contained files to 500.

### Scanner unavailable

Treat a degraded `/api/health/scanner` response as an operational incident. New uploads
may be accepted into private quarantine, but they fail closed and never become available
without a clean verdict. Do not bypass scanning, move objects by hand, or change a document
row to `Clean`. Documents that were already clean remain downloadable because their exact
stored content was approved before the outage. Restore ClamAV connectivity/signatures,
verify the scanner health endpoint, and inspect durable `document.scan` outcomes and the
reconciliation report before resuming normal operation.

## Backup and retention

Run daily from a host scheduler:

```powershell
pwsh -File scripts/operations/backup.ps1 -RetentionDays 30
```

The script creates one timestamped, manifested database/document recovery set,
hashes every artifact, labels partial attempts `incomplete`, and never deletes the
latest valid set. Configure Task Scheduler or cron at an operator-selected daily
time; alert when no `complete` manifest exists within 24 hours.

## Restore drill

Freeze writes and use a clean non-production stack. Then run:

```powershell
pwsh -File scripts/operations/restore.ps1 -RecoveryId 20260825T120000Z -ConfirmNonProduction
pwsh -File scripts/operations/reconcile.ps1
```

The restore verifies manifest hashes before replacing validation data, restores
PostgreSQL and document volumes, and validates migrations. Reconciliation then checks
every document metadata key and SHA-256 hash in its expected available/quarantine area,
detects binaries without metadata, and reports pending or failed quarantine objects older
than 24 hours. Missing, mismatched, orphaned, and stale objects produce redacted
`document.reconciliation` audit events; orphan keys are represented by an opaque digest.
No candidate history or binary is deleted automatically. The command exits unsuccessfully
until every finding has been reviewed and resolved. Record elapsed time against the 4-hour
RTO and backup age against the 24-hour RPO.

Development/Test operation diagnostics support lookup by operation ID or exact
correlation ID under `/api/reference/operations`. Responses contain status, type,
timestamps, attempt count, correlation ID, and outcome code only; they omit idempotency
keys, document contents, storage locations, and candidate data.

## Rollback

Before any later business cutover, stop the KTL-5 stack and redeploy the existing
frontend-only Compose file. Existing localStorage, Supabase, and Access sources are
untouched. After a business cutover, freeze writes, select a compatible complete
recovery set, restore it, reconcile it, and only then re-enable traffic.

Authentication, production host/proxy selection, Access migration, SMB, and CSV
product behavior remain separate changes.
