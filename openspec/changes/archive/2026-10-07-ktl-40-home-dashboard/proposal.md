## Why

The home page still reflects the first version of the application. Its figures lead nowhere,
«Con CV principal» is redundant, «Centro operativo» duplicates the navigation, «Nuevo candidato»
ignores `candidates.create`, and one failed count blanks every tile. Positions (KTL-15/KTL-30) and
availability (KTL-36), the two things HR works with daily, are absent. HR users rebuild the same
filtered views from the menu every morning.

The page is also called «Dashboard», an English word in an otherwise Spanish UI. It becomes
**Inicio**.

A development database rarely holds enough positions, links, availability checks and presets to
show the new page meaningfully. A reproducible demo dataset lets the team (and reviewers) see every
panel populated without hand-entering records.

## What Changes

- **Home page rewrite** (`/app`, brief KTL-40 § Panels): header quick actions gated by
  `candidates.create`, `positions.manage` and `candidates.import`; a «Candidatos» card with the
  availability split («disponibles · no disponibles · sin comprobar») and an optional «inactivos»
  figure for `candidates.delete`; «Posiciones abiertas» and «Sin CV principal» tiles; panels for
  open positions with per-stage counts, «Últimos disponibles», «Últimos añadidos» and «Búsquedas
  guardadas». Every figure and row links to the filtered list, record or preset behind it.
- Each panel loads, fails and shows its empty state independently, and a panel the actor may not see
  sends no request. Composed from existing endpoints; no `/api/dashboard`.
- Removed: «Con CV principal», «Centro operativo», the page-wide `dashboard-error`, and the
  ungated «Alta de candidato».
- **Rename «Dashboard» to «Inicio»** in the primary navigation (desktop and mobile), the page
  heading and the specs that name it. The route (`/app`), `nav-dashboard` and the `dashboard.*`
  i18n key namespace stay, because they are not user-visible and specs bind to them. This was
  out of scope in the brief and is brought in by request.
- **API:** `GET /api/positions` list items gain `stageCounts` (`new`, `shortlisted`, `interview`,
  `hired`, `rejected`), requiring only `positions.read`. `createdAt` joins the closed candidate
  search sort set, backed by a new filtered index (one EF Core migration, no grant change).
- **Search page:** `/app/search?preset=<id>` applies that preset on load, records the use, selects
  it in the picker and removes the parameter from the URL.
- **Candidate list:** honours `sort=createdAt` (descending by default) from the URL.
- **Demo data (new, by request):** a development-only Node script, `scripts/seed-demo-data.js`
  (`npm run seed:demo` from `frontend/`), that creates through the API a fixed set of fabricated
  candidates with backdated availability checks, open and closed positions with candidates linked at
  every stage, and shared presets with use history. It is idempotent, recognisable (reserved
  `demo.kepler-talento.local` e-mail domain and fixed titles/names), never carries the e2e
  `Date.now()` marker so the Playwright teardown leaves it alone, and offers `--remove` to withdraw
  it. It refuses to run unless the target answers the development token endpoint on a loopback host.

## Capabilities

### New Capabilities

- `operational-dashboard`: the «Inicio» home page — its panels, their permissions, their links,
  their independent loading/empty/error states and the request budget.
- `development-demo-data`: the development-only demo dataset script — what it creates, how it is
  recognised and withdrawn, and the guards that keep it out of any non-development environment.

### Modified Capabilities

- `primary-navigation`: the always-present home entry is labelled «Inicio» instead of «Dashboard»
  (entries, Admin grouping, wide and narrow layouts, top-level pages without a breadcrumb).
- `position-management`: _Bounded position listing_ — list items also carry per-stage counts that
  require only `positions.read` and name no one.
- `candidate-search`: _Bounded deterministic pagination_ — creation time joins the documented sort
  set.
- `saved-search-presets`: _Search page applies presets only_ — the search page accepts a preset id
  parameter and applies it on load.

## Impact

- **Backend:** `IPositionRepository`/`PositionRepository.ListAsync`, `PositionContract`,
  `ListPositions`; `SearchContract`, `CandidateSearchQuery` (SQL and encrypted paths),
  `CandidateConfiguration` plus migration `CandidateCreatedAtSortIndex`. No new endpoint, table,
  permission, grant or NuGet package.
- **Frontend:** `features/dashboard/**` rewritten; `nav-items.ts` label; `position.models.ts`,
  `candidate.models.ts`, `candidate-list.logic.ts`, `advanced-search-page.tsx`; `es.json`
  (`dashboard.*`). No new npm dependency.
- **Scripts:** new `scripts/seed-demo-data.js` (Node built-ins only: `fetch`, `node:util`) and a
  `seed:demo` npm script.
- **Docs:** `docs/ktl-18/list-contract.md`, `docs/ktl-10/search.md`,
  `docs/ktl-30/position-candidates.md`, new `docs/ktl-40/release-notes.md` and
  `docs/ktl-40/demo-data.md`; `README.md` (Spanish).
- **Personal data and security:** the page shows candidate names only (no e-mail, phone or CV
  action); stage counts name no one; the only identifier entering a URL is a preset id; nothing
  new is logged and listing stays unaudited. Every request goes through existing endpoints that
  check `ICurrentActor` and the permission first; hiding a panel is not the control. No RLS, storage
  policy, role or grant changes. The demo script writes **fabricated** personal data through the API
  (so encryption, validation and audit all apply); it is reachable only where the development token
  endpoint exists, which `Program.cs` never maps in Production, and it additionally refuses
  non-loopback targets.
- **Tests:** xUnit (positions, search, schema, query plans), Vitest (dashboard logic and page,
  advanced search, list logic, navigation label, seed script pure helpers), Playwright
  (`dashboard.spec.ts`, navigation specs updated for «Inicio»), security specs.
