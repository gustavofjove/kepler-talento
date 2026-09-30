## Context

See `proposal.md` for motivation and `specs/` for the required behavior. The facts that shape the
approach:

- Candidate data is read and written only through `ApplicationDbContext` (the API and the
  operator tool `ktl-migrate`, which reuses Infrastructure). No other process writes candidate
  tables.
- Only three places make PostgreSQL look inside personal values: the text filter
  (`ILIKE` over five columns) and the last-name sort in `CandidateSearchQuery`, and
  `ImportRunSupport.ExistingEmailsAsync` (`lower("Email")`). Every other read is by identifier.
- Search filter documents (presets in `ADM_SearchPresets."Filters"`, position requirements in
  `OPS_Positions."Requirements"`) are `jsonb` written and parsed by one serializer,
  `SearchFilterDocument`, and carry check constraints on their JSON shape and version.
- The `migrator` container is the API image run with `--migrate` as `ktl_migrator`. It must never
  hold the keys, so no key-dependent work can happen inside an EF migration.
- `ktl_runtime` already holds `SELECT, INSERT, UPDATE` on every table this change encrypts.
- The dataset is about 3 000 candidates; PostgreSQL is `postgres:17.6-alpine`.

## Goals / Non-Goals

**Goals:**

- Ciphertext-only storage for the columns listed in the personal-data-encryption spec, transparent
  to handlers, endpoints and the SPA.
- One mechanism, reused by the API, the backfill and rotation.
- A rollout that cannot leave the application serving a half-encrypted database.

**Non-Goals:**

- Hiding the existence or approximate length of a value (a `NULL` stays `NULL`; ciphertext length
  follows plaintext length).
- Protecting against an attacker who controls the running API host; that attacker can read the key
  file. The threat addressed is a copy of the database or its backups.
- Changing any API contract, permission or audit event.

## Decisions

### 1. Application-level encryption through EF Core value converters

An `IFieldProtector` in `Application/Abstractions/Encryption` exposes
`Protect(string plaintext, FieldContext context)` / `Unprotect(...)`, where `FieldContext` is the
physical `"<Table>.<Column>"` name. `AesGcmFieldProtector` in `Infrastructure/Encryption` implements
it with `System.Security.Cryptography.AesGcm` (256-bit key, 96-bit random nonce, 128-bit tag,
`FieldContext` as associated data). An `EncryptedStringConverter` is applied in each entity
configuration through one extension, `builder.Property(x => x.FirstName).IsEncrypted(Table)`, so
the list of encrypted columns is greppable and testable.

Value converters were chosen over a `SaveChanges`/materialization interceptor because converters
also run for scalar projections (`.Select(c => new CandidateSearchItem(c.FirstName, ...))`), which
most list and search reads use; an interceptor would only see tracked entities.

The model is cached per context type, so the converter holds the singleton protector, and
`ApplicationDbContext` registers an `IModelCacheKeyFactory` keyed on the protector instance so tests
using different key sets get separate models.

_Alternatives:_ `pgcrypto` (rejected: the key travels in SQL text and can reach server logs and
`pg_stat_statements`); PostgreSQL TDE (not in community PostgreSQL; does not protect logical dumps);
ASP.NET Core Data Protection (rejected: its payloads are not bound to a column, its key ring would
need its own persistence outside the database, and it adds nothing over `AesGcm` here).

### 2. Envelope format and column type

A stored value is the string `ktl1.<keyId>.<base64url(nonce ‖ ciphertext ‖ tag)>` in a `text`
column. `text` over `bytea` because the same envelope must also live inside `jsonb` filter
documents, and one format is simpler to test and inspect.

**Length limits move out of the column type.** Today the `varchar(n)` width is the _only_ limit on
candidate text: create, update, relations and document upload have no length validation, and an
overlong value fails inside PostgreSQL as an unhandled error. The envelope is about 4/3 of the UTF-8
length plus 40 characters, so the widths cannot stay. The limits keep their current values and move
to `CandidateTextLimits` in `Domain/Candidates`, which is used in two places:

- FluentValidation rules on `CreateCandidate`, `UpdateCandidate`, `SetCandidateRelations` and
  `UploadCandidateDocument`, with one stable code `candidate.field.too_long` and a Spanish message.
  An overlong value becomes a 400 instead of a 500.
- The encryption converter, which refuses to encrypt a value longer than its column's limit. This is a
  backstop for writers that bypass the validators (the legacy loader, direct test writes), so no
  path can store more than it could before.

Columns that were unbounded `text` before (candidate notes, relation notes, experience functions)
stay unbounded.

