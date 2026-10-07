## Context

See `proposal.md` for motivation and `openspec/KTL-40.md` (§ Panels, § Decisions) for the agreed
panel-by-panel contract, which this design adopts unchanged except for the two requested additions:
the «Inicio» label and the demo dataset.

Current state that shapes the approach:

- `frontend/src/app/features/dashboard/dashboard-page.tsx` loads four counts with one
  `Promise.all`; one rejection sets a page-wide error. Its heading comes from `dashboard.title`
  («Dashboard»). The navigation label is a literal in `core/layout/nav-items.ts`
  (`label: 'Dashboard'`, `testId: 'nav-dashboard'`), asserted by `tests/unit/nav-items.spec.ts`.
- `GET /api/positions` already projects `candidateCount` per item in
  `PositionRepository.ListAsync`; links live in `OPS_PositionCandidates` with a unique index on
  (`PositionId`, `CandidateId`) and at most 500 links per position.
- Candidate search has a closed sort switch in `SearchContract` and two execution paths in
  `CandidateSearchQuery`: SQL ordering for filter-only searches and an in-memory `Comparer` over
  decrypted `EncryptedSearchRow`s for text searches (KTL-33).
- `PUT /api/candidates/{id}/availability` accepts a `CheckedOn` date up to tomorrow (UTC), so past
  checks can be recorded. `POST /api/dev/token` issues development tokens and is mapped only outside
  Production.

## Goals / Non-Goals

**Goals:**

- Panels that are independent in loading, failure, emptiness and permission, built only from
  existing endpoints plus the two small API additions.
- Rename the visible label to «Inicio» without breaking any load-bearing selector.
- A one-command, repeatable way to populate a local stack so every panel shows realistic content.

**Non-Goals:**

- No `/api/dashboard` aggregate endpoint, no caching layer, no polling or live refresh.
- No change to the `/app` route, the `nav-dashboard` test id or the `dashboard.*` i18n namespace.
- Moving the other navigation labels from `nav-items.ts` into `es.json` (a separate cleanup).
- Demo data in staging or production, demo CV contents beyond a minimal synthetic PDF, and a
  generic fixture framework.

## Decisions

### D1. Compose the page from existing endpoints, one hook per panel

Each panel owns a small data hook (`usePanelData(load, enabled)`) returning
`{ status: 'idle' | 'loading' | 'ready' | 'error', data }`, created with an `AbortController`
aborted on unmount. `enabled` is the permission boolean read once at the top of `DashboardPage`
with `usePermission()`; when false the hook never calls `load`, which is what makes "no request
without permission" testable by asserting on the doubles.

The candidate card derives its active total from the «Últimos añadidos» response's `totalCount` and
its available figure from «Últimos disponibles» `totalCount`, so the 7-request budget holds:
unavailable count, without-CV count and (with `candidates.delete`) the inactive count are
`pageSize: 1` searches. The card renders as soon as its own inputs resolve; if any input fails, the
card shows the panel error while the recent panels still render.

Data access goes through `candidateService.listPage(query, signal)` and
`positionService.search(query, signal)`, which return pages without touching the list pages'
shared signals. `positionService.list` is avoided on purpose because it overwrites the position
list's signal. Presets use `searchPresetsService.load()` and `useSearchPresets()`.

_Alternative considered:_ a `/api/dashboard` endpoint. Rejected (brief § Decisions): a new
mixed-permission contract that re-implements every panel's permission rule.

### D2. Stage counts as one lateral aggregate per page row

`PositionSummary` gains a `PositionStageCounts` record. `ListAsync` pages the positions first
(`Skip`/`Take`), then left-joins, per page row, one grouped aggregate over that position's links in
`OPS_PositionCandidates` that yields the total and the five stage counts together. PostgreSQL plans
it as `Limit` → `Nested Loop Left Join` → `GroupAggregate` over the unique
(`PositionId`, `CandidateId`) index, so the aggregate runs only for the page's rows (≤ 100 rows,
≤ 500 links each) and the query stays one statement besides the count, as
`PositionQueryPlanTests` pins.

