# KTL-40 — Dashboard built around positions and availability

## [original]

The dashboard is not very useful the way it is, and it still reflects the very first version of the
application. It should summarise each section and offer shortcuts to the most used options: open
positions with their candidates, total candidates and how many are available, the latest candidates
marked as available, and so on.

Problems with the current page (`frontend/src/app/features/dashboard/dashboard-page.tsx`):

- The figures do not lead anywhere. «Con CV principal» is just active minus «Sin CV principal», and
  no tile is a link.
- Positions (KTL-15/KTL-30) and availability (KTL-36), the two things HR works with daily, are
  absent.
- «Centro operativo» is two buttons that duplicate the primary navigation.
- «Nuevo candidato» is shown to every actor, without checking `candidates.create`.
- One failed count blanks every tile.

Agreed direction (explored in conversation):

- **Quick actions** in the header, each shown only with its permission: «Nuevo candidato»
  (`candidates.create`), «Nueva posición» (`positions.manage`), «Importar CSV»
  (`candidates.import`). No buttons that repeat the navigation.
- **Candidatos** card: active total, with an availability bar and the split «disponibles · no
  disponibles · sin comprobar». «Sin comprobar» is derived as total minus the other two. Each part
  links to the candidate list filtered by it (`?availability=…`).
- **Posiciones abiertas** tile (count, link to the positions list) and **Sin CV principal** tile
  (count, link to `?cv=no`).
- **Posiciones abiertas** panel: the most recently updated open positions with per-stage counts
  (Nuevo, Preseleccionado, Entrevista, Contratado, Descartado) and the total, plus «Ver todas».
- **Últimos disponibles** panel: candidates whose availability is `available`, most recently checked
  first, with the elapsed time, plus «Ver todos». This means "most recently _confirmed_": only the
  latest check is stored, so a «Sigue igual» moves someone to the top, and "recently _became_
  available" cannot be derived. Accepted.
- **Últimos añadidos** panel: the most recently created candidates with their availability and CV
  state, plus «Ver todos». Ordered by record creation (`createdAt`), which is always present; a bulk
  import fills the panel, which is accepted.
- **Búsquedas guardadas** panel: the shared presets most recently used, each opening the advanced
  search with that preset applied.
- Remove «Con CV principal» and «Centro operativo». Keep «Inactivos» only as a small link for
  `candidates.delete` holders.
- Each panel loads, fails and shows its empty state independently. A panel the actor may not see
  sends no request.
- Build it from the existing endpoints. No dedicated dashboard summary endpoint.

Backend additions needed:

- `GET /api/positions` list items gain per-stage link counts. Like `candidateCount`, they name no
  one, so `positions.read` alone is enough.
- `createdAt` joins the closed set of candidate search sort fields.

Frontend-only addition: `/app/search` accepts `?preset=<id>` and applies that preset on load.

Later tickets, out of scope here: lapsed «No disponible hasta» candidates to recheck, candidates past
their `reviewDueAt`, recent stage activity across positions, the last import batch, and renaming the
«Dashboard» navigation label.

## [enhanced]

**Status:** Ready for an OpenSpec change
**Depends on:** KTL-14 (shared presets), KTL-15/KTL-30 (positions and links), KTL-18 (list
contract and URL), KTL-36 (availability checks), KTL-38 (chip tones)

### Summary

Replace the first-version dashboard with a home page that answers three questions at a glance. Each
answer leads to the screen where the user acts on it:

1. How many candidates are there, and how many are available?
2. How are the open positions progressing?
3. Who was recently confirmed available, and who was recently added?

It also offers the create actions and the shared saved searches in one click. Every figure and every
row is a link.

Two small API additions back it:

- per-stage link counts on the position list;
- `createdAt` as a candidate search sort field.

Everything else uses existing endpoints. There is no new endpoint, table, permission, grant or
runtime dependency.

### User story

As an HR user, I want the home page to summarise candidates, availability and open positions, and to
take me straight to the filtered list, position or saved search behind each figure, so that I can
start my daily work without rebuilding the same views from the menu.

### Layout

