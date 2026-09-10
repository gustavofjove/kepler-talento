## 0. Create Feature Branch

- [x] 0.1 Create and check out `feat/KTL-7` from `main`, confirming the archived KTL-6 catalog
      work it depends on (`CAT_CatalogItems`, `CatalogName.DeriveCode`, seeded families) is
      present.

## 1. Candidate schema — domain and mapping

_Spec: `candidate-persistence`. Design: "KTL-7 ships the candidate schema", "Catalog
references are enforced by composite foreign key"._

- [x] 1.1 Expand `backend/Domain/Candidates/Candidate.cs` to the full field set (phone,
      email, location, province, country, availability, status, source, notes, `ReceivedAt`,
      `ConsentAt`, `ReviewDueAt`), keeping `IsActive`, `DeletedAtUtc` and `Version`. Add a
      `CandidateStatus` value with the closed set `new`, `available`, `in_process`, `hired`,
      `rejected`. No field gets a permissive default.
- [x] 1.2 Add relation entities under `backend/Domain/Candidates/`: `CandidateLanguage`,
      `CandidateProgram`, `CandidateEducation`, `CandidateExperience` and `CandidateSkill`, each
      owned by exactly one candidate and matching the field set in
      `src/app/features/candidates/models/candidate.models.ts`. Do **not** add a document entity —
      KTL-5's `CandidateDocument` / `CND_Documents` is the document record.
- [x] 1.3 Extend `backend/Domain/Documents/CandidateDocument.cs` with `DocumentType` and
      `IsPrimary`, leaving its existing scan-state transitions intact.
- [x] 1.4 Add a `SourceKey` property to the candidate, each relation entity, and
      `CandidateDocument` (nullable, legacy provenance only). Add `SourceLoadedAtUtc` and the
      derived `HasApplicationChangesSinceLoad` to the candidate, with a check constraint that a
      load moment cannot exist without a source key.
- [x] 1.5 Update `CandidateConfiguration` and add configurations for each relation table:
      quoted `CND_` names, keys, required fields, the status check constraint, and cascade
      behavior that preserves relations when a candidate is logically deleted.
- [x] 1.6 Add the unique index on `CAT_CatalogItems (Id, Family)` and, on each relation
      table, the fixed `Family` column with its check constraint plus the composite foreign key
      onto `(Id, Family)`.
- [x] 1.7 Extend `DocumentConfiguration` with the new columns and the partial unique index
      enforcing at most one primary document per candidate (`WHERE "IsPrimary"`).
- [x] 1.8 Add access-path indexes (candidate by identifier, active candidates by update
      recency, each relation by owning candidate) and the unique partial index on `SourceKey`.
- [x] 1.9 Register the new sets on `ApplicationDbContext`.

## 2. Candidate schema — migration and grants

- [x] 2.1 Generate the EF Core migration creating the expanded `CND_Candidates` and the five
      relation tables, and altering `CND_Documents`, with all constraints and indexes from
      section 1.
- [x] 2.2 Extend the runtime-role grants so the migration ships least privilege for the new
      tables in the same deployment action; the runtime role gets no schema-modification
      privilege on `CND_` tables.
- [x] 2.3 Update `DatabaseInitializer.SeedSyntheticReferenceAsync` for the expanded
      constructor, keeping the seed candidate synthetic and giving it explicit consent metadata.
- [x] 2.4 Extend `backend/Tests/UnitTests/Persistence/DatabaseNamingTests.cs` to cover the
      new tables' prefixes and quoted casing.
- [x] 2.5 Extend `backend/Tests/IntegrationTests/PostgreSqlPersistenceTests.cs` to apply the
      migration against disposable PostgreSQL and assert: full field round-trip, rejection of an
      out-of-set status, rejection of a relation pointing at a catalog entry of the wrong family,
      rejection of a relation with a missing candidate, consent metadata absent when not
      supplied, relations preserved across a logical delete, and rejection of a second primary
      document for one candidate.
- [x] 2.6 Add an integration test asserting the runtime role cannot alter or drop a `CND_`
      table while its permitted reads and writes succeed.

## 3. Export contract and fixtures

_Spec: `legacy-data-migration` — "Documented export contract", "Synthetic fixtures outside
production runs"._

- [x] 3.1 Define the export contract: one UTF-8 delimited file per entity, required columns,
      the per-row source-identifier column, date and boolean encodings, and the document manifest
      (source key, relative path, SHA-256).