Alternatives measured with `PositionQueryPlanTests` (5,000 positions, 100 of them with 60 links):

- Six correlated `COUNT` subqueries in the projection (the existing `candidateCount` pattern,
  extended): PostgreSQL evaluates projection subplans for every row an `OFFSET` skips, so the deep
  page (offset 4,900) went from 73 ms to 229 ms against the 250 ms budget. Rejected.
- A `GROUP BY` subquery joined to the page without the lateral correlation: aggregates every link in
  the table before the limit applies. Rejected.

The lateral form measured 0.9 ms on the default page and 4.5 ms on the deep page; the evidence is
regenerated into `docs/ktl-15/query-plans.md`. There is no candidate join, so removed candidates
remain counted and the five values always sum to `candidateCount`.

Wire shape: `"stageCounts": { "new": 3, "shortlisted": 2, "interview": 1, "hired": 0, "rejected": 4 }`,
using the `PositionCandidateStages` vocabulary. The guard stays `PositionGuards.RequireRead`.

### D3. `createdAt` sort with a matching filtered index

`SearchSortField.CreatedAt` (wire `createdAt`) is added to the closed parse switch. SQL path orders
by `CreatedAtUtc` then `Id`; the encrypted stage gains `CreatedAtUtc` on `EncryptedSearchRow` and a
`Comparer` branch, so text and non-text searches order identically. Migration
`CandidateCreatedAtSortIndex` adds `IX_CND_Candidates_IsActive_CreatedAtUtc` on
(`CreatedAtUtc` desc, `Id`) filtered on `"IsActive"`, mirroring the KTL-36 availability index. It is
generated with `dotnet ef migrations add` and runs through the `--migrate` entry point; `ktl_runtime`
already has `SELECT` on `CND_Candidates`, so no grant change. On the frontend,
`CandidateSortField` gains `'createdAt'` and `defaultDirection('createdAt')` is `desc`; no column is
added to the list.

### D4. «Inicio» is a label change only

- `nav-items.ts`: `label: 'Inicio'`; `testId: 'nav-dashboard'`, `to: '/app'` and `end: true`
  stay. `nav-items.ts` holds Spanish literals by design (it is a `.ts` table, not JSX), so the label
  is changed in place rather than moved to `es.json`.
- `es.json`: `dashboard.title` becomes «Inicio». Key names stay under `dashboard.*`; renaming keys
  would churn every test that resolves them for no user-visible gain.
- Specs and docs that name the page («Dashboard») are updated in the same change, including the
  `primary-navigation` delta and the README section.
- Unit specs that assert the label (`nav-items.spec.ts`, `primary-nav.spec.tsx`) change their
  expectation; e2e specs select by `nav-dashboard`, so they need no change.

_Alternative considered:_ also renaming the route to `/app/inicio` and the feature folder. Rejected:
URL and folder churn with bookmarks and specs at stake, and no user value.

### D5. `?preset=<id>` on the advanced search page

On mount, `advanced-search-page.tsx` reads `preset` with `useSearchParams`. If present it runs the
existing apply path (the `use` call that records the use and returns the preset), selects it in the
picker, and calls `setSearchParams` with `{ replace: true }` minus `preset`. A non-GUID value is
rejected client-side before any request and treated like a missing preset: the existing
apply-failed toast, empty filters and the default search. A ref guards against double application
under React StrictMode. The id is not personal data; the filters never enter the URL.

### D6. Demo data: a Node script against the API, not SQL and not the Access generator

`scripts/seed-demo-data.js` (Node 22 built-ins only: global `fetch`, `FormData`/`Blob`,
`node:util` `parseArgs`) is exposed as `npm run seed:demo` in `frontend/package.json`, following the
`security:rls` pattern (`node ../scripts/...`). Options: `--base-url` (default
`http://localhost:4200`, env `KTL_API_BASE`), `--remove`, `--no-cvs`.

