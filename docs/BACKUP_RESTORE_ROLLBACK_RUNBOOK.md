# Backup, restore and rollback

The current procedure from KTL-5 lives in
[`ktl-5/operator-runbook.md`](ktl-5/operator-runbook.md). It replaces the previous
Supabase-snapshot-only procedure for the ASP.NET Core / PostgreSQL / private-storage
platform.

Legacy paths still on Supabase are unchanged by KTL-5 and must keep their existing backup
until their own migration and cutover are validated.

## After the Access migration

Once KTL-7 has loaded the candidate dataset, the recovery set contains real candidate
personal data and CV binaries. Two things follow.

**Reconciliation must still pass.** Re-run the KTL-5 backup validation after the migration
and confirm it reconciles against the migrated database, including the documents the
migration added:

```powershell
pwsh -File scripts/operations/backup.ps1 -RetentionDays 30
pwsh -File scripts/operations/restore.ps1 -RecoveryId <new-id> -ConfirmNonProduction
pwsh -File scripts/operations/reconcile.ps1
```

Every migrated document must appear in its expected available area with a matching SHA-256.
A migrated document that reconciliation reports as missing or mismatched means the migration
and the backup disagree about what was stored, and the load should not be trusted until that
is explained.

**Restore is the migration's rollback.** A migration run records the label of the backup
taken before it, and its reconciliation report repeats that label in the header. To undo a
migration, restore that recovery set — see
[`ktl-7/migration-runbook.md`](ktl-7/migration-runbook.md). There is deliberately no
run-scoped undo in the migration tool.
