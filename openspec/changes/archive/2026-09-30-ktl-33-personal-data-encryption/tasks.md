## 0. Create Feature Branch

- [x] 0.1 Create branch `feat/KTL-33` from an up-to-date `main` (after KTL-32 is merged, so its uncommitted work is not carried over)

## 1. Preparation and parity baseline

- [x] 1.1 Read `datcollate`, `datctype` and `datlocprovider` from the `postgres:17.6-alpine` database used by Compose and Testcontainers; record the result and the chosen in-memory comparer in `docs/ktl-33/design-notes.md`
- [x] 1.2 Add a parity fixture (accented, mixed-case, `%`/`_`-bearing names, e-mails, phones and notes) and capture today's SQL results for text search and last-name sort in both directions as expected data for later tasks; run it against the current code and confirm it passes

## 2. Encryption core (Application + Infrastructure)

- [x] 2.1 Add `IFieldProtector`, `FieldContext` and `IBlindIndex` abstractions under `Application/Abstractions/Encryption`
- [x] 2.2 Implement the `ktl1.<keyId>.<payload>` envelope and `AesGcmFieldProtector` (AES-256-GCM, random 96-bit nonce, `FieldContext` as associated data) in `Infrastructure/Encryption`
- [x] 2.3 Implement the key file model and lazy loader (`FieldEncryption:KeyFile`, default `/run/secrets/ktl_field_keys`) with validation: file present, JSON valid, every key 32 bytes, active ids present, purposes not sharing a key
- [x] 2.4 Implement `HmacBlindIndex` (`<keyId>.<hex HMAC-SHA256>` over `NormalizeEmail`) computing hashes under every configured blind-index key
- [x] 2.5 Add `EncryptedStringConverter`, the `IsEncrypted(table)` property extension and the `IModelCacheKeyFactory` keyed on the protector instance; register everything in `Infrastructure/DependencyInjection.cs`
- [x] 2.6 Unit tests: round-trip, fresh nonce per value, wrong `FieldContext` fails, tampered tag fails, unknown key id fails, envelope parsing, key-file validation cases, blind-index normalization and multi-key lookup; run `dotnet test backend/KeplerTalento.slnx --no-restore --filter "FullyQualifiedName~Encryption"` and confirm green

## 3. Schema and mappings

- [x] 3.0 Add `CandidateTextLimits` (current column widths) and `candidate.field.too_long` validation to create, update, relations and document upload; unit-test each rule
- [x] 3.1 Apply `IsEncrypted` to every column listed in design decision 8 and remove `HasMaxLength` from those columns; drop the name indexes and `CK_CND_Candidates_Name` from `CandidateConfiguration`
- [x] 3.2 Add the `EmailHash` shadow property and index on `CND_Candidates`, set by `CandidateRepository` on insert and whenever the e-mail changes
- [x] 3.3 Generate the `EncryptPersonalData` migration with `dotnet ef migrations add`; add the `NOT VALID` envelope check constraints and re-state `ktl_runtime` grants for `CND_Candidates` in the same migration
- [x] 3.4 Update `CandidateSchemaTests`, `SearchSchemaTests`, `DatabaseNamingTests` and any other schema test asserting the dropped indexes, lengths or check constraint; add tests that the envelope checks reject a direct plaintext insert and update and accept `NULL` on nullable columns

## 4. Fail-closed startup

- [x] 4.1 Add the `FieldKeyValidator` startup step on the serving path only (after the `--migrate`, `--reconcile` and `--purge-imports` branches in `Program.cs`), with stable diagnostic codes and no path or value in messages
- [x] 4.2 Add the plaintext probe (per encrypted table and filter-document `text` member) and the `migration_staging` schema check to the same step
- [x] 4.3 Wire the Compose secret for `api` only (not `migrator`), add the key file location to `.env.example` as a path, and add the default host folder to `.gitignore`
- [x] 4.4 Integration tests: API refuses to start with missing, malformed, short and shared keys, with plaintext present and with `migration_staging` present; `--migrate` succeeds without any key file

## 5. Filter documents

- [x] 5.1 Implement `FilterDocumentProtector` and use it in `SearchPresetRepository` and `PositionRepository` for the `text` member only
- [x] 5.2 Tests: preset and position round-trip with text, empty text stays empty, stored `jsonb` holds no plaintext term, existing preset and position suites unchanged

## 6. Search and import on encrypted data

