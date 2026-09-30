# KTL-33 — Encrypt candidate personal data at rest in PostgreSQL

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-5 (PostgreSQL conventions, roles and grants), KTL-10 (paged search and its
query-plan evidence), KTL-17 (candidate import and its e-mail deduplication), KTL-18 (list sort
contract)

## Summary

Candidate personal data is stored in plaintext columns. Anyone who obtains a copy of the database
— a `pg_dump` taken by any account with read access, a copied data volume, a VM snapshot, a
recovery set from `backups/` — can read every candidate's identity, contact details and notes with
standard PostgreSQL tools.

This ticket makes the database hold **only ciphertext** for candidate personal data. The API
encrypts on write and decrypts on read with keys that live **outside PostgreSQL and outside the
backup set**, so a copy of the database, in any form, is unreadable without them.

Encryption is reversible (AES-256-GCM), not hashing: the application must show the data again.
Because each value is encrypted with a random nonce, PostgreSQL can no longer compare, search or
sort the encrypted columns. The two places that depend on that today — the free-text search filter
with the last-name sort, and import deduplication by e-mail — move to the API and to a keyed-hash
blind index respectively. Users should see no behaviour change.

## Context — what exists today

| Concern              | Today                                                                                | Evidence                                                                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Candidate columns    | `FirstName`, `LastName`, `Phone`, `Email`, `Location`, `Province`, `Notes` in clear  | `Infrastructure/Persistence/Configurations/CandidateConfiguration.cs`                                                                    |
| Related free text    | Note bodies, experience company, education institution, document file names in clear | `CandidateNoteConfiguration.cs`, `CandidateExperienceConfiguration.cs`, `CandidateEducationConfiguration.cs`, `DocumentConfiguration.cs` |
| Search text filter   | `ILIKE '%…%'` over first name, last name, e-mail, phone and notes, in SQL            | `Infrastructure/Persistence/CandidateSearchQuery.cs` (`MatchingAsync`)                                                                   |
| Last-name sort       | `ORDER BY "LastName", "FirstName"` served by B-tree indexes                          | `CandidateSearchQuery.cs` (`Order`), `CandidateConfiguration.cs`                                                                         |
| Import deduplication | `lower("Email")` compared in SQL                                                     | `Infrastructure/Import/ImportRunSupport.cs`                                                                                              |
| Saved search presets | Filter document stored as `jsonb`, including any free-text value typed by the user   | `SearchPresetConfiguration.cs`                                                                                                           |
| Audit                | Stores identifiers and codes only, no personal values                                | `AuditEventConfiguration.cs`                                                                                                             |
| Backups              | Plaintext `pg_dump` custom-format file plus a tarball of document binaries           | `scripts/operations/backup.ps1`                                                                                                          |
| Data size            | About 3 000 candidates                                                               | `docs/ktl-10/query-plans.md`                                                                                                             |

## In scope

- **Field encryption in the API.** An `IFieldProtector` abstraction in `Application/Abstractions`
  with an AES-256-GCM implementation in `Infrastructure`, built on
  `System.Security.Cryptography.AesGcm` (no new runtime dependency). EF Core value converters apply
  it transparently, so entities keep plain `string` properties and create, update, detail, list and
  position-candidate reads keep working without per-call code.
- **Self-describing ciphertext.** Every stored value carries a format version and a key identifier
  alongside nonce, ciphertext and tag, so keys can be rotated without a flag day. The table and
  column name are bound as associated data, so a ciphertext cannot be moved into another column
  and still decrypt.