Each encrypted column gets a check constraint `"<Col>" LIKE 'ktl1.%'` (or `IS NULL OR ...` for
nullable columns), added `NOT VALID` by the schema migration. PostgreSQL enforces a `NOT VALID`
check on every insert and update immediately, without checking existing rows, which is exactly the
rollout guarantee: from the migration onward nothing can write plaintext, and the backfill's own
updates satisfy it. Validating the constraints for existing rows needs table ownership, so it is a
follow-up migration after production reports a complete backfill (see Migration Plan).

`NULL` stays `NULL`. Empty strings are encrypted like any other value, so the required
`NOT NULL` columns keep rejecting a missing value (candidate-persistence spec).

### 3. Keys: generated random keys in a mounted key file, one key per purpose

The API reads a JSON key file from `FieldEncryption:KeyFile` (default
`/run/secrets/ktl_field_keys`, mounted as a Compose secret from a host path outside the repository):

```json
{
  "encryption": { "active": "e1", "keys": { "e1": "<base64 32 bytes>" } },
  "blindIndex": { "active": "b1", "keys": { "b1": "<base64 32 bytes>" } }
}
```

Keys are generated by `ktl-migrate encryption generate-keys`, which uses
`RandomNumberGenerator` and writes a new file (or adds a key to an existing one for rotation). No
password derivation. Escrow is a copy of this file in the corporate password manager or a printed
copy in a safe, held by two named people; a restore drill proves it (Migration Plan step 8).

_One key per purpose rather than envelope encryption (KEK wrapping per-row DEKs):_ without a vault or
HSM the KEK would sit in the same file as the data keys, so wrapping would add code and a second
rotation procedure without separating anything. Key identifiers in every envelope already give
rotation without a flag day. If a corporate vault becomes available, the key file loader is the
only component to replace.

### 4. Fail closed at startup, but not in the migrator

A `FieldKeyValidator` hosted-startup step runs on the serving path only (after the `--migrate`,
`--reconcile` and `--purge-imports` branches in `Program.cs`), so the migrator never needs the
keys. It refuses to start when the file is missing, unreadable, malformed, a key is not 32 bytes,
the active id is absent, or the two purposes share a key. It then runs the plaintext probe: one
query per encrypted table counting rows where any encrypted column is non-null and not
`LIKE 'ktl1.%'`; a non-zero count stops startup with a diagnostic naming the table. The probe is a
sequential scan (a few milliseconds at 3 000 rows, well under a second at 12 000) and is removed by
the follow-up migration that validates the constraints. Diagnostics use stable codes
(`encryption.key.missing`, `encryption.plaintext.present`, ...) and never the path or the value.

The protector itself loads keys lazily, so the `--migrate` path can build the model without them.

### 5. E-mail blind index with key-id prefix

`CND_Candidates."EmailHash"` is `text` holding `<blindKeyId>.<hex HMAC-SHA256(key,
normalize(email))>`, where `normalize` is the existing `CandidateImportRowEvaluator.NormalizeEmail`
(trim, lower-invariant). It is non-unique (the product allows two candidates to share an e-mail;
import decides duplicates by rule) and indexed. `Candidate` gets no new domain property; the
repository sets it through an EF shadow property on every insert and on every update where the
e-mail changed, so it cannot drift from the e-mail.

Lookups compute the hash under every configured blind-index key and query `IN (...)`. During a
blind-index rotation both keys are configured and lookups keep working; afterwards the old key is
removed. `ExistingEmailsAsync` becomes a hash lookup returning the matched hashes, mapped back to
the input e-mails in memory.

### 6. Search: SQL for everything clear, API for text and name order

`CandidateSearchQuery.MatchingAsync` keeps building the SQL predicate for status, catalog families,
tags, CV presence and visibility, unchanged. When the request has no text and does not sort by
last name, `Page` runs exactly as today. Otherwise a new `EncryptedSearchStage`:

1. Reads `(Id, FirstName, LastName, Email, Phone, Notes, UpdatedAtUtc, Status)` for every row of the
   SQL predicate — the converters decrypt on materialization.
2. Filters with `value.Contains(text, StringComparison.OrdinalIgnoreCase)` over the five fields. This
   is a literal match, so `%` and `_` need no escaping; it is accent-sensitive and case-insensitive,
   like `ILIKE` today.
3. Orders by the requested field with the identifier ascending as final tie-breaker, counts, and
   slices the page.
4. Loads the page's full `CandidateSearchItem` projection by identifier with the existing SQL
   projection, preserving the page order.

Ordering parity: the in-memory comparer must reproduce the database's current `ORDER BY "LastName"`.
The first implementation task reads `datcollate`/`datlocprovider` from the target image and pins
the comparer accordingly (ordinal for the `C`/musl case, `CompareInfo` for ICU), and a parity test
compares the old SQL order with the new order on an accented, mixed-case dataset before the SQL
path is removed. Ordering by `updatedAt` or `status` inside the API stage uses the same comparisons
as SQL (timestamps and ASCII status codes).