```
Dashboard                            [Alta de candidato] [Nueva posición] [Importar candidatos]
Resumen de candidatos, posiciones y búsquedas guardadas.

┌ Candidatos ───────────────────────────────┐ ┌ Posiciones abiertas ┐ ┌ Sin CV principal ┐
│ 312 activos                               │ │ 7                   │ │ 23               │
│ ██████▓▓▓░░░░░░░░░░░░░░░░                 │ └─────────────────────┘ └──────────────────┘
│ 84 disponibles · 41 no disponibles ·      │
│ 187 sin comprobar           12 inactivos  │
└───────────────────────────────────────────┘

┌ Posiciones abiertas ──────────────────────────────────────────────────── Ver todas (7) ┐
│ Posición            Nuevo  Preseleccionado  Entrevista  Contratado  Descartado  Candidatos │
│ Desarrollador .NET    3           2              1           0           4          10     │
│ Técnico de soporte    1           0              2           1           0           4     │
└────────────────────────────────────────────────────────────────────────────────────────────┘

┌ Últimos disponibles ──────── Ver todos (84) ┐ ┌ Últimos añadidos ─────────── Ver todos ┐
│ Ana Ruiz      [Disponible] hace 2 días      │ │ Luis Pérez   [Sin comprobar]   Sin CV  │
│ Marta Gil     [Disponible] hace 5 días      │ │ Eva Soler    [Disponible] hace 1 día   │
└─────────────────────────────────────────────┘ └────────────────────────────────────────┘

┌ Búsquedas guardadas ────────────── Gestionar presets ┐
│ Java sénior Madrid              Usada hace 3 días    │
│ Soporte N2 con inglés           Sin usar             │
└──────────────────────────────────────────────────────┘
```

At 390 px the panels stack in this order.

### Panels

| Panel                    | Shown with                                                                                   | Data                                                                                                                                                                                                                                                                                                   | Content and links                                                                                                                                                                                                                                                                                        |
| ------------------------ | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Header actions           | each action its own permission: `candidates.create`, `positions.manage`, `candidates.import` | —                                                                                                                                                                                                                                                                                                      | «Alta de candidato» → `/app/candidates/new`; «Nueva posición» → `/app/positions/new`; «Importar candidatos» → `/app/admin/import`.                                                                                                                                                                       |
| Candidatos               | `candidates.read`                                                                            | Active total = `totalCount` of «Últimos añadidos». Available = `totalCount` of «Últimos disponibles». Unavailable = one count search. `unknown` = total − available − unavailable, never below 0. With `candidates.delete`: one count with `includeInactive`; inactive = that − active, never below 0. | «N activos» → `/app/candidates`. Each part of the split → `?availability=available&sort=availabilityCheckedOn&dir=desc`, `?availability=unavailable`, `?availability=unknown`. «N inactivos» is plain text.                                                                                              |
| Posiciones abiertas tile | `positions.read`                                                                             | `totalCount` of the positions panel request.                                                                                                                                                                                                                                                           | → `/app/positions` (opens on open positions by default).                                                                                                                                                                                                                                                 |
| Sin CV principal tile    | `candidates.read`                                                                            | Count search with `hasCv: no`.                                                                                                                                                                                                                                                                         | → `/app/candidates?cv=no`.                                                                                                                                                                                                                                                                               |
| Posiciones abiertas      | `positions.read`                                                                             | `GET /api/positions?status=open&sortField=updatedAt&sortDirection=desc&pageSize=5`.                                                                                                                                                                                                                    | One row per position: the title, the five stage counts and `candidateCount`. The title is the keyboard link to the position, and the whole row is clickable, as in the position list. «Ver todas (N)» → `/app/positions`. When empty: «No hay posiciones abiertas.», plus «Nueva posición» for managers. |
| Últimos disponibles      | `candidates.read`                                                                            | Search with `availabilityValues: [available]`, sorted by `availabilityCheckedOn` desc, `pageSize` 5.                                                                                                                                                                                                   | Name → candidate page, then the availability chip with the elapsed time (`AvailabilityCell`). «Ver todos (N)» → the same URL as the «disponibles» link.                                                                                                                                                  |
| Últimos añadidos         | `candidates.read`                                                                            | Search with no filter, sorted by `createdAt` desc, `pageSize` 5.                                                                                                                                                                                                                                       | Name → candidate page, the availability chip with the elapsed time, and «Sin CV» when there is no primary CV. «Ver todos» → `/app/candidates?sort=createdAt&dir=desc`.                                                                                                                                   |
| Búsquedas guardadas      | `candidates.read`                                                                            | `GET /api/search-presets` through `searchPresetsService.load()`.                                                                                                                                                                                                                                       | Top 5 by `lastUsedAt` desc, never-used presets last, then by name. Name → `/app/search?preset=<id>`. Each row shows «Usada hace …» or «Sin usar». With `presets.manage`: «Gestionar presets» → `/app/admin/presets`. When empty: «No hay búsquedas guardadas.»                                           |

