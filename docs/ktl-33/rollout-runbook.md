# KTL-33 rollout runbook

Moves an existing installation from plaintext to encrypted candidate data. It was rehearsed on
the development stack on 2026-09-29 (505 candidates and their relations: backfill, report clean,
`VACUUM FULL`, API started).

Plan a maintenance window. With a few thousand candidates the data steps take seconds; the window
is for the checks.

## Before the window

- Read [key-runbook.md](key-runbook.md). Name the two escrow holders.
- Confirm nobody depends on reading candidate data directly from PostgreSQL (reports, ad-hoc SQL):
  after this, they see ciphertext.

## Steps

1. **Keys.** `ktl-migrate encryption generate-keys --out <host path>`. Escrow the file now. Set
   `KTL_FIELD_KEYS_FILE` in `.env` to its path.
2. **Backup.** `pwsh -File scripts/operations/backup.ps1`. This recovery set is **plaintext**; it is
   the rollback, and it is retired in step 9.
3. **Deploy.** `docker compose up --build -d`. The `migrator` applies `EncryptPersonalData` without
   keys. The API then **refuses to start** with `encryption.plaintext.present for CND_Candidates`
   and restarts in a loop. This is expected.
4. **Backfill** as the runtime role:
   `encryption backfill --connection "Host=postgres;...;Username=ktl_runtime;..."`.
5. **Report.** `encryption report` must exit 0 and end with "every value is encrypted and
   readable". If it lists plaintext, run the backfill again (a concurrent write may have won a row).
6. **Remove old row versions** as the migration role, so no plaintext stays in the data files:

   ```powershell
   'VACUUM FULL "CND_Candidates", "CND_CandidateEducation", "CND_CandidateExperience", "CND_CandidateLanguages", "CND_CandidateNotes", "CND_CandidatePrograms", "CND_CandidateSkills", "CND_CandidateTags", "CND_Documents", "ADM_SearchPresets", "OPS_Positions";' |
     docker compose exec -T postgres psql -U ktl_migrator -d kepler_talento
   ```

7. **Start.** `docker compose restart api`. It becomes healthy. Open a candidate, run a text search
   and a last-name sort, validate an import.
8. **Restore drill** with the escrowed key file ([key-runbook.md](key-runbook.md#restore-drill)).
9. **Retire plaintext copies.** Delete the step-2 recovery set and every older one, and any volume
   or VM snapshot taken before step 4, once the drill has passed.

## Rollback

At any step: restore the step-2 recovery set with `restore.ps1` and redeploy the previous release.
There is deliberately no command that decrypts the database in place.

## Follow-up

Once production has passed step 5, a small change validates the envelope constraints
(`ALTER TABLE ... VALIDATE CONSTRAINT`) and removes the startup plaintext probe.