No decrypted value is cached between requests (plaintext lives only for the request).

Performance budget and ceiling come from the candidate-search delta: p95 ≤ 300 ms at 12 000 active
candidates. Evidence is a new `EncryptedSearchPerformanceTests` (integration, Testcontainers,
12 000 seeded candidates with realistic note lengths), replacing the text and name-sort sections of
`SearchQueryPlanTests`. Beyond the ceiling the documented next step is a word-level blind index
(out of scope).

### 7. Filter documents: encrypt the `text` member only

Encrypting the whole `jsonb` column would break its shape and version check constraints and the
`SearchFilterDocument` contract. Instead, `SearchPresetRepository` and `PositionRepository` pass the
document through a `FilterDocumentProtector` that replaces a non-empty `text` member with its
envelope on write and restores it on read, with `FieldContext` `"ADM_SearchPresets.Filters.text"` /
`"OPS_Positions.Requirements.text"`. `SearchFilterDocument` and the filter schema version do not
change. The plaintext probe and the backfill treat a non-empty `text` member without the `ktl1.`
prefix as plaintext.

### 8. Columns encrypted

Rule: every human-entered free-text column on a `CND_` table is encrypted; machine values are not.

| Table                                                               | Encrypted                                                                                   | Left in clear (reason)                                                                      |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `CND_Candidates`                                                    | FirstName, LastName, Phone, Email, Location, Province, Country, Availability, Source, Notes | Status (closed vocabulary, filtered), SourceKey (legacy row id, unique index), dates, flags |
| `CND_CandidateNotes`                                                | Body                                                                                        | Author, dates, IsActive                                                                     |
| `CND_CandidateExperience`                                           | Company, Position, Functions, Notes                                                         | Sector reference, dates, SourceKey                                                          |
| `CND_CandidateEducation`                                            | Degree, Specialty, Institution, Notes                                                       | Type/status references, dates, SourceKey                                                    |
| `CND_CandidateLanguages`                                            | Certification, Notes                                                                        | Catalog references                                                                          |
| `CND_CandidateSkills`, `CND_CandidatePrograms`, `CND_CandidateTags` | Notes (where present)                                                                       | Catalog references                                                                          |
| `CND_Documents`                                                     | OriginalFileName                                                                            | StorageKey (opaque), ContentType, Sha256, DocumentType, scan codes                          |
| `ADM_SearchPresets`, `OPS_Positions`                                | `text` member of the filter document                                                        | Name/title (business labels), criteria, version                                             |

`Country`, `Availability` and `Source` are encrypted (brief decision 4): no query filters or sorts on
them, so encrypting costs nothing and removes a judgment call. `AUD_Events` holds identifiers and
codes only and is unchanged. User accounts (`ADM_Users`) are out of scope.

### 9. Query guard

`EncryptedColumnQueryGuard`, an `IQueryExpressionInterceptor` registered by `ApplicationDbContext`
itself, inspects every LINQ query before EF compiles it. It throws `EncryptedColumnQueryException`,
naming the column, when an encrypted property (or `EF.Property` on one) appears inside a filtering,
ordering, grouping, joining or aggregate operator, including correlated subqueries. Projections are
allowed. The encrypted properties come from the EF model, so the guard cannot drift from the
configuration.

_Changed during implementation:_ the first plan was a source scan in a unit test, so that code paths
no test executes would also fail. A scan that can tell `Candidate.Email` (encrypted) from
`User.Email` (clear) needs semantic analysis, which means adding Roslyn as a dependency. The runtime
guard needs nothing new, runs in production as well, and every integration test that executes a
query exercises it. Its first real catch was a test ordering candidates by e-mail in SQL.
`EncryptedColumnQueryGuardTests` pins its behaviour without a database.

### 10. Backfill and rotation in `ktl-migrate`

New commands in `Tools/DataMigration`:

- `encryption generate-keys --out <file> [--add encryption|blindIndex]`
- `encryption backfill --connection <runtime connection> --keys <file>` — per table, in batches of
  500 by `Id`, reads raw values with Npgsql (not EF, so it sees plaintext), encrypts values lacking
  the `ktl1.` prefix or carrying a non-active key id, fills `EmailHash`, and updates each row with an
  optimistic guard (`WHERE "Id" = @id AND "<Col>" = @old`). Idempotent and resumable by
  construction. Runs as `ktl_runtime`, never `ktl_migrator`.
- `encryption report` — counts per table and key id, decrypts every value, and fails on any
  plaintext, unknown key id or authentication failure. Rotation is `generate-keys --add`, change the
  active id, restart the API, `backfill`, `report`, then remove the old key.

