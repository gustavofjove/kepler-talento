## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-40` from an up-to-date `main` (brief `openspec/KTL-40.md`)

## 1. Backend: stage counts on the position list (position-management)

- [x] 1.1 Add the five stage counts to `PositionSummary` (`IPositionRepository.cs`) and `PositionStageCountsResponse(New, Shortlisted, Interview, Hired, Rejected)` to `PositionListItemResponse` in `PositionContract.cs`; map them in `ListPositions.cs` with the guard unchanged
- [x] 1.2 Compute the counts in `PositionRepository.ListAsync` with one lateral grouped aggregate per page row over `OPS_PositionCandidates` (design D2), deriving `candidateCount` from the same aggregate
- [x] 1.3 Extend `PositionHandlerTests` (mapping) and `PositionApiTests`: counts 3/0/1/0/2 sum to 6, a removed candidate still counted, a `positions.read`-only actor receives counts with no candidate id or name, unauthenticated → 401 and unauthorized → 403 still
- [x] 1.4 Extend `PositionQueryPlanTests` with the stage-count projection; record the EXPLAIN for lateral vs correlated and keep the cheaper within budget

## 2. Backend: `createdAt` sort (candidate-search)

- [x] 2.1 Add `SearchSortField.CreatedAt` (wire `createdAt`) to the closed parse switch in `SearchContract.cs`
- [x] 2.2 Order by `CreatedAtUtc` then `Id` on the SQL path of `CandidateSearchQuery`; add `CreatedAtUtc` to `EncryptedSearchRow` and a `Comparer` branch for the encrypted stage
- [x] 2.3 Add index `IX_CND_Candidates_IsActive_CreatedAtUtc` (`CreatedAtUtc` desc, `Id`, filtered on `"IsActive"`) in `CandidateConfiguration.cs` and generate migration `CandidateCreatedAtSortIndex` with `dotnet ef migrations add`; confirm no grant change is needed
- [x] 2.4 Tests: `SearchApiTests`/`SearchHandlerTests` (both directions, with and without text filter, non-overlapping pages, unknown and `status` sort still refused), `SearchSchemaTests` (index and filter), `SearchQueryPlanTests` (index used for a `createdAt` sort over active candidates)
- [x] 2.5 Run `npm run test:backend` (Docker running) and inspect the output; confirm `ProjectDependencyTests` still pass and check the migrated schema in PostgreSQL shows the new index

## 3. Frontend: models, list and search page

- [x] 3.1 Add `stageCounts: Record<PositionCandidateStage, number>` to `PositionListItem` (`position.models.ts`) and `'createdAt'` to `CandidateSortField`; `defaultDirection('createdAt')` returns `desc` in `candidate-list.logic.ts`
- [x] 3.2 Make sure `candidateService.listPage(query, signal)` and `positionService.search(query, signal)` exist, accept an `AbortSignal` and leave the shared list signals untouched (add them if missing)
- [x] 3.3 `advanced-search-page.tsx`: apply `?preset=<id>` on mount through the existing apply/use path, select it in the picker, remove the parameter with a replace navigation, reject non-GUID values client-side, guard against double application (design D5)
- [x] 3.4 Update/extend `candidate-list.logic.spec.ts` and `advanced-search-page.spec.tsx` (success, `abc`, deleted id, URL cleaned, use recorded once)

## 4. Frontend: «Inicio» label (primary-navigation)

- [x] 4.1 Change the home entry label to `Inicio` in `core/layout/nav-items.ts` (keep `to`, `end`, `testId: 'nav-dashboard'`) and `dashboard.title` to «Inicio» in `es.json`
- [x] 4.2 Update the label expectations in `tests/unit/nav-items.spec.ts` and `tests/unit/primary-nav.spec.tsx`; grep `frontend/tests` and `docs/` for any other «Dashboard» text assertion or reference and update it

## 5. Frontend: home page panels (operational-dashboard)

- [x] 5.1 Write `features/dashboard/dashboard.logic.ts`: per-panel queries, availability split clamped at 0, inactive figure clamped at 0, preset ordering and top 5, hrefs built with `writeListView`
- [x] 5.2 Write the shared `dashboard-panel.tsx` (labelled region, heading, «Ver todos» slot, loading `role="status"`, empty and error states) and the per-panel data hook with `AbortController` and an `enabled` flag (design D1)
- [x] 5.3 Write `dashboard-actions.tsx` (three permission-gated actions) and `candidate-summary.tsx` (card, decorative bar with KTL-38 tones hidden at total 0, «Sin CV principal» tile, «inactivos» text for `candidates.delete`)
- [x] 5.4 Write `open-positions-panel.tsx` (tile count, 5 rows with stage counts and total, `useRowLink`, `.data-table` in `.table-wrap`, empty state with «Nueva posición» for managers)
- [x] 5.5 Write `recent-candidates-panel.tsx` (used for «Últimos disponibles» and «Últimos añadidos», name link, `AvailabilityCell`, «Sin CV») and `saved-searches-panel.tsx` (top 5, «Usada …»/«Sin usar», `/app/search?preset=<id>`, «Gestionar presets» for `presets.manage`)
- [x] 5.6 Rewrite `dashboard-page.tsx` to compose the panels, reading every permission with `usePermission()` at the top; show «No tienes acceso a ningún resumen.» when neither read permission is held; remove «Con CV principal», «Centro operativo» and `dashboard-error`; add `dashboard.css` using Kepler tokens and the existing `.grid` rules (no new breakpoint)
- [x] 5.7 Update `es.json` `dashboard.*` keys (add the panel, state and plural `_one`/`_other` keys; remove `kpi.withPrimaryCv`, `shortcuts.*`, `error`); add English values to `en.json` where convenient; keep the page off `LEGACY_HARDCODED_COPY`
- [x] 5.8 Write `tests/unit/dashboard.logic.spec.ts` and rewrite `tests/unit/dashboard-page.spec.tsx` with `<ServicesProvider>` doubles: criteria 1–4, 6, 7, 9 (panel), 11–14, and assertions on which requests are and are not sent
- [x] 5.9 Review the other existing unit specs touched by the page (e.g. anything importing dashboard test ids or `dashboard.*` keys) and update them