An actor holding neither `candidates.read` nor `positions.read` sees the header, any actions they
hold, and «No tienes acceso a ningún resumen.»

### Decisions

| Topic                             | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Data source                       | Compose the page from existing endpoints. There is no `/api/dashboard` endpoint. It would be a new mixed-permission contract that re-implements every panel's permission rule. Revisit only with measured load-time evidence.                                                                                                                                                                                                                           |
| Request budget                    | At most 7 requests, in parallel: 5 searches, 1 position list and 1 preset list. Count searches use `pageSize` 1 and list searches `pageSize` 5. No search has a text filter, so all stay on the SQL path. Searching and listing are not audited.                                                                                                                                                                                                        |
| Independent panels                | Each panel has its own loading (`role="status"`), empty and error states, so one failure never blanks another panel. Requests are aborted on unmount.                                                                                                                                                                                                                                                                                                   |
| No request without permission     | A panel the actor may not see is not rendered and sends no request. The position page follows the same rule for links. Hiding a panel is not the control: the API already refuses.                                                                                                                                                                                                                                                                      |
| Minimal personal data             | Candidate rows show the name only: no e-mail, phone or CV action. The detail is one click away.                                                                                                                                                                                                                                                                                                                                                         |
| «Últimos disponibles»             | Means most recently _confirmed_ available. Rows are ordered by check date, so a «Sigue igual» moves a candidate up. Only the latest check is stored, so "recently _became_ available" cannot be derived.                                                                                                                                                                                                                                                |
| «Últimos añadidos»                | Ordered by `CreatedAtUtc`, which every candidate has. After a bulk import or an Access load the panel shows the imported records, which is accepted. Rows do not show the creation date, because the search projection does not change.                                                                                                                                                                                                                 |
| `createdAt` in the list           | The list honours `sort=createdAt` from the URL like any contracted field, descending by default. No column is added, so no header shows a sort indicator. Clicking a header switches to that column's sort, as today.                                                                                                                                                                                                                                   |
| Stage counts permission           | `positions.read` alone, like `candidateCount`: a count per stage names no one. Links to removed candidates are counted, as `candidateCount` already does, so the five counts always add up to `candidateCount`.                                                                                                                                                                                                                                         |
| Inactive count                    | Kept for `candidates.delete` holders, as plain text rather than a link. `?inactive=1` lists active and removed candidates together, so a link labelled «inactivos» would mislead.                                                                                                                                                                                                                                                                       |
| Rows per panel                    | 5.                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `?preset=<id>` on the search page | Advanced search applies the preset on load through the existing «use» call, which records the use. It then selects the preset in the picker and removes the parameter with a replace navigation, so a reload or Back does not apply it again. A value that is not a GUID, or a preset that no longer exists, shows the existing apply-failed error and runs the default empty search. The id is not personal data, and the filters never enter the URL. |
| Removed                           | «Con CV principal» (redundant), «Centro operativo» (duplicates the navigation) and the page-wide `dashboard-error`. «Alta de candidato» now requires `candidates.create`.                                                                                                                                                                                                                                                                               |
| Availability bar                  | Decorative (`aria-hidden="true"`); the figures beside it are the accessible content. Segment colours reuse the KTL-38 chip tones: available is `success`, unavailable `danger` and unknown `neutral`. The bar is hidden when the total is 0.                                                                                                                                                                                                            |

### API and data changes

**Stage counts on the position list** (`GET /api/positions`):

- `backend/Application/Abstractions/Persistence/IPositionRepository.cs`: `PositionSummary` gains the
  stage counts.
- `backend/Application/Features/Positions/PositionContract.cs`:
  - `PositionListItemResponse` gains
    `StageCounts: PositionStageCountsResponse(int New, int Shortlisted, int Interview, int Hired, int Rejected)`,
    which follows the `PositionCandidateStages` vocabulary.
  - Wire shape: `"stageCounts": { "new": 3, "shortlisted": 2, "interview": 1, "hired": 0, "rejected": 4 }`.
  - `candidateCount` stays.
- `backend/Application/Features/Positions/ListPositions.cs`: maps the counts. The guard is unchanged
  (`PositionGuards.RequireRead`).
