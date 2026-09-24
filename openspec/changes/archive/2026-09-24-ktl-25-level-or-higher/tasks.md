## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-25` before implementation (all KTL-25 requirements).

## 1. Backend Level Resolution

- [x] 1.1 Update the search filter contract comments to document empty-level and minimum-level
      semantics while keeping `FilterSchemaVersion` at `1` (candidate-search: Multi-value criteria
      semantics; saved filters story).
- [x] 1.2 Widen catalog resolution to load each referenced level family with `SortOrder`, including
      inactive values, and expand a named level to its own and all higher IDs while unknown levels
      remain empty/fail closed (candidate-search: minimum, reorder, inactive, and unknown scenarios).
- [x] 1.3 Preserve the correlated skill, language, and program existence predicates and verify that
      `ANY`, `ALL`, and duplicate-result behavior use the expanded ID sets without rewriting criteria
      (candidate-search: `ANY`, `ALL`, and deduplication scenarios).
- [x] 1.4 Review and update affected backend unit tests that document exact-level matching,
      normalization, or schema versioning (candidate-search: all minimum-level scenarios).

## 2. PostgreSQL Integration and Parity Evidence

- [x] 2.1 Extend `SearchApiTests` with language, skill, and program minimum levels, highest level,
      reordered and inactive levels, unknown levels, `ANY`/`ALL`, and no-duplicate cases
      (candidate-search: observable matching scenarios).
- [x] 2.2 Update `ReferenceSearchEvaluator` and `SearchParityFixture` with ordered level families and
      the same minimum-level rule, then run the affected parity integration tests against PostgreSQL
      and inspect the resulting catalog and candidate relation state (candidate-search: parity across
      all families).
- [x] 2.3 Review and update preset and position matching integration tests, assert filters remain
      stored byte-for-byte while results include higher levels, and run those tests against PostgreSQL
      (candidate-search: saved-filter scenario).
- [x] 2.4 Re-capture and run `SearchQueryPlanTests`; verify an index-backed correlated existence
      predicate with bounded `LevelId IN (...)`, no outer catalog join, and no duplicate candidates
      (candidate-search: performance and deduplication success criteria).

## 3. Frontend Minimum-Level Presentation

- [x] 3.1 Add Spanish i18n keys for `≥ {{level}}`, `Nivel mínimo`, and the catalog order
      hint; optionally mirror them in English (candidate-search labels; business-catalogs order hint).
- [x] 3.2 Format non-empty levels as minimums in search chips and `SearchCriteriaSummary`, and label
      the KTL-24 picker control `Nivel mínimo` without changing candidate edit displays
      (candidate-search: level label scenarios).
- [x] 3.3 Show the order-semantics hint in catalog management for `language_level`,
      `program_level`, and `skill_level` only (business-catalogs: level and non-level hint scenarios).
- [x] 3.4 Review and update affected frontend unit tests for summary and picker labels and catalog
      family hints, using roles, accessible names, or `data-testid` rather than new literal-copy
      selectors (candidate-search labels; business-catalogs order hint).

## 4. Security and Boundary Verification

- [x] 4.1 Run backend authorization tests proving unauthenticated and unauthorized callers are
      refused before validation or catalog resolution and no search terms or levels enter logs
      (candidate-search authorization success criterion; personal-data principle).
- [x] 4.2 Run least-privilege database and RLS/security checks and verify KTL-25 adds no migration,
      runtime grant, role, or browser database path (candidate-search security boundary).
- [x] 4.3 Run private-storage and `frontend/tests/security` checks and verify result projections still
      expose no storage path, key, document internals, or additional personal data
      (candidate-search minimal projection and security boundary).

## 5. End-to-End Verification

- [x] 5.1 Extend `advanced-search.spec.ts` so a seeded higher-level candidate matches a lower minimum,
      using `data-testid` or role selectors and asserting the “≥ B2” chip presentation
      (candidate-search matching and label scenarios).
- [x] 5.2 Start the required stack and actually run the targeted Playwright advanced-search spec from
      `frontend/`; inspect the result and restore any reordered or modified seed data afterwards
      (candidate-search and business-catalogs reorder scenarios).
- [x] 5.3 Run affected frontend unit suites and backend unit/integration suites, inspect all output,
      and verify PostgreSQL state is restored and private storage remains unchanged; retain and run
      unchanged legacy Supabase integration checks where applicable (all KTL-25 requirements).

## 6. Documentation and Quality Gates

- [x] 6.1 Update `docs/ktl-10/search.md` with minimum-level matching and catalog-order ranking, and
      inspect `README.md` to update it only if it describes level matching (candidate-search contract).
- [x] 6.2 Add `docs/ktl-25/release-notes.md` announcing that saved presets and position requirements
      with levels can return more candidates without stored-filter changes (saved filters story).
- [x] 6.3 Run `npm run build:all` from `frontend/` and inspect TypeScript, Vite, and .NET build output
      (all KTL-25 requirements).
- [x] 6.4 Run `npm run lint` and `npm run format:check` from `frontend/`, inspect their output, and fix
      every KTL-25-related failure before considering the change done (all KTL-25 requirements).