Why the API:

- Candidate names and e-mails are encrypted with the field keys (KTL-33); writing SQL would have to
  re-implement encryption and blind indexes, and bypass validation and audit.
- `Tools/TestDataGenerator` produces Access-shaped CSVs for `ktl-migrate`; it covers candidates and
  their profile rows but not positions, links, availability checks, presets or documents. Extending
  it would push dashboard concepts into the legacy migration path. It stays unchanged; its own
  volume set remains the tool for load testing.
- The API path exercises the real permission checks and audit, so the seeded data is
  indistinguishable from data entered by hand.

Flow:

1. **Guard.** Parse `--base-url`; refuse unless the hostname is `localhost`, `127.0.0.1` or `[::1]`.
   `POST /api/dev/token` with the seeded development administrator identity (the same one the e2e
   suite uses); any non-2xx exits non-zero with nothing created.
2. **No catalog lookups.** Candidates use only free-text fields, and the presets use availability,
   CV, check-date and text filters, so the dataset does not depend on which catalog values a
   database holds.
3. **Candidates.** A fixed table of 24 fabricated people (Spanish names, `nombre.apellido@demo.kepler-talento.local`,
   9-digit phones, no 13-digit numbers). Existing demo candidates are found with
   `GET /api/candidates?includeInactive=true` filtered client-side by e-mail domain. Missing ones are
   created in table order, so «Últimos añadidos» has a stable order. Then for each, per the table:
   availability via `PUT .../availability` with a `CheckedOn` offset from today (0 to −28 days;
   «hasta» dates for some `unavailable`). Two of them are removed logically at the very end, after
   their position links exist (links require an active candidate).
4. **CVs** (unless `--no-cvs`). For roughly half, upload a synthetic one-page PDF (generated in
   memory like `tests/e2e/support/synthetic-cv.ts`; the generator is duplicated in the script
   rather than imported, since `scripts/` is plain JS and the e2e helper is TS) with
   `isPrimary=true`, which makes it the primary CV in the same request. Whether a primary CV exists
   does not depend on the scan, so «Sin CV» is right even while ClamAV is down; the script reports
   how many uploads are still pending scan, and those binaries stay quarantined as usual.
5. **Positions.** 8 fixed titles (e.g. «Desarrollador .NET sénior», «Técnico de soporte N2»,
   «Analista de datos», «Jefe de proyecto», «Administrativo de RR. HH.», «Diseñador UX/UI»,
   «Comercial B2B» open; «Becario de marketing» closed) with location and a short description.
   Found by exact title; missing ones created; the closed one updated to `closed`. Links are added
   and moved through `PUT .../stage` according to a fixed matrix that puts candidates in all five
   stages, and leaves one open position empty. Positions are created in reverse display order and
   one is touched last, so «más recientes» is deterministic.
6. **Presets.** 4 fixed names («Disponibles con CV», «Pendientes de comprobar», «Candidatos en
   Madrid», «No disponibles con CV»), created if missing; on the run that creates them, 3 are
   `POST .../use`d in a fixed order with a short pause between calls so their `lastUsedAt` differ,
   and 1 is never used. A re-run does not use them again, so it never moves their last use.
7. **Report.** Counts created/existing per kind and CV scan outcome. Never prints names, e-mails or
   ids beyond counts.

`--remove`: deactivates every candidate in the demo domain (`PUT .../active` false), closes every
demo-titled position, deletes every demo-named preset. Rows remain (logical removal), consistent
with the domain rule; a developer who wants a clean slate uses their normal database reset.

Pure parts (the dataset tables, date offsets, the loopback check and the "what is missing" diff)
live in `scripts/seed-demo-data.lib.js` and are unit-tested from `frontend/tests/unit/` via
`tests/repo-root.ts`, as other specs that read root files do.