- `backend/Infrastructure/Persistence/PositionRepository.cs` (`ListAsync`): counts links per stage
  in the same projection, either as correlated counts or as a grouped subquery. The design picks one,
  with EXPLAIN evidence. `UX_OPS_PositionCandidates_PositionId_CandidateId` serves it, and the bound
  is 500 links per position and 100 positions per page.
- No migration and no grant change.

**`createdAt` sort field** (`POST /api/candidates/search`):

- `backend/Application/Features/Search/SearchContract.cs`: adds `SearchSortField.CreatedAt` (wire
  value `createdAt`) to the closed parse switch, next to `availabilityCheckedOn`.
- `backend/Infrastructure/Persistence/CandidateSearchQuery.cs`:
  - SQL path (`Order`): `CreatedAtUtc` ascending or descending, then the id tie-breaker.
  - Encrypted stage, used by text searches: `EncryptedSearchRow` gains `CreatedAtUtc` and `Comparer`
    handles the field, so a text search sorted by creation orders identically.
- `backend/Infrastructure/Persistence/Configurations/CandidateConfiguration.cs`: adds index
  `IX_CND_Candidates_IsActive_CreatedAtUtc` on (`CreatedAtUtc` desc, `Id`), filtered on
  `"IsActive"`, mirroring the availability index.
- One EF Core migration:
  `dotnet ef migrations add CandidateCreatedAtSortIndex --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`.
  No grant change: `ktl_runtime` already reads `CND_Candidates`.
- The search projection is unchanged.

### Frontend changes (paths under `frontend/src/app/`)

- `features/dashboard/`:
  - `dashboard-page.tsx`, rewritten. It composes the panels below, reads every permission with
    `usePermission()` at the top and passes the booleans down.
  - `dashboard.logic.ts`, with the pure helpers:
    - the search query for each panel;
    - the availability split, with the never-below-zero rule;
    - preset ordering and the top 5;
    - list hrefs, built with `writeListView` from `features/candidates/pages/candidate-list.logic.ts`
      so that URL parameter names stay in one place.
  - `components/`:
    - `dashboard-actions.tsx`;
    - `candidate-summary.tsx`: the card, the bar and the «Sin CV principal» tile;
    - `open-positions-panel.tsx`;
    - `recent-candidates-panel.tsx`, one component used for both «Últimos disponibles» and «Últimos
      añadidos»;
    - `saved-searches-panel.tsx`;
    - `dashboard-panel.tsx`, the shared shell for the title, «Ver todos» and the loading, empty and
      error states.
  - `dashboard.css`: co-located plain CSS (no CSS Modules) using the Kepler tokens. Columns use the
    existing `.grid` rules, with no new breakpoint.
- Data access:
  - `candidateService.listPage(query, signal)` and `positionService.search(query, signal)`, which
    leave shared list state untouched. Do not use `positionService.list`: it overwrites the position
    list page's signal.
  - Presets through `searchPresetsService.load()` and the `useSearchPresets()` hook.
- `features/positions/position.models.ts`: `PositionListItem.stageCounts: Record<PositionCandidateStage, number>`.
- `features/candidates/models/candidate.models.ts`: `CandidateSortField` gains `'createdAt'`.
  In `features/candidates/pages/candidate-list.logic.ts`, `defaultDirection('createdAt')` returns
  `desc`.
- `features/search/pages/advanced-search-page.tsx`: reads `preset` with `useSearchParams` on mount
  (see Decisions).
- Reused pieces:
  - `AvailabilityCell` (`features/candidates/components/availability-cell.tsx`);
  - `StatusChip` and its tones;
  - `formatNumber` and `formatElapsed`;
  - `useRowLink`;
  - `.data-table` and `.table-wrap`.
- `data-testid`s:
  - new: `dashboard-actions`, `dashboard-candidates`, `dashboard-positions`,
    `dashboard-recent-available`, `dashboard-recent-added`, `dashboard-saved-searches`;
  - kept on their figures: `kpi-active`, `kpi-without-cv`, `kpi-inactive`;
  - removed, since no spec uses them today: `kpi-with-primary-cv` and `dashboard-error`.
