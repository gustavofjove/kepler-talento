## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-15` before implementation, confirm the branch and preserve unrelated working-tree changes. `[proposal: KTL-15 delivery]`

## 1. Dependencies and shared permission vocabulary

- [x] 1.1 Add the centrally pinned `HtmlSanitizer` NuGet package and the minimal Lexical React/HTML/rich-text/list packages selected in design D4-D5; restore npm, NuGet and lockfiles and verify both dependency trees. `[position-management: Safe rich-text position description]`
- [x] 1.2 Add `positions.read` and `positions.manage` to the API and SPA permission catalogues, role validation and test actor utilities with no aliases or implied permissions. `[identity-and-access-control: Position permission vocabulary and seeded assignments]`
- [x] 1.3 Update and run the existing permission-vocabulary and role unit tests to prove the frontend/backend catalogues stay identical and custom permission combinations remain valid. `[identity-and-access-control: Position permission vocabulary and seeded assignments]`

## 2. Domain model and shared filter contract

- [x] 2.1 Implement the `Position` aggregate, `PositionStatuses` and normalized position text value rules, including open-by-default lifecycle, bounded fields, copied requirements and no delete method. `[position-management: Position identity and lifecycle]`
- [x] 2.2 Add domain unit tests for trimming, case/accent title normalization, field bounds, open/closed transitions, unchanged requirements on close/reopen and invalid statuses. `[position-management: Position identity and lifecycle]`
- [x] 2.3 Generalize `SearchFilterDocument` error selection without changing preset behavior, and add unit tests for position-specific invalid-document failures plus existing/older filter-family compatibility. `[position-management: Position requirements use the candidate-search contract]`
- [x] 2.4 Add the position description sanitizer port and safe application-level length/canonical-output contract. `[position-management: Safe rich-text position description]`

## 3. Application vertical slices

- [x] 3.1 Add position response/list DTOs, page and sort options, stable `position.*` error codes, validators and `PositionGuards` under `Application/Features/Positions/`. `[position-management: Bounded position listing; Position read and write API; Position authorization fails closed]`
- [x] 3.2 Add `IPositionRepository` with focused list/find/add/version/save operations and explicit saved/title-conflict/concurrency-conflict outcomes. `[position-management: Position read and write API]`
- [x] 3.3 Implement `ListPositionsQuery` and `GetPositionQuery`, including bounded/default paging, status/text normalization, closed sort mapping, minimal list projection contract and fail-closed guards. `[position-management: Bounded position listing; Position authorization fails closed]`
- [x] 3.4 Implement `CreatePositionCommand` and `UpdatePositionCommand`, including filter normalization/serialization, server sanitization, v7 ids, UTC timestamps, optimistic versions and stable conflict mapping. `[position-management: Position identity and lifecycle; Safe rich-text position description; Position requirements use the candidate-search contract]`
- [x] 3.5 Add hand-written application doubles and unit tests for every handler's unauthenticated/unauthorized guard before validation/lookup, successful mapping, invalid filters, not-found, title race and stale version. `[position-management: Position authorization fails closed; Position read and write API]`

## 4. Infrastructure, sanitization and PostgreSQL

- [x] 4.1 Implement the Infrastructure HTML sanitizer with the empty-by-default allowlist and canonicalization; test allowed formatting plus scripts, malformed HTML, SVG/MathML, event attributes, CSS, URLs, encoded payloads and paste aliases using the real library. `[position-management: Safe rich-text position description]`
- [x] 4.2 Add `PositionConfiguration`, `DbSet<Position>`, repository implementation and DI registrations; project lists without description/requirements and reload `xmin` after writes. `[position-management: Bounded position listing; Position read and write API]`
- [x] 4.3 Add identifier-only position audit event types and transactional create/update/status recording, plus Spanish audit labels and tests proving no title, location, description or requirements enter events. `[position-management: Safe rich-text position description; Live candidate matching preserves privacy boundaries]`
- [x] 4.4 Generate the KTL-15 EF Core migration for `"OPS_Positions"` and review the generated model/snapshot before adding explicit `pg_trgm`, constraints, GIN/B-tree indexes and a non-destructive down path. `[postgresql-persistence: Position workflow persistence uses least privilege]`
- [x] 4.5 Add idempotent seeded-role JSON updates and `ktl_runtime` `SELECT`/`INSERT`/`UPDATE` grants plus explicit `DELETE` revoke to the same migration; leave custom roles untouched and never grant DDL. `[identity-and-access-control: Position permission vocabulary and seeded assignments; postgresql-persistence: Position workflow persistence uses least privilege]`
- [x] 4.6 Add PostgreSQL integration tests for schema constraints, normalized-title concurrent conflict, status vocabulary, JSON/schema version checks, `xmin`, idempotent role seeds, exact runtime grants and refused runtime deletion. `[postgresql-persistence: Position workflow persistence uses least privilege]`
- [x] 4.7 Add representative position-list query-plan tests for default ordering and normalized title/location contains filters, proving bounded projections avoid reading description and requirements. `[position-management: Bounded position listing; postgresql-persistence: Position workflow persistence uses least privilege]`

