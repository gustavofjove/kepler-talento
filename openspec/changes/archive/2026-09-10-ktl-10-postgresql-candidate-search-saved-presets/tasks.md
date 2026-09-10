## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-10` before implementation; confirm the working tree and preserve unrelated user changes. (Proposal: traceable KTL-10 delivery)

## 1. Prerequisites and Shared Contracts

- [x] 1.1 Verify the completed KTL-6 through KTL-9 schema/API mappings for candidates, catalog relations and primary-document states; record any incompatible assumption before coding. (Candidate search: full filter contract, primary-CV state)
- [x] 1.2 Define backend request, paged response and preset contracts matching `SearchFilters`, `SearchResult` and `SearchPreset`, including page defaults/bounds and JSON filter schema version. (Candidate search: bounded pagination and minimal projection; Presets: listing/create/update)
- [x] 1.3 Add shared normalization/validation cases for blank values, duplicate criteria, statuses, modes, CV selection, literal wildcard characters and preset names. (Candidate search: full contract and multi-value semantics; Presets: create/update)
- [x] 1.4 Build a shared parity fixture with all five statuses, deleted candidates, duplicate-capable relation rows, level-less and leveled criteria, and pending/available/refused primary documents. (Candidate search: filter semantics, CV state, no duplicates)
- [x] 1.5 Preserve a test-only reference evaluator for the pre-KTL-10 in-memory semantics and generate expected candidate IDs from the shared fixture. (Proposal acceptance: identical results)

## 2. Preset Persistence and Database Migration

- [x] 2.1 Add the saved-preset domain/application model with opaque ID, actor-derived owner, normalized name, versioned filters and server timestamps. (Presets: owner-scoped listing, create/update, last use)
- [x] 2.2 Map `ADM_SearchPreset` explicitly in EF Core with bounded columns, JSONB filters, not-blank/JSON checks and UTC timestamps. (Presets: create/update and privacy)
- [x] 2.3 Add the per-owner case-insensitive unique constraint and owner/name listing index, and map concurrent violations to the stable conflict contract. (Presets: name uniqueness)
- [x] 2.4 Create the repeatable EF migration containing the preset table, constraints, indexes and explicit least-privilege runtime grants without granting DDL or schema-wide access. (Presets: runtime database role; Proposal security impact)
- [x] 2.5 Add only non-duplicated candidate/relation/document search indexes justified by the final KTL-7/KTL-9 mappings; keep `pg_trgm` and `tsvector` out of this slice. (Candidate search: bounded server query; Design decision 3)
- [x] 2.6 Add migration/integration tests that deploy twice and inspect table shape, constraints, indexes and runtime/migration role privileges. (Presets: privacy; Proposal success/security)

## 3. Candidate Search Backend Slice

- [x] 3.1 Implement search request normalization and validation with page 1/size 25 defaults, the 1..100 bound and stable Spanish validation problems. (Candidate search: full contract and bounded pagination)
- [x] 3.2 Implement the active-candidate base query, status predicate, escaped literal `ILIKE` across parity fields and deterministic `UpdatedAtUtc DESC, Id ASC` ordering. (Candidate search: full contract, deleted exclusion and pagination)
- [x] 3.3 Implement skill, language and program `ANY` predicates as correlated `EXISTS` expressions with value/optional-level matching. (Candidate search: multi-value semantics)
- [x] 3.4 Implement each family’s `ALL` predicate as one correlated existence condition per distinct criterion, with no outer relation join or compensating `DISTINCT`. (Candidate search: correct multi-row `ALL` and no duplicates)
- [x] 3.5 Implement `hasCv` from non-removed primary metadata in every KTL-9 scan state and project only the documented `SearchResult` fields. (Candidate search: primary-CV state and minimal projection)
- [x] 3.6 Implement total count and page materialization through the candidate repository abstraction without loading full aggregates. (Candidate search: bounded deterministic pagination)
- [x] 3.7 Add `POST /api/candidates/search`, checking `candidates.read` before validation/repository access and keeping `view_all_candidates` intentionally inert. (Candidate search: authorization and visibility)
- [x] 3.8 Configure request/error logging so search bodies, terms and filter values are never logged while correlation IDs and safe outcome metadata remain available. (Candidate search: authorization/privacy)
- [x] 3.9 Add backend unit tests for normalization, literal wildcard escaping, every family, `ANY`/`ALL`, blank levels, duplicate criteria, validation and page bounds. (Candidate search: full contract, semantics and pagination)
- [x] 3.10 Add PostgreSQL integration tests comparing every filter family and combined filters with the shared reference fixture, including no duplicates and exact totals. (Candidate search: parity and no duplicates)
- [x] 3.11 Add integration cases for pending/available/refused primary CVs, absent primaries and logically deleted candidates. (Candidate search: primary-CV state and logical deletion)
- [x] 3.12 Capture and inspect `EXPLAIN (ANALYZE, BUFFERS)` for unfiltered, text, `ANY`, `ALL` and combined searches against the reconciled KTL-7-scale dataset; retain the evidence. (Candidate search: bounded query and index decision)