- **Columns encrypted.** At minimum:
  - `CND_Candidates`: `FirstName`, `LastName`, `Phone`, `Email`, `Location`, `Province`, `Notes`.
  - Candidate note bodies, experience company, education institution, document original file name.
  - The free-text value inside saved search presets (it can contain a candidate's name).

  The design lists every remaining text column on candidate tables and states, for each, whether it
  is encrypted or why it is not.

- **Columns left in clear.** Identifiers, catalog references (skills, languages, programs, tags and
  their levels), status, flags and timestamps. Every search filter on these stays in SQL, unchanged,
  with the existing correlated-existence shape and no duplicates.
- **E-mail blind index.** A new `EmailHash` column holding HMAC-SHA256 of the normalized e-mail
  under a key separate from the encryption key, with an index. Import deduplication and any
  e-mail equality lookup use it instead of the e-mail column.
- **Text search and last-name sort move to the API.** When a request carries a text filter or sorts
  by last name, SQL applies every other filter and returns the matching rows; the API decrypts,
  applies the same case-insensitive literal substring match over the same five fields, sorts, counts
  and pages. Requests without either (including the default `updatedAt` descending order) keep the
  current fully-SQL path. Results, totals and paging must be identical to today.
- **Schema changes through EF Core migrations.** Columns widen for ciphertext; indexes on encrypted
  columns are dropped; `CK_CND_Candidates_Name` (which cannot inspect ciphertext) is dropped once
  its rule is confirmed to be enforced by the validators; `EmailHash` and its index are added, with
  `ktl_runtime` grants in the same migration.
- **Backfill.** A `ktl-migrate` command encrypts existing rows and fills `EmailHash`, idempotently
  and in batches, with a reconciliation report that counts rows and proves every value decrypts.
  Rollout is expand → backfill → verify → contract, and ends with `VACUUM FULL` on the affected
  tables so no old plaintext row versions remain in the data files.
- **Key handling.** Keys are loaded at startup from a source outside PostgreSQL and outside
  `backups/`. The API **refuses to start** when a key is missing or malformed (fail closed); there is
  no plaintext fallback. `ktl_migrator` and the `migrator` container never hold the keys; only the
  API and the explicit backfill run do.
- **Query guard.** An automated check fails the build when an encrypted property appears in a
  query predicate, ordering, grouping or `ILike`/`ToLower` translation. Without it such a query does
  not throw — it silently returns nothing or garbage.

## Out of scope

- Encrypting CV and document **binaries** on the file system and in `documents.tar.gz`. They are
  not in PostgreSQL; they need their own ticket (encrypt after ClamAV reports `Clean`).
- Encrypting backup artifacts as files (the database dump becomes ciphertext for the encrypted
  columns as a consequence of this ticket).
- Word-level or prefix blind indexes for search. They are the documented next step if the
  candidate count outgrows the in-API search ceiling, and they would change substring matching to
  whole-word matching.
- Disk or volume encryption, and PostgreSQL TDE (not available in community PostgreSQL, and it
  would not protect logical dumps).
- User accounts (`ADM_` e-mail and names) — not candidate data; can follow the same mechanism later.
- Any frontend change. The API contract stays identical.

## Decisions to make in the design

1. **Key store and custody.** The default is a **generated random key delivered as a file**: each
   key is 32 random bytes (for example `openssl rand -base64 32`), never a human-chosen password
   and never derived from one. The encryption key and the blind-index key are separate. The API
   reads them from a key file mounted as a Docker secret, readable only by the `api` container and
   holding a key identifier with each value so rotation is possible. The file is never in
   PostgreSQL, `backups/`, `.env` or git. The design confirms this default or replaces it with a
   corporate vault if one exists, and names who holds the escrow copy (a password manager, vault
   or printed copy in a safe, held by at least two named people) and how a restore drill proves
   the escrowed key works. **Losing the key loses all candidate data, backups included.**
2. **Key hierarchy.** A single data key per purpose (encryption, blind index) versus envelope
   encryption with a key-encryption key wrapping data keys. Say which, and why.
3. **Rotation procedure.** How a new encryption key becomes active, how old values are re-encrypted
   in the background, and how the blind-index key would be rotated (it requires recomputing every
   hash).
4. **Remaining quasi-identifiers.** Whether `Country`, `Availability` and `Source` are encrypted.
   They are less identifying but still personal data by the project's definition.
5. **Match semantics parity.** How the in-API match reproduces PostgreSQL `ILIKE` behaviour
   (case folding, accents, escaping of `%` and `_`), proven by running the existing search test
   cases against both paths.
6. **Performance ceiling and budget.** The candidate count up to which in-API search is acceptable,
   and the latency budget at that count. A starting proposal: p95 under 300 ms at 20 000
   candidates for a text search or last-name sort.
7. **Whether to cache decrypted values in memory.** The default is no cache: plaintext stays in the
   API process only for the duration of a request.
8. **Column type.** `text` holding an encoded envelope versus `bytea`.

## Acceptance criteria

- After migration and backfill, no encrypted column in PostgreSQL contains a plaintext value, and a
  fresh `pg_dump` of the database contains none of a set of known seeded names, e-mails and phone
  numbers.
- Every read path (detail, list, search, position candidates, import preview/commit, presets)
  returns the same values as before the change.
- Search returns identical results, totals and page contents with and without the change for the
  existing search test cases, including ANY/ALL families combined with a text filter, and never
  returns a duplicate candidate.
- Import still detects duplicate e-mails, now through `EmailHash`.
- The API does not start without its keys, and there is no configuration that stores data in
  plaintext.
- A query that filters or sorts on an encrypted property fails the build.
- Performance evidence meets the budget chosen in the design.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Security evidence required

This ticket touches personal data, grants and the database schema:

- An integration test reading raw column values through a plain SQL connection and asserting none
  equals, or contains, the plaintext written through the API.
- A test proving a ciphertext copied into another column or tampered with fails to decrypt, and that
  the failure is reported without logging the value.
- A startup test proving the API refuses to start with a missing or malformed key, in every
  environment.
- Tests proving `EmailHash` is computed from the normalized e-mail and never exposed in responses.
- Log assertions: no plaintext, ciphertext, key material or key file path reaches the logs.
- The existing unauthenticated/unauthorized tests for search, list, detail and import keep passing
  unchanged.
- Grants: `ktl_runtime` keeps DML only on approved tables, including the new column; verified by the
  existing grant tests.

## Risks

- **Key loss.** Irrecoverable. Mitigated only by escrow and a tested restore drill, which are part of
  this ticket's done criteria, not a follow-up.
- **Silent wrong queries.** A future query that filters on an encrypted column compiles and returns
  nothing. The query guard is the control; it must be part of the build, not a review convention.
- **Scale.** In-API search grows linearly with the rows that pass the non-text filters. Record the
  ceiling in the spec so crossing it is a planned change, not a surprise.
- **Existing plaintext copies.** Recovery sets taken before the rollout, and any old volume
  snapshots, still contain plaintext. The runbook states how they are retired.
- **Document binaries stay in clear** until their own ticket, so "all candidate data encrypted" is
  not yet true for CV files. Say so in the release notes.
- **Out-of-app access.** Ad-hoc SQL, reporting tools and DBA queries see only ciphertext from now on.
  Confirm nobody depends on reading candidate data directly from PostgreSQL.

## Documentation to update

- `docs/ktl-33/` — encryption design summary, key custody and escrow, rotation runbook, backfill
  runbook, performance evidence and ceiling.
- `docs/ktl-5/database-conventions.md` — encrypted-column convention and the query-guard rule.
- `docs/ktl-10/search.md` and `docs/ktl-10/query-plans.md` — which search paths now run in the API
  and why the name indexes are gone.
- `docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md` — restores need the keys; retirement of pre-rollout
  plaintext recovery sets.
- `openspec/specs/` — candidate search and persistence requirements affected by the change.
- `README.md` (Spanish) — operator note on the key requirement to start the stack.