- [x] 3.2 Write `docs/ktl-7/access-export-procedure.md` — how an operator produces the export
      set from `BBDD CVs.accdb` on Windows, where to hold it, and the requirement to destroy it
      after a reconciled run.
- [x] 3.3 Build a synthetic fixture set under `backend/Tests/` covering: clean rows,
      unresolved catalog values, missing consent metadata, malformed contact fields, logically
      removed source rows, and documents that are clean, hash-mismatched, and rejected by the
      scanner. Embed sentinel strings in every personal-data field.

## 4. Migration tool skeleton

_Design: "A standalone CLI, not a durable worker operation"._

- [x] 4.1 Create `backend/Tools/DataMigration` as a console project referencing
      `Infrastructure`, added to `KeplerTalento.slnx`, with `validate`, `load` and `report`
      verbs and arguments for connection string, export directory, mapping file, output path and
      `--pre-migration-backup`.
- [x] 4.2 Make `load` refuse to run without `--pre-migration-backup`, and make the tool use
      the migration database role only.
- [x] 4.3 Add the `OPS_MigrationRuns` entity, configuration and migration (run id, verb,
      timestamps, counts, outcome, backup label), and record a run row for every invocation.
- [x] 4.4 Extend `backend/Tests/UnitTests/Architecture/ProjectDependencyTests.cs` so the tool
      cannot be referenced by `Web`, `Application` or `Domain`, keeping the bulk-data path out of
      the API.

## 5. Staging and validation

_Spec: "Validate before writing business data", "Protected staging representation"._

- [x] 5.1 Create and drop the `migration_staging` schema from the tool; copy the export into
      it verbatim as text.
- [x] 5.2 Implement structural validation — missing files, missing columns, missing source
      identifiers, wrong encoding — failing before any business write with a specific message.
- [x] 5.3 Implement per-row validation producing every problem in one pass, each as
      `(entity, sourceKey, field, reasonCode)`.
- [x] 5.4 Drop the staging schema on a successful load; on failure retain it and print that it
      exists and must be dropped.

## 6. Reference-value resolution

_Spec: "Reference-value resolution without silent creation"._

- [x] 6.1 Implement the three-step resolver: exact `Name` match, then
      `CatalogName.DeriveCode` match, then the operator `mappings.csv`. No fuzzy matching.
- [x] 6.2 Reject every row carrying an unresolved value and collect unresolved values with
      family and occurrence counts; never insert into `CAT_CatalogItems`.
- [x] 6.3 Unit-test the resolver: exact hit, code-normalized hit, mapping-file hit, unresolved
      value, and a mapping entry pointing at a nonexistent catalog code.

## 7. Load

_Spec: "Idempotent re-runnable load", "Per-candidate transactional load", "Consent and
retention metadata fidelity", "Logical state fidelity"._

- [x] 7.1 Implement the upsert-on-`SourceKey` load, one transaction per candidate aggregate,
      leaving application-created rows (null source key) untouched.
- [x] 7.2 Carry received, consent and review-due dates across exactly; reject rows whose
      consent metadata cannot be established, with a consent reason code.
- [x] 7.3 Carry source logical-removal state across as logical state, counted as loaded.
- [x] 7.4 Stamp `SourceLoadedAtUtc` on every migration write, and skip any candidate whose
      `HasApplicationChangesSinceLoad` is true, reporting it with an application-changed reason.
      Overwrite only when `--overwrite-app-edits` is passed, and report how many were overwritten.
- [x] 7.5 Report unmatched target records — source keys present in `CND_` tables but absent
      from the export — counted separately from rejections and skips, changing nothing about them.
- [x] 7.6 Integration-test against disposable PostgreSQL: full run over fixtures, a second
      identical run producing unchanged counts and no duplicates, a corrected re-run updating in
      place, and a mid-candidate relation failure leaving nothing partial.
- [x] 7.7 Integration-test the newer-export path: a record edited through the application is
      skipped and reported, is overwritten only with `--overwrite-app-edits`, an untouched record
      still updates without the flag, and a record whose source key vanished from the export is
      reported as unmatched and left unchanged.

## 8. Document migration

_Spec: `private-document-storage` deltas — quarantine for migrated content, ingested content
verification._