- **Copy** (`assets/i18n/es.json`, under `dashboard.*`):
  - Remove `dashboard.kpi.withPrimaryCv`, `dashboard.shortcuts.*` and `dashboard.error`.
  - Subtitle: «Resumen de candidatos, posiciones y búsquedas guardadas.»
  - Card figures:
    - «{{value}} activos», «{{value}} disponibles», «{{value}} no disponibles», «{{value}} sin
      comprobar» and «{{value}} inactivos»;
    - each as `_one`/`_other` plurals selected on `count`, with `value` set to
      `formatNumber(count)`.
  - Panels: «Últimos disponibles», «Últimos añadidos», «Búsquedas guardadas», «Ver todas ({{value}})»,
    «Ver todos ({{value}})», «Ver todos», «Sin CV», «Usada {{elapsed}}», «Sin usar», «Gestionar
    presets» and «Importar candidatos».
  - States: «Cargando…», «No se ha podido cargar este resumen.», «No hay posiciones abiertas.»,
    «Ningún candidato está marcado como disponible.», «Todavía no hay candidatos.», «No hay
    búsquedas guardadas.» and «No tienes acceso a ningún resumen.»
  - Reuse `candidate.new`, `positions.actions.create`, `positions.stage.*`,
    `positions.list.candidates` and `candidate.availability.state.*`.

### Acceptance criteria

1. **Candidate card.** Given 10 active candidates (4 `available`, 2 `unavailable` and 4 `unknown`),
   when a reader with `candidates.read` opens the dashboard, then:
   - the card shows 10 activos, 4 disponibles, 2 no disponibles and 4 sin comprobar;
   - each figure opens the candidate list with the matching filter in the URL.
2. **Inactive.** With `candidates.delete` and 3 removed candidates, «3 inactivos» is shown as text.
   Without that permission, no `includeInactive` search is sent.
3. **Sin CV principal.** The tile shows the number of active candidates without a primary CV and
   opens `/app/candidates?cv=no`.
4. **Open positions.** Given 6 open positions and 1 closed one, then:
   - the tile shows 6;
   - the panel lists the 5 most recently updated open positions;
   - a position with links at Nuevo ×3, Entrevista ×1 and Descartado ×2 shows 3, 0, 1, 0, 2 and a
     total of 6;
   - clicking the row or its title opens the position.
5. **Stage counts API.** Every item of `GET /api/positions` carries `stageCounts`, and:
   - its five values add up to `candidateCount`, including links to removed candidates;
   - an actor with `positions.read` but without `candidates.read` receives them;
   - the response contains no candidate identifier or name.
6. **Últimos disponibles.** Given candidates checked `available` on 01/10, 03/10 and 05/10, and one
   checked `unavailable` on 06/10, then:
   - the panel lists the three available candidates, most recent check first, with the elapsed time;
   - «Ver todos (3)» opens the list filtered by `available` and sorted by check date descending.
7. **Últimos añadidos.** The panel lists the five most recently created active candidates, newest
   first. Each row shows its availability, and «Sin CV» when there is no primary CV. «Ver todos»
   opens the list sorted by `createdAt` descending.
8. **`createdAt` sort.** `POST /api/candidates/search` with `sortField=createdAt`:
   - orders by creation time in either direction, with the id tie-breaker;
   - behaves the same with and without a text filter;
   - returns pages that do not overlap.

   An unknown sort field is still refused.

9. **Saved searches.** Given presets last used 3 days ago, 1 day ago and never, the panel lists them
   in the order 1 day, 3 days, never. Activating one opens the advanced search with:
   - its filters applied and the results loaded;
   - the preset selected in the picker;
   - its last-used time updated;
   - no `preset` parameter left in the URL.
10. **Bad preset link.** `/app/search?preset=<deleted id>` and `?preset=abc` show the apply-failed
    error, empty filters and the default results.
11. **Permissions per panel.**
    - An actor with `positions.read` only sees the positions tile and panel. No
      `/api/candidates/search` or `/api/search-presets` request is sent.
    - An actor with `candidates.read` only sees the candidate panels. No `/api/positions` request is
      sent.
    - An actor with neither sees «No tienes acceso a ningún resumen.», and the dashboard sends no
      request.
12. **Actions.** Each header action appears only with its permission. «Alta de candidato» is absent
    without `candidates.create`.
13. **Independent failure.** When the positions request fails, the positions tile and panel show the
    error message, and every candidate panel still shows its data.
14. **Empty states.** With no open positions, no available candidates, no candidates and no presets,
    each panel shows its own empty message. Managers also see «Nueva posición» in the empty
    positions panel.