- [x] 6.1 Implement `EncryptedSearchStage` in `CandidateSearchQuery` for requests with text or last-name sort, using the comparer chosen in 1.1; leave the default SQL path untouched
- [x] 6.2 Replace `ImportRunSupport.ExistingEmailsAsync` with the blind-index lookup
- [x] 6.3 Run the parity fixture from 1.2 against the new path and confirm identical results, order, pages and totals
- [x] 6.4 Add `EncryptedSearchPerformanceTests` (12 000 candidates, realistic note length) asserting p95 ≤ 300 ms for text search and last-name sort; replace the text and name-sort sections of `SearchQueryPlanTests`
- [x] 6.5 Add the query guard (`EncryptedColumnQueryGuard` interceptor, see design decision 9) reading the encrypted-property list from the EF model; `EncryptedColumnQueryGuardTests` confirm it refuses `Where(c => c.Email == x)`

## 7. Backfill, report and rotation tooling

- [x] 7.1 Add `ktl-migrate encryption generate-keys` (new file and `--add` for rotation)
- [x] 7.2 Add `ktl-migrate encryption backfill` (raw Npgsql, batches of 500 by id, optimistic guard per row, fills `EmailHash`, runs as `ktl_runtime`)
- [x] 7.3 Add `ktl-migrate encryption report` (counts per table and key id, decrypts everything, fails on plaintext, unknown key or authentication failure)
- [x] 7.4 Integration tests: backfill over a plaintext database, re-run is a no-op, interrupted run resumes, rotation with two keys leaves everything readable and moves every value to the new key, blind-index rotation keeps import deduplication working, row counts unchanged

## 8. Security evidence

- [x] 8.1 Integration test: create candidates, notes, experience, education, documents, a preset and a position through the API, then read every encrypted column over a raw connection and assert no seeded value appears
- [x] 8.2 Integration test: run `pg_dump` against the seeded Testcontainers database and assert the dump contains none of the seeded names, e-mails, phones or saved search term
- [x] 8.3 Tests that a ciphertext copied into another column or tampered with returns a stable server error, and that `EmailHash` never appears in any serialized candidate response
- [x] 8.4 Log assertions across the new tests: no key material, key file path, plaintext or ciphertext in captured logs
- [x] 8.5 Confirm the existing unauthenticated/unauthorized tests for search, list, detail, import, presets and positions still pass unchanged

## 9. Update existing tests and run the suites

- [x] 9.1 Review and update every existing unit and integration test affected by the change (test fixtures now need a test key set; schema assertions; search plan evidence; import deduplication)
- [x] 9.2 Provision the development key file (`ktl-migrate encryption generate-keys`) and bring the development database to no plaintext (done by rehearsing the production rollout rather than recreating it: backup `20260929T152844Z`, migrate, backfill as `ktl_runtime`, report clean, `VACUUM FULL`)
- [x] 9.3 Run `npm run test:backend` and confirm green; inspect a test database to verify encrypted columns hold only envelopes and `EmailHash` is populated (2026-09-30: 773 unit + 458 integration green after making the `SearchQueryPlanTests` skill-index assertion accept either `CandidateId`-led index; inspected the development database, since Testcontainers databases are discarded: 0 non-envelope values across every encrypted column and filter-document `text` member, 505/505 `EmailHash` values well-formed)
- [x] 9.4 Run `npm test` from `frontend/` and confirm green (no frontend change expected)
- [x] 9.5 Run `npm run build:all`, `npm run lint`, `npm run format:check`, `npm run security:rls` and `npm run security:storage` from `frontend/` and confirm each passes

## 10. End-to-end verification

- [x] 10.1 Rebuild and start the stack with `docker compose up --build` using the development key file; confirm the API starts and `--migrate` ran without keys
- [x] 10.2 Run `npm run e2e` from `frontend/` and confirm the full Playwright suite passes unchanged, in particular `candidate-crud`, search, import and position specs
- [x] 10.3 Stop the stack, remove the key file mount, start again and confirm the API refuses to start with the missing-key diagnostic; restore the mount afterwards
- [x] 10.4 Take a backup with `scripts/operations/backup.ps1`, run `pg_restore --data-only` to a text file and confirm it contains no candidate name created by the e2e run; delete the backup afterwards

## 11. Documentation

- [x] 11.1 Write `docs/ktl-33/`: design summary, key generation and escrow, rotation runbook, production rollout runbook (design Migration Plan), performance evidence and ceiling
- [x] 11.2 Update `docs/ktl-5/database-conventions.md` (encrypted-column convention, envelope checks, query guard), `docs/ktl-10/search.md` and `docs/ktl-10/query-plans.md` (API-side text and name-sort path)
- [x] 11.3 Update `docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md`: restores need the escrowed key file; retirement of pre-rollout plaintext recovery sets
- [x] 11.4 Update `README.md` (Spanish): generating the development key file and the key requirement to start the stack
- [x] 11.5 Release notes: operational breaking change (key file required; direct SQL sees ciphertext) and that CV files on disk are not yet encrypted