Row counts are compared before and after inside the report; the tool never inserts or deletes.

### Authorization, storage and principles

No endpoint, permission or policy changes; every existing fail-closed check stays in front of the
handlers. Roles: the migration only alters columns, adds checks, an index and a column; runtime
grants on existing tables already cover the new column, and the migration re-states them for
`CND_Candidates` so the grant test sees them in the same slice. Documents on disk are unaffected.
There is no departure from the standing principles; this change strengthens principle 1.

### Test strategy

- Unit: protector round-trip, wrong column context, tampered tag, unknown key id, envelope parsing;
  key-file validation cases; blind-index normalization; filter-document protector; query guard.
- Integration (Testcontainers): raw-SQL plaintext absence for every encrypted column; `pg_dump`
  of a seeded database scanned for seeded values; `NOT VALID` checks reject a direct plaintext
  write; startup refusal with missing keys and with plaintext present; backfill idempotency,
  interruption and report; rotation with two keys; the full existing `SearchApiTests`,
  `ImportApiTests`, `CandidateApiTests` and position suites unchanged; ordering parity; performance.
- Log assertions: no key material, key path, plaintext or ciphertext in captured logs.
- E2E: the existing Playwright suite unchanged, with the development key file provisioned.

## Risks / Trade-offs

- [Key loss makes all candidate data unrecoverable, backups included] → Escrow with two named
  holders and a restore drill are rollout steps, not follow-ups; the report command proves the
  escrowed file decrypts a restored database.
- [A future query filters on an encrypted column and silently returns nothing] → Query guard in the
  unit suite, reading the encrypted-property list from the EF model.
- [In-API search grows linearly] → Documented 12 000-candidate ceiling and a performance test at
  that size; the word-level blind index is the planned next step.
- [Ordering or matching drifts from today's SQL behavior] → Parity tests run the old and new paths on
  the same accented dataset before the SQL path is deleted; the collation is read, not assumed.
- [Old plaintext survives in table files, WAL and earlier backups] → `VACUUM FULL` after the
  backfill; the runbook retires pre-rollout recovery sets and snapshots.
- [The legacy migration staging schema holds plaintext while `ktl-migrate load` runs, and is
  retained on failure] → Runbook: the plaintext probe is extended to fail startup when
  `migration_staging` exists, so a failed load cannot be forgotten into the next backup.
- [One damaged or foreign ciphertext makes every text search and last-name sort fail, because the
  stage decrypts all candidate rows the other filters leave] → Accepted as fail-closed: a partial
  result would hide candidates silently. `ktl-migrate encryption report` names the table and column
  with the damaged value.
- [Blind index reveals which candidates share an e-mail] → Accepted: equality is exactly what import
  needs; no other column gets a blind index.
- [Direct SQL and reporting lose readable data] → Called out as an operational breaking change in the
  release notes; the report command is the supported way to inspect encryption state.
- [The startup probe scans every encrypted table] → Measured cost is small at the ceiling; it is
  removed once the constraints are validated.

## Migration Plan

Development and test databases start empty or are recreated, so they need only the key file.
Production (maintenance window, API stopped):

1. Generate the key file with `ktl-migrate encryption generate-keys`, store it on the host outside
   the repository, and escrow it with the two named holders.
2. Take a normal backup (it is plaintext; it is retired at step 9).
3. Deploy the release; the `migrator` applies `EncryptPersonalData` (columns to `text`, name
   indexes and `CK_CND_Candidates_Name` dropped, `EmailHash` and index added, `NOT VALID` envelope
   checks added, grants re-stated). The API refuses to start: the probe finds plaintext.
4. Run `ktl-migrate encryption backfill` as `ktl_runtime` with the key file.
5. Run `ktl-migrate encryption report`; it must pass.
6. Run `VACUUM FULL` on the affected tables as `ktl_migrator` from `db-tools`.
7. Start the API; the probe passes. Smoke-test search, detail and import.
8. Restore drill: restore a new backup into a non-production environment with the escrowed key file
   and run `report` against it.
9. Retire the plaintext recovery sets from before step 3.

Rollback: before step 4, roll the migration back and redeploy the previous release. After step 4,
rollback is restoring the step-2 backup (the documented restore procedure); there is no in-place
decrypt command, deliberately.

Follow-up (separate small change once production has run step 5): a migration that runs
`VALIDATE CONSTRAINT` on every envelope check and removes the startup probe.

## Open Questions

- Names of the two escrow holders and where the escrow copy is kept. It does not change the design
  or tasks; it must be answered before production step 1.
- Whether a corporate vault becomes available later; only the key file loader would change.