15. **Accessibility and layout.**
    - Each panel is a labelled region with a heading.
    - Loading is announced.
    - The bar is hidden from assistive technology.
    - Every link is reachable by keyboard.
    - At 390 px the panels stack and the page does not scroll horizontally; the positions table may
      scroll inside its own container.

### Verification

- **Backend (xUnit):**
  - `PositionApiTests`: criterion 5, including a reader without `candidates.read`. Unauthenticated
    and unauthorized callers still get 401/403.
  - `PositionHandlerTests`: the stage count mapping.
  - `PositionQueryPlanTests`: the representative lists with stage counts stay within their budget.
  - `SearchApiTests` and `SearchHandlerTests`: criterion 8 on both the SQL path and the encrypted
    stage, and an unknown sort is still refused.
  - `SearchSchemaTests`: the new index exists with its filter.
  - `SearchQueryPlanTests`: a `createdAt` sort over the active population uses the new index.
  - `ProjectDependencyTests` keeps passing.
- **Frontend unit (Vitest + Testing Library, `frontend/tests/unit/`):**
  - `dashboard.logic.spec.ts`: the split, including never below zero, the preset ordering and the
    hrefs.
  - `dashboard-page.spec.tsx`, with doubles through `<ServicesProvider>`:
    - criteria 1–4, 6, 7 and 11–14;
    - criterion 9 for the panel only;
    - assertions on which requests are and are not sent.
  - `advanced-search-page.spec.tsx`: the `preset` parameter, on success and on failure.
  - `candidate-list.logic.spec.ts`: the default direction for `createdAt`.
- **E2e (Playwright, `frontend/tests/e2e/dashboard.spec.ts`):**
  - Seed a marked position with candidates linked at different stages, a marked candidate checked
    `available` and a marked preset.
  - Check that the figures open the right views, the stage counts, both recent lists and the preset
    shortcut.
  - Select by role, label and test id. Every record the spec creates carries a `Date.now()` marker.
- **Security (`frontend/tests/security`):** the stage counts reveal no candidate identity, and the
  dashboard sends no request for a panel the actor may not see. `npm run security:rls` is unchanged.
- **Gates:** `npm test`, `npm run test:backend`, `npm run lint`, `npm run format:check`,
  `npm run build:all`.

### Documentation

- **New capability spec:** `operational-dashboard`, covering the panels, their permissions, their
  links and their independent states. There is no dashboard spec today.
- **Delta specs:**
  - `position-management`, _Bounded position listing_: list items also carry per-stage counts, which
    require only `positions.read`.
  - `candidate-search`, _Bounded deterministic pagination_: creation time joins the documented sort
    set.
  - `saved-search-presets`, _Search page applies presets only_: the search page accepts a preset id
    parameter.
- **Docs:**
  - `docs/ktl-18/list-contract.md` and `docs/ktl-10/search.md`: the new sort field.
  - `docs/ktl-30/position-candidates.md`, § Position list: the stage counts.
  - `docs/ktl-40/release-notes.md`.
- **`README.md`** (in Spanish): in the KTL-18 section, replace the dashboard sentence with a short
  description of the new dashboard.

### Non-functional requirements

- **Security and personal data:**
  - No new endpoint or permission. Every request goes through existing endpoints that fail closed.
  - Stage counts name no one.
  - Candidate rows show names only.
  - Nothing new is logged.
  - The only identifier in a URL is a preset id. No search term, candidate value or filter enters a
    URL.
  - Searching and listing stay unaudited.
- **Performance:**
  - The requests run in parallel.
  - With no text filter, decryption covers only the 10 names shown.
  - The search p95 budget of ≤ 300 ms (KTL-33) still holds, and the new index serves the
    `createdAt` sort.
- **Accessibility:**
  - labelled regions and headings;
  - loading and errors announced;
  - every value given as text, never by colour alone;
  - links reachable by keyboard.
- **Responsive:** the existing `.grid` rules apply, with no new breakpoint and no width checks in
  code. There is no horizontal page overflow at 390 px.

### Out of scope

- Lapsed «No disponible hasta» candidates to recheck (needs a filter on the «hasta» date).
- Candidates past `reviewDueAt` (needs a review-date filter).
- Recent stage activity across positions (needs a cross-position link query).
- The status of the last import batch.
- Stage counts on the Posiciones page.
- A «Fecha de alta» column, and `createdAt` in the search projection.
- Renaming the «Dashboard» navigation label.
- A per-user view («mis posiciones»): the domain has no ownership.
