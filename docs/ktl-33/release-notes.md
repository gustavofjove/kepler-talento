# KTL-33: Candidate personal data encrypted in the database

Candidate names, contact details, notes, career history, CV file names and saved search terms are
now stored encrypted. A copy of the database or of a backup cannot be read without the key file,
which is kept separately.

**For users, nothing changes.** Screens, search results, sorting, import and exports behave exactly
as before. Two small differences: a value longer than the field allows is now refused with a clear
message instead of a generic error, and saving an experience or education without its company,
position, degree or institution is refused before it reaches the database.

**Search limit.** Text search and sorting by surname now run in the application. They stay fast up
to about 12,000 candidates (there are about 3,000 today). Growing past that needs a planned change.

**Not yet covered.** CV and import **files** on disk are not encrypted by this change, and neither
are user accounts. Recovery sets taken before the rollout contain plaintext and must be retired.

**Operations — breaking.**

- The stack needs a key file (`KTL_FIELD_KEYS_FILE`). The API refuses to start without it, or while
  any candidate value is still unencrypted.
- **Losing the key file loses the data, backups included.** It must be escrowed with two named
  holders before rollout. See [key-runbook.md](key-runbook.md).
- Direct SQL, reporting tools and DBA queries see ciphertext for these columns.
- Rollout: [rollout-runbook.md](rollout-runbook.md). New tool commands: `ktl-migrate encryption
generate-keys | add-key | retire-key | backfill | report`.
- The e2e teardown now reads the development key file to recognise test candidates.

**Dependencies.** None added: encryption uses .NET's built-in AES-GCM and HMAC.

Details: [field-encryption.md](field-encryption.md), [performance.md](performance.md),
[design-notes.md](design-notes.md).
