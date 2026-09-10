# KTL-7 migration runbook

Moving the candidate dataset from the legacy Access database into PostgreSQL.

> **This is not the "Importación" screen in the admin area.** That screen validates a small
> flat CSV in the browser and creates no candidates. This migration is an operator-run
> command-line tool, never reachable over HTTP, and it is what actually moves the dataset.

The tool is `backend/Tools/DataMigration`, built as `ktl-migrate`.

```
ktl-migrate validate --connection <string> --export <directory> [--mappings <file>] [--output <directory>]
ktl-migrate load     --connection <string> --export <directory> --pre-migration-backup <label> [--mappings <file>] [--overwrite-app-edits]
ktl-migrate report   --connection <string> --run <guid> [--output <directory>]
```

`--connection` uses the **migration** database role, not the runtime role. `load` refuses to
start without `--pre-migration-backup`.

## Before you start

- Catalog vocabulary must already be seeded and reviewed through the catalog administration
  screens. The migration resolves Access free text against existing catalog entries and
  never creates one.
- You need a copy of `BBDD CVs.accdb`. Never work against the file HR is using.
- You need somewhere protected to hold the export set. It contains names, contact details,
  notes, consent metadata and CV binaries in the clear.

## 1. Produce the export set

Follow [`access-export-procedure.md`](access-export-procedure.md). It defines the seven CSV
files, their columns and encodings, the per-row source identifiers, and how to compute the
document hashes.

## 2. Validate, and keep validating until you understand the result

```powershell
ktl-migrate validate --connection $env:KTL_MIGRATION_CONNECTION --export D:\ktl7\export --output D:\ktl7\reports
```

`validate` writes **no business data**. It stages the export, applies every row rule,
resolves every catalog reference, and verifies every document — existence, format, size,
hash and malware scan — then writes a reconciliation report.

Read the report and work through its checklist:

- **Unresolved reference values.** Each one needs a decision. Either add the value through
  the catalog administration screens, or map it in `mappings.csv`:

  ```csv
  Family,SourceValue,TargetCode
  language,Ingl.,INGLES
  ```

  A mapping names an existing catalog **code**. It cannot create vocabulary; a mapping
  pointing at a code that does not exist is reported as a broken mapping.

- **Rejected rows.** Each names a source key, the failing field, and a reason code. Fix them
  in Access and re-export, or accept that they will not be migrated. Rows rejected for
  `consent.missing` cannot be waved through: a candidate whose consent state cannot be
  established is not loaded, and no date is substituted for the missing one.

- **Document problems.** A `document.hash.mismatch` means the manifest and the file disagree
  — usually a stale hash after the file was replaced. A `document.scan.rejected` means the
  scanner found something; that file will not become available under any circumstances.

Re-run `validate` until the result is one you are willing to commit.

## 3. Take the backup that will undo this

```powershell
pwsh -File scripts/operations/backup.ps1 -RetentionDays 30
```

Note the recovery identifier. You will pass it as `--pre-migration-backup`, it is recorded
on the run, and the report repeats it in its header — so the artifact that proves the
migration also names the thing that reverses it.

## 4. Load

```powershell
ktl-migrate load `
  --connection $env:KTL_MIGRATION_CONNECTION `
  --export D:\ktl7\export `
  --mappings D:\ktl7\mappings.csv `
  --pre-migration-backup 20260910T090000Z `
  --output D:\ktl7\reports
```

Exit codes: `0` reconciled, `4` did not reconcile, `3` failed, `2` bad usage.

A candidate and all of its relation rows commit together. If any part of one candidate
fails, none of that candidate is written and it is reported as rejected; the rest of the run
continues.

## 5. Check the report

The run reconciles when every source row is accounted for as loaded, rejected or skipped, in
every entity. If it does not reconcile, **treat the load as incomplete** and investigate
before anyone relies on the data.

Confirm as well:

- document verification shows `matched` for every document you expected to migrate;
- the counts match what `validate` predicted.

## 6. Destroy the export set

Delete the export root, including `files/` and any intermediate hash file, and empty the
recycle bin. The tool cannot do this for you — it never learns where the export came from
beyond the directory it was given.

## 7. Re-run the KTL-5 backup validation

```powershell
pwsh -File scripts/operations/backup.ps1 -RetentionDays 30
pwsh -File scripts/operations/restore.ps1 -RecoveryId <new-id> -ConfirmNonProduction
pwsh -File scripts/operations/reconcile.ps1
```

Reconciliation must still pass against the migrated database, including the documents this
migration added.

## Running again over a newer export

HR keeps using Access until the candidate cutover, so a second and newer export is the
normal path, not an exception. Re-running is safe:

- rows that changed in Access are **updated in place** — no duplicates, same identifiers;
- rows that are new in Access are **inserted**;
- records the application created carry no source key and are **never touched**.

Two things need your attention:

**Records edited in the application.** Once the candidate screens write to PostgreSQL, a
record may have been changed by a user after the migration loaded it. Those records are
**skipped and reported**, not overwritten. If the Access values really should win, re-run
with `--overwrite-app-edits`; the report then states how many records were overwritten.
There is no way to do this by accident.

**Records absent from the newer export.** A source key stored in PostgreSQL that the new
export does not contain is reported as an unmatched target record, and **nothing is changed
about it**. It usually means the row was deleted in Access — but an export query missing a
`WHERE` clause looks exactly the same from here, and deactivating live candidates on that
basis is not a decision worth automating. Confirm which it was, then act deliberately.

## Rollback

**Restore the backup named in the report header.** That is the rollback; there is no
`ktl-migrate rollback` verb, deliberately.

```powershell
pwsh -File scripts/operations/restore.ps1 -RecoveryId <the label from the report> -ConfirmNonProduction
pwsh -File scripts/operations/reconcile.ps1
```

**Restore the document volume too, not only PostgreSQL.** The recovery set covers both, and
it has to: rolling back the database alone removes the document metadata while the binaries
it pointed at stay on disk, under opaque keys nothing references any more. Reconciliation
would then report them as orphans. `restore.ps1` handles both; a hand-rolled `pg_restore`
does not.

A run-scoped undo was considered and rejected: reverting _updates_ rather than only inserts
needs a per-run change journal, which means keeping staging data — the same personal data
the export holds — indefinitely, and it would be the only code path in the product that
physically deletes candidate rows. Restore is coarser, already documented, already exercised
by the KTL-5 backup validation, and correct.

After restoring, resume from step 2.

## If a run fails partway

The staging schema is retained for diagnosis and the tool says so on stderr. **It holds the
same personal data the export does.** Once you are done with it:

```sql
DROP SCHEMA migration_staging CASCADE;
```

A successful run drops it automatically.

## Development and test runs

Never point this tool at the production Access dataset outside a real migration. The
synthetic fixtures under `backend/Tests/Fixtures/ktl-7/` exercise every outcome the tool can
produce — loaded, rejected, skipped, unresolved, unmatched, and all three document failure
modes — and the automated suite runs the whole process, including rollback, against them.