## 4. Saved Preset Backend Slices

- [x] 4.1 Implement owner-scoped list and create handlers with actor-derived ownership, normalized ordering and complete filter validation. (Presets: listing and create/update)
- [x] 4.2 Implement owner-scoped rename/filter update with immutable owner/creation fields and atomic timestamp advancement. (Presets: create/update)
- [x] 4.3 Implement owner-scoped delete and apply/use handlers, with apply atomically updating `LastUsedAtUtc` and `UpdatedAtUtc`. (Presets: deletion and last-used tracking)
- [x] 4.4 Expose `GET/POST /api/search-presets`, `PUT/DELETE /api/search-presets/{id}` and `POST /api/search-presets/{id}/use`, all checking `candidates.read` before data access. (Presets: lifecycle and fail-closed access)
- [x] 4.5 Return the same stable not-found response for missing and cross-owner preset IDs, with no owner/filter leakage in problems or logs. (Presets: owner isolation and safe diagnostics)
- [x] 4.6 Add backend unit tests for CRUD, timestamps, case-insensitive conflicts, malformed filters, owner derivation and safe errors. (Presets: all lifecycle requirements)
- [x] 4.7 Add PostgreSQL integration tests for CRUD persistence, unique-constraint concurrency, actor isolation and last-use tracking. (Presets: all lifecycle requirements)

## 5. Frontend Search Cutover

- [x] 5.1 Extend search models with the paged request/response while preserving the exact minimal `SearchResult` projection and default filter cloning. (Candidate search: pagination and minimal projection)
- [x] 5.2 Rewrite `CandidateSearchService` over `ApiTransport`, pass the caller’s `AbortSignal`, and remove its `CandidateService` dependency and aggregate hydration. (Candidate search: superseded cancellation; Proposal frontend impact)
- [x] 5.3 Update service wiring and test doubles in `src/app/core/di/services.ts` and `ServicesProvider` fixtures for the API-backed search boundary. (Candidate search: service boundary)
- [x] 5.4 Refactor the advanced-search page to expose loading/error/empty states, page navigation and totals without treating partial data as a complete collection. (Candidate search: bounded pagination)
- [x] 5.5 Add the approximately 300 ms debounce and abort the prior controller before every superseding search; suppress cancelled results and toasts. (Candidate search: superseded cancellation)
- [x] 5.6 Update the results component for page navigation while preserving every existing `name`, `data-testid`, permission hook and minimal-field rendering contract. (Candidate search: pagination and minimal projection)
- [x] 5.7 Rewrite existing `candidate-search.service` unit tests around API serialization, paging and propagated cancellation, retaining parity expectations in backend integration coverage. (Candidate search: full contract and cancellation)
- [x] 5.8 Add React unit tests proving rapid filter changes abort the network request and cannot apply stale results, counts or errors. (Candidate search: superseded cancellation)

## 6. Frontend Preset Cutover

- [x] 6.1 Rewrite `SearchPresetsService` as an asynchronous API client with signal-backed loading/loaded/failed state for list, create, update, delete and apply/use. (Presets: full lifecycle)
- [x] 6.2 Add `useSearchPresets()` to subscribe during render and wire it through the existing singleton DI without direct render-time reads via `useServices()`. (Presets: owner-scoped listing; Frontend conventions)
- [x] 6.3 Refactor the advanced-search preset controls for async operations, Spanish error toasts, rename/update support and owner-scoped refresh after mutations. (Presets: create/update/delete/last use)
- [x] 6.4 Remove all source references and read/write behavior for `rrhh.search.presets.v1`, including legacy `languageValues`/`programValues` preset migration code. (Presets: legacy local presets unsupported)
- [x] 6.5 Preserve `rrhh.search.last-filters.v1` as defensive local-only normalization and verify it never causes a preset API write. (Presets: last filters remain local)
- [x] 6.6 Rewrite existing `search-presets.service` tests for asynchronous API state, full CRUD/use, safe failures and retained local last filters. (Presets: all lifecycle and local-filter requirements)
- [x] 6.7 Add a source regression test proving the former preset storage key is absent while the last-filter key remains functional. (Presets: legacy key removal and local filters)