_Alternatives considered:_ a backend `--seed-demo` entry point (rejected: puts fabricated personal
data creation inside a production binary, even if gated); an EF `HasData` seed (rejected: ends up in
every environment's migrations); reusing Playwright fixtures (rejected: those records carry the
teardown marker and vanish after each run).

### D7. Authorization and personal-data model

No new permission, endpoint, grant, table or storage path. Every dashboard request hits an endpoint
that checks `ICurrentActor` and its permission before validation; the UI's permission gating only
avoids pointless 403s. Stage counts are aggregates over links and carry no identity. Candidate rows
render `fullName` from the existing search projection only. Nothing new is logged, client or
server. The demo script writes fabricated data through the same checks and appears in the audit
trail as the development administrator.

### D8. Test strategy

- **xUnit:** `PositionApiTests` (stage counts, sum equals `candidateCount` with a removed candidate,
  reader without `candidates.read`, 401/403 unchanged), `PositionHandlerTests` (mapping),
  `PositionQueryPlanTests`, `SearchApiTests`/`SearchHandlerTests` (`createdAt` both directions, text
  and non-text paths, non-overlapping pages, unknown sort refused), `SearchSchemaTests` (index and
  filter), `SearchQueryPlanTests` (index used).
- **Vitest:** `dashboard.logic.spec.ts`, `dashboard-page.spec.tsx` (criteria 1–4, 6, 7, 9 panel,
  11–14, request assertions on doubles), `advanced-search-page.spec.tsx` (`preset` success, bad id,
  deleted), `candidate-list.logic.spec.ts`, `nav-items.spec.ts` and `primary-nav.spec.tsx`
  («Inicio»), `seed-demo-data.lib.spec.ts` (loopback guard, no 13-digit marker anywhere in the
  dataset, stage matrix covers all five stages, missing-diff).
- **Security (`tests/security`):** stage counts carry no candidate identity; the dashboard sends no
  request for a panel without permission.
- **Playwright:** `dashboard.spec.ts` with marked records (position with links at several stages,
  available candidate, preset) verifying links, counts, recent lists and the preset shortcut;
  navigation specs keep selecting `nav-dashboard`. A manual run of `npm run seed:demo` twice and
  `--remove` against the Compose stack, with screenshots of the populated page at 1280 and 390 px.

## Risks / Trade-offs

- [Seven parallel requests on every visit to the home page] → All are bounded (`pageSize` ≤ 5, no
  text filter, SQL path, indexed sorts); search p95 ≤ 300 ms holds. Revisit an aggregate endpoint
  only with measured evidence.
- [Card figures come from different requests and can be momentarily inconsistent] → «Sin comprobar»
  and «inactivos» are clamped at zero; accepted for a summary view.
- [«Últimos disponibles» means most recently _confirmed_] → Accepted in the brief; only the latest
  check is stored.
- [Demo data is fabricated personal data in a shared dev database] → Reserved domain, loopback-only
  guard, dev-token-only auth, `--remove`, documented in `docs/ktl-40/demo-data.md`; never in
  Compose start-up or migrations.
- [Demo CVs need ClamAV to become viewable] → The upload marks them primary immediately; the script
  reports how many are pending scan, and the dashboard is correct either way.
- [Demo `createdAt`/check dates cluster near "now"] → Check dates are backdated through the API;
  creation times are not backdatable and only their order matters for the panel.
- [Label change breaks text-based assertions] → Only two unit specs assert the literal; e2e uses
  `nav-dashboard`.

## Migration Plan

1. Merge with the `CandidateCreatedAtSortIndex` migration; deploy runs the `migrator` container
   (`--migrate`) before the API, as for every slice. The index is additive.
2. API and SPA ship together; an older SPA ignores `stageCounts`, and a newer SPA against an older
   API would show zeros, so they are released as one image set.
3. Rollback: revert the images; the index can stay (harmless) or be dropped by reverting the
   migration with `dotnet ef database update <previous>` through the migrator.
4. Demo data is never part of deployment; developers run `npm run seed:demo` locally.