## 5. HTTP boundary and API security

- [x] 5.1 Add `PositionEndpoints` with the four contracted routes, authorization before request-value parsing/dispatch, explicit response metadata and `Program.cs` registration; expose no delete or candidate-aggregation route. `[position-management: Position read and write API; Position authorization fails closed]`
- [x] 5.2 Add API integration tests for create/get/list/update, defaults, every status/sort/page/text edge, minimal list JSON, sanitized detail JSON, title conflict, missing id, concurrency and the absence of a delete endpoint. `[position-management: Position identity and lifecycle; Bounded position listing; Safe rich-text position description]`
- [x] 5.3 Add endpoint authorization matrices covering unauthenticated and wrong-permission callers with valid/invalid requests and existing/missing ids, and prove refusals occur before validation or existence disclosure. `[position-management: Position authorization fails closed]`
- [x] 5.4 Extend personal-data redaction sentinels for position title, location, description and requirements and verify API/application logs and problems reveal no HTML, filters, candidate values or database details. `[position-management: Live candidate matching preserves privacy boundaries]`

## 6. Frontend service and rich-text components

- [x] 6.1 Add position models and `PositionService` over `ApiTransport`, including list query encoding, CRUD requests, stable error mapping, a list-state signal and no local/Supabase persistence. `[position-management: Bounded position listing; Position read and write API]`
- [x] 6.2 Add `usePositions()` and wire the service through `core/di/services.ts`/`ServicesProvider`; add unit tests that render-time reads subscribe to the signal and do not use a derived snapshot. `[position-management: Position user experience is localized and accessible]`
- [x] 6.3 Build the bounded Lexical position editor and read-only description renderer with paragraph/emphasis/list controls, controlled form integration, labels, keyboard behavior and safe API-canonical HTML rendering. `[position-management: Safe rich-text position description; Position user experience is localized and accessible]`
- [x] 6.4 Add editor/viewer unit tests for formatting, paste normalization, empty/initial values, change propagation, focus/keyboard behavior and unsupported-control absence. `[position-management: Safe rich-text position description; Position user experience is localized and accessible]`

## 7. Position pages, presets, matching and navigation

- [x] 7.1 Build the server-paged position list and pure list logic with debounced text, open/closed/all status, closed sort options, page reset, localized loading/empty/error states and permission-gated actions. `[position-management: Bounded position listing; Position user experience is localized and accessible]`
- [x] 7.2 Build the controlled create/edit form around the rich-text editor and shared `SearchCriteriaForm`, including cancel, stale/title conflict handling and open/closed transitions. `[position-management: Position identity and lifecycle; Position requirements use the candidate-search contract]`
- [x] 7.3 Add preset selection/apply and save-as-preset controls using `SearchPresetsService`, defensive filter cloning and the independent `candidates.read`/`presets.manage` rules; preserve the draft on failures. `[saved-search-presets: Position editor consumes the shared preset library; position-management: Presets are copied rather than linked]`
- [x] 7.4 Build the detail page with canonical description, `SearchCriteriaSummary` and live paged `SearchResults`; abort stale requests and issue no search or match count when `candidates.read` is absent. `[position-management: Live candidate matching preserves privacy boundaries]`
- [x] 7.5 Add the four SPA routes, route guards, `nav.positions` data entry, hoisted navigation permission and responsive styles; add all Spanish `positions.*` copy and stable accessible/test identifiers. `[primary-navigation: Positions is a permission-filtered primary destination; position-management: Position user experience is localized and accessible]`
- [x] 7.6 Add frontend unit/integration tests for service contracts, list/filter/paging states, create/edit/close/reopen, preset-copy independence, permission combinations, live-match cancellation/no-search behavior, routes and responsive/keyboard semantics. `[position-management; saved-search-presets; primary-navigation]`
- [x] 7.7 Review and update all affected existing unit tests—search criteria/results, presets, auth/roles, route guards, navigation, i18n and DI doubles—so the new section does not regress their established behavior. `[proposal: composed existing capabilities]`

