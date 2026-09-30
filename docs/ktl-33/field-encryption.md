# KTL-33 field encryption

Candidate personal data is stored in PostgreSQL only as ciphertext. A copy of the database — a
`pg_dump`, a recovery set in `backups/`, a copied volume — is unreadable without the key file,
which lives outside the database and outside the backup set.

The design and its trade-offs are in
[`openspec/changes/ktl-33-personal-data-encryption/design.md`](../../openspec/changes/ktl-33-personal-data-encryption/design.md)
(archived under `openspec/changes/archive/` once the change is archived).

## What is encrypted

Every human-entered free-text column on candidate tables:

| Table                                                               | Encrypted columns                                                                                               |
| ------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `CND_Candidates`                                                    | `FirstName`, `LastName`, `Phone`, `Email`, `Location`, `Province`, `Country`, `Availability`, `Source`, `Notes` |
| `CND_CandidateNotes`                                                | `Body`                                                                                                          |
| `CND_CandidateExperience`                                           | `Company`, `Position`, `Functions`, `Notes`                                                                     |
| `CND_CandidateEducation`                                            | `Degree`, `Specialty`, `Institution`, `Notes`                                                                   |
| `CND_CandidateLanguages`                                            | `Certification`, `Notes`                                                                                        |
| `CND_CandidateSkills`, `CND_CandidatePrograms`, `CND_CandidateTags` | `Notes`                                                                                                         |
| `CND_Documents`                                                     | `OriginalFileName`                                                                                              |
| `ADM_SearchPresets.Filters`, `OPS_Positions.Requirements`           | the `text` member of the JSON document only                                                                     |

Left in clear: identifiers, catalog references, status, flags, dates, source keys, storage keys,
content hashes, scan codes, preset names and position titles. Audit events hold no personal values.

**Not covered:** CV and import files on disk (`documents` volume, `documents.tar.gz` in backups),
and user accounts (`ADM_Users`). Those need their own tickets.

## How it works

- **Envelope.** Each value is stored as `ktl1.<keyId>.<base64url(nonce ‖ ciphertext ‖ tag)>`:
  AES-256-GCM with a random 96-bit nonce per value, and `"<Table>.<Column>"` as associated data,
  so a value copied into another column does not decrypt. The same plaintext never produces the
  same envelope.
- **Transparent to the code.** EF Core value converters encrypt on save and decrypt on read,
  including in projections. Entities, handlers, endpoints and the SPA are unchanged.
- **Database checks.** Every encrypted column has a check constraint `CK_<Table>_<Column>_Encrypted`
  (`LIKE 'ktl1.%'`), added `NOT VALID` by the migration: any new plaintext write is rejected, even
  from a direct SQL client.
- **Length limits.** The former `varchar` widths are kept as `CandidateTextLimits` and enforced by
  the validators (`candidate.field.too_long`, `document.file_name.too_long`) and, as a backstop, by
  the converter.
- **E-mail blind index.** `CND_Candidates."EmailHash"` holds `<keyId>.<HMAC-SHA256>` of the
  trimmed, lower-cased e-mail, under a separate key. Import duplicate detection uses it.
- **Search.** Status, catalog families, tags and CV presence are still filtered by PostgreSQL. Free
  text and the last-name order run in the API over decrypted values, with exactly the previous
  semantics ([design-notes.md](design-notes.md)). Documented ceiling: **12,000 candidates**, p95 ≤
  300 ms ([performance.md](performance.md)). Beyond it, a word-level blind index is the planned
  next step.
- **Query guard.** A query that filters, orders or groups on an encrypted property throws
  `EncryptedColumnQueryException` naming the column, instead of silently comparing ciphertext.
  Filter and sort such fields in memory after `Select`.

## Fail-closed startup

On the serving path (not `--migrate`) the API refuses to start when:

| Code                            | Cause                                                                |
| ------------------------------- | -------------------------------------------------------------------- |
| `encryption.key.missing`        | The key file is absent or unreadable                                 |
| `encryption.key.malformed`      | Not valid JSON, a key is not base64, or an identifier is malformed   |
| `encryption.key.length`         | A key is not 256 bits                                                |
| `encryption.key.active_unknown` | The active identifier names no key in the file                       |
| `encryption.key.shared`         | Two keys are identical                                               |
| `encryption.plaintext.present`  | An encrypted column still holds plaintext (the backfill has not run) |
| `encryption.staging.present`    | The legacy `migration_staging` schema (plaintext) still exists       |

Diagnostics name the code and, at most, a table. Never a value, a key or the key file's path.

## For developers

- Queries: never put an encrypted property inside `Where`, `OrderBy`, `GroupBy`, `Any`, `Count`,
  joins or `EF.Functions`. The guard will throw. Use `EmailHash` for e-mail equality.
- New candidate text column: mark it `.IsEncrypted(Table, limit)` in its configuration, add the
  envelope check in the same migration, and extend the backfill test.
- Tests: contexts built directly need `.UseTestFieldEncryption()`; test hosts get the run's key
  file through `FieldEncryption__KeyFile`, set by `TestFieldEncryption`.

Operations: [key-runbook.md](key-runbook.md) and [rollout-runbook.md](rollout-runbook.md).