- [x] 8.1 Stream each manifest document through `WriteQuarantineAsync`, compare the returned
      SHA-256 against the manifest hash, run the registered `IMalwareScanner`, and promote only on
      a clean result — writing a `CND_Documents` row and driving `MarkClean` / `MarkUnavailable`
      rather than setting scan state directly.
- [x] 8.2 Reject hash mismatches, infected and unscannable documents; report them by document
      source key with a reason code and no content, storage key or path.
- [x] 8.3 Integration-test with `FakeMalwareScanner`: a clean document becomes available under
      an opaque key with a matching stored hash; a mismatched and an infected document never
      become available and are reported.

## 9. Reconciliation report

_Spec: "Explicit row outcomes", "Reconciliation report", "No personal data in migration
output"._

- [x] 9.1 Emit `reconciliation-<runId>.json` and a Markdown rendering: per-entity source /
      loaded / rejected / skipped counts, rejections, unresolved values, document hash results,
      application-changed skips, unmatched target records, and the pre-migration backup label in
      the header.
- [x] 9.2 Give the report writer an API that accepts no free-form field value for rejections,
      so a field value has no path into the report.
- [x] 9.3 Report a run as not reconciled, explicitly, when loaded target counts do not match
      the accounted-for source rows.
- [x] 9.4 Implement `report` re-emitting a recorded run's report from `OPS_MigrationRuns`.
- [x] 9.5 Test that loaded + rejected + skipped equals the source count for every entity.

## 10. Rollback

_Spec: "Documented rollback"._

- [x] 10.1 Document the rollback in `docs/ktl-7/migration-runbook.md`: restore the backup
      named in the report header, then re-run from validation.
- [x] 10.2 Add the post-migration reconciliation check to
      `docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md`.
- [x] 10.3 Integration-test rollback: snapshot → migrate → restore → assert the state matches
      the snapshot and a subsequent `load` proceeds from it.

## 11. Personal-data and security evidence

_Mandatory for a change touching personal data, database grants and storage._

- [x] 11.1 Add the sentinel test: run a migration over fixtures hitting every rejection reason
      code, capture the tool's log output and both report files, and assert no sentinel value,
      storage key, drive letter, UNC path or host directory appears in any of them.
- [x] 11.2 Assert the tool exposes no HTTP surface and that no API endpoint triggers a
      migration.
- [x] 11.3 Confirm Serilog redaction covers the new candidate fields, and that the tool's
      logging never receives entity values.
- [x] 11.4 Assert the staging schema does not exist after a successful run.

## 12. Test and verification runs

_Every command below must actually be executed and its output inspected, not described._

- [x] 12.1 Review and update existing unit tests affected by the expanded `Candidate` —
      `GetReferenceCandidateTests`, `DatabaseNamingTests`, and any catalog test touching
      `CAT_CatalogItems` indexes.
- [x] 12.2 Run the backend unit suite (`dotnet test backend/Tests/UnitTests`) and confirm it
      passes.
- [x] 12.3 Run the backend integration suite (`dotnet test backend/Tests/IntegrationTests`)
      against disposable PostgreSQL and inspect the resulting database and document-storage state.
- [x] 12.4 Run the frontend unit suite (`npm run test:unit`) to confirm the untouched frontend
      still passes, and confirm the legacy Supabase integration checks for unchanged legacy paths
      still pass.
- [x] 12.5 Run the Playwright regression for the paths this change could disturb — the
      catalogs CRUD spec and the candidate flows — and restore seed data afterwards. This change
      ships no frontend work, so this run is proving nothing regressed.
- [x] 12.6 Run `npm run lint` and `npm run format:check`.

## 13. Documentation

- [x] 13.1 Write `docs/ktl-7/migration-runbook.md` covering the full operator sequence from
      the design's Migration Plan, including destroying the export set, the newer-export path
      (application-changed skips, `--overwrite-app-edits`, unmatched target records), and an
      explicit note that this tool is not the admin "Importación" screen.
- [x] 13.2 Re-read the `ktl-8-candidate-core-writes-api-cutover` (KTL-8) `candidate-management`
      spec against the delivered schema and confirm every obligation in the design's "What KTL-8
      expects of this schema" is met. Any gap is a defect in this change, not a KTL-8 task.
- [x] 13.3 Update `README.md` and `backend` documentation with the new tool, its verbs, and
      the fact that it is operator-run and never reachable over HTTP.