## 8. Security and privacy evidence

- [x] 8.1 Add `tests/security/ktl-15-positions-boundary.spec.ts` for route/handler guard presence, shared permission parity, no `DELETE`, no browser database/local-storage path and no candidate request without `candidates.read`. `[position-management: Position authorization fails closed; Live candidate matching preserves privacy boundaries]`
- [x] 8.2 Exercise API calls as read-only, manage-only, candidate-less, preset-less and document-less actors and verify each independent permission boundary fails closed without data/count/existence leakage. `[identity-and-access-control: Position permission vocabulary and seeded assignments]`
- [x] 8.3 Inspect the migrated PostgreSQL state as `ktl_runtime`: verify allowed DML, refused `DELETE`/DDL, exact indexes/constraints and unchanged append-only audit grants; retain the evidence in the integration suite. `[postgresql-persistence: Position workflow persistence uses least privilege]`
- [x] 8.4 Verify position responses/logs expose no candidate fields beyond the existing search projection, no unsafe/raw HTML and no storage key/path; run the private-storage checks even though KTL-15 adds no document storage. `[position-management: Safe rich-text position description; Live candidate matching preserves privacy boundaries]`

## 9. Documentation and release readiness

- [x] 9.1 Add `docs/ktl-15/positions.md` covering routes, payloads/errors, status lifecycle, rich-text allowlist, copied requirements, permission matrix, live-match semantics and operator troubleshooting. `[position-management]`
- [x] 9.2 Add `docs/ktl-15/release-notes.md` with Spanish user-visible behavior, preset independence, role changes, migration prerequisite and no-delete/no-snapshot limitations. `[proposal: user-visible change]`
- [x] 9.3 Update `docs/ktl-5/database-conventions.md` for position workflow ownership under `OPS_`, `pg_trgm`, table/index names and runtime grants; update the Spanish `README.md` only if its enumerated feature/permission overview requires it. `[postgresql-persistence: Position workflow persistence uses least privilege]`
- [x] 9.4 Document migration preflight, backup, explicit `--migrate` rollout, verification and data-safe rollback for `OPS_Positions`, including the rule not to drop `pg_trgm` and not to drop a populated table without approval. `[postgresql-persistence: Position workflow persistence uses least privilege]`

## 10. Execute verification gates

- [x] 10.1 Restore dependencies, run the targeted backend unit tests for positions/filter codec/sanitizer/redaction, inspect failures and record the successful output. `[position-management: backend unit evidence]`
- [x] 10.2 Run the KTL-15 backend integration/API/schema/grant/query-plan tests against Testcontainers PostgreSQL, inspect the resulting position/audit rows and privileges, and restore fixture state afterwards. `[position-management; postgresql-persistence; identity-and-access-control]`
- [x] 10.3 Run the targeted frontend Vitest suites plus `npm run test:integration`; inspect output and retain the legacy Supabase integration checks for unchanged legacy paths. `[position-management: frontend and compatibility evidence]`
- [x] 10.4 Start the required Compose stack, run `npx playwright test tests/e2e/positions.spec.ts tests/e2e/navigation-responsive.spec.ts tests/e2e/ux-accessibility.spec.ts tests/e2e/secure-access.spec.ts`, inspect traces/results and restore seeded position/candidate data afterwards. `[position-management: end-to-end and accessibility evidence; primary-navigation]`
- [x] 10.5 Run `npm run test:security`, `npm run security:rls` and `npm run security:storage`; inspect fail-closed authorization, grant and private-storage results. `[position-management: security evidence; postgresql-persistence]`
- [x] 10.6 Run the complete `npm test` and `npm run test:backend` suites and inspect their outputs for regressions across candidate search, presets, identity, audit and navigation. `[proposal: existing capability regression evidence]`
- [x] 10.7 Run `npm run lint` and `npm run format:check`, fix every issue and rerun both successfully. `[proposal: repository quality gates]`
- [x] 10.8 Run `npm run build:all`, inspect the warnings-as-errors frontend/backend build output, and verify the final git diff contains no credentials, candidate exports, CVs, storage paths or unrelated changes. `[proposal: releasable implementation]`