## 6. Demo data (development-demo-data)

- [x] 6.1 Write `scripts/seed-demo-data.lib.js` with the fixed dataset tables (24 candidates, availability offsets, CV flags, 2 removed, 8 positions, stage matrix, 4 presets), and the loopback guard
- [x] 6.2 Write `scripts/seed-demo-data.js`: argument parsing (`--base-url`, `--remove`, `--no-cvs`), dev-token sign-in, catalog lookup by code, idempotent creation in deterministic order, backdated availability, removals, synthetic PDF upload with bounded scan polling and primary selection, positions and stage moves, presets and ordered uses, counts-only report (design D6)
- [x] 6.3 Implement `--remove` (deactivate demo candidates, close demo positions, delete demo presets) through the API only, and make a later seed reactivate and reopen them
- [x] 6.4 Add `"seed:demo": "node ../scripts/seed-demo-data.js"` to `frontend/package.json`
- [x] 6.5 Write `tests/unit/seed-demo-data.lib.spec.ts` (loopback accepted/refused, no 13-digit number anywhere in the dataset, every stage present in the matrix, at least one empty open position, every e-mail distinct and in the reserved domain, checks within 30 days)
- [x] 6.6 Run `npm run seed:demo` against the Compose stack (development database holding only the 500 synthetic `ktl-testdata` candidates and a few e2e records, no demo data yet) and inspect the report; run it a second time and confirm it creates nothing
- [x] 6.7 Run the e2e suite (or `global-teardown` via `node scripts/e2e-cleanup.js`) after seeding and confirm the demo records survive
- [x] 6.8 Run `npm run seed:demo -- --remove` and confirm demo candidates are inactive, demo positions closed and demo presets deleted; then re-seed so the stack is left populated for review

## 7. Security evidence

- [x] 7.1 Add/extend `frontend/tests/security` checks: the position list response carries no candidate identity in `stageCounts`; the dashboard sends no `/api/candidates/search` or `/api/search-presets` request without `candidates.read` and no `/api/positions` request without `positions.read`
- [x] 7.2 Confirm in the backend tests that `GET /api/positions` and `POST /api/candidates/search` with `sortField=createdAt` fail closed for unauthenticated (401) and unauthorized (403) callers
- [x] 7.3 Confirm the demo script refuses a non-loopback `--base-url` before sending any request, and that seeded candidate names are stored encrypted in `CND_Candidates` and the writes appear in the audit trail
- [x] 7.4 Run `npm run security:rls` and `npm run security:storage` and confirm they are unchanged and passing

## 8. End-to-end

- [x] 8.1 Write `frontend/tests/e2e/dashboard.spec.ts`: seed a marked position with candidates at several stages, a marked candidate checked `available` and a marked preset (all carrying `Date.now()`); verify figures open the right views, stage counts, both recent lists and the preset shortcut; select by role, label and test id only
- [x] 8.2 Run the dashboard spec: `npx playwright test tests/e2e/dashboard.spec.ts` (needs `docker compose up` with the rebuilt API) and inspect the result
- [x] 8.3 Run `tests/e2e/navigation-responsive.spec.ts` and `tests/e2e/advanced-search-presets.spec.ts` and confirm they pass with «Inicio»
- [x] 8.4 Run the full `npm run e2e` and confirm the teardown leaves no marked records; re-seed the demo data afterwards if anything removed it
- [x] 8.5 With the demo data loaded, open `/app` at 1280 px and 390 px and confirm every panel is populated, the page has no horizontal scroll and every link is keyboard-reachable

## 9. Documentation

- [x] 9.1 Update `docs/ktl-18/list-contract.md` and `docs/ktl-10/search.md` (new `createdAt` sort) and `docs/ktl-30/position-candidates.md` § Position list (stage counts)
- [x] 9.2 Write `docs/ktl-40/release-notes.md` (Inicio page, label rename, API additions, migration) and `docs/ktl-40/demo-data.md` (what the dataset contains, how to run, re-run, remove, guards, fabricated-data warning)
- [x] 9.3 Update `README.md` in Spanish: replace the dashboard sentence in the KTL-18 section with a short description of «Inicio», and add `npm run seed:demo` to the development commands
- [x] 9.4 Add `npm run seed:demo` to the Commands block of `AGENTS.md`

## 10. Gates

- [x] 10.1 Run `npm test` (from `frontend/`) and inspect the output
- [x] 10.2 Run `npm run test:backend` and inspect the output
- [x] 10.3 Run `npm run lint` and `npm run format:check`
- [x] 10.4 Run `npm run build:all` (warnings are errors)
- [x] 10.5 Run `openspec validate ktl-40-home-dashboard --strict`