## 7. Authorization, Privacy, and Boundary Evidence

- [x] 7.1 Add API tests proving search and every preset route fail closed without a current actor and with an actor lacking `candidates.read`, before repository/data access. (Candidate search: authorization; Presets: fail-closed listing)
- [x] 7.2 Add authorization tests proving actors differing only by `view_all_candidates` receive the same matches until a real visibility domain exists. (Candidate search: visibility decision)
- [x] 7.3 Capture application/request/error logs for successful and failing searches/preset operations and assert absence of terms, filter payloads, preset names and candidate personal fields. (Candidate search and presets: safe diagnostics)
- [x] 7.4 Assert search/preset responses expose no relation collections, consent/retention values, owner IDs, document filenames, storage keys, paths, scan internals or cross-owner existence signal. (Candidate search: minimal projection; Presets: privacy)
- [x] 7.5 Run and inspect least-privilege tests for runtime grants, direct-browser database isolation and migration-role separation; prove denied writes leave PostgreSQL unchanged. (Proposal principles 1 and 3)
- [x] 7.6 Run unchanged private-storage/document authorization checks and verify KTL-10 neither reads CV bytes nor makes pending/refused documents downloadable. (Candidate search: primary-CV state; Proposal storage impact)

## 8. Documentation and Final Verification

- [x] 8.1 Review and update all existing frontend and backend unit tests affected by async search/presets, paging, KTL-8/KTL-9 contracts and DI changes. (Mandatory regression review; Proposal success criteria)
- [x] 8.2 Update API/technical documentation with search/preset contracts, bounds, ordering, `view_all_candidates` decision, index evidence, grants and rollback procedure. (Candidate search and presets: documented contracts)
- [x] 8.3 Update Spanish user/release documentation stating that old local presets are unsupported and must be recreated while last filters remain browser-local. (Presets: legacy decision and local filters)
- [x] 8.4 Run `dotnet test backend/Tests/UnitTests/UnitTests.csproj`; inspect and fix every affected backend unit/architecture failure. (Proposal acceptance: backend unit and architecture)
- [x] 8.5 Run `dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`; inspect PostgreSQL rows, totals, indexes, constraints and grants after the run. (Proposal acceptance: integration/database evidence)
- [x] 8.6 Run `npm test -- --run tests/unit/candidate-search.service.spec.ts tests/unit/search-presets.service.spec.ts` and the affected React page/component unit tests; inspect and fix every failure. (Proposal acceptance: frontend tests)
- [x] 8.7 Run `npm run test:integration` and `npm run test:security`; inspect API authorization, redaction, cancellation and resulting PostgreSQL state. (Proposal acceptance: integration/security)
- [x] 8.8 Run `npm run security:rls` and `npm run security:storage` for unchanged legacy Supabase paths; inspect the private-storage result and do not weaken legacy checks. (Mandatory legacy/security evidence)
- [x] 8.9 Run `npm run lint` and inspect/fix all findings. (Mandatory quality gate)
- [x] 8.10 Run `npm run format:check` and inspect/fix all findings without formatting unrelated user changes. (Mandatory quality gate)
- [x] 8.11 Run `npm run build:all` and inspect both TypeScript/Vite and ASP.NET Core build output. (Proposal acceptance: architecture/contract compilation)
- [x] 8.12 Run `npm run e2e -- tests/e2e/advanced-search.spec.ts tests/e2e/advanced-search-presets.spec.ts` against same-origin `/api`; inspect the actual Playwright results for filters, modes, pages, presets and cancellation. (Candidate search and presets: end-to-end acceptance)
- [x] 8.13 Restore the E2E/integration seed and local browser state after testing, then verify candidate, relation, document and preset PostgreSQL state matches the documented fixture baseline. (Mandatory state restoration)
- [x] 8.14 Run `openspec validate ktl-10-postgresql-candidate-search-saved-presets --strict` and reconcile implementation, docs and every completed checkbox before requesting archive. (Proposal traceability and completion)
