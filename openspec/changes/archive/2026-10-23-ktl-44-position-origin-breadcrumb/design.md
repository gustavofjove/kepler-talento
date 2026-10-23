## Context

See proposal.md for motivation and specs/primary-navigation/spec.md for the required trails.

Today the breadcrumb (`shared/components/breadcrumb.tsx`) is presentational: each page builds its
own `BreadcrumbItem[]`. `CandidateDetailPage` always builds `Candidatos › name`. The position detail
page reaches candidates through three components it composes:

- `PositionCandidatesPanel`: linked candidates, a `.row-link` name plus a `useRowLink()` row.
- `SearchResults` (`tableId="position-matches"`): matching candidates, same pattern. The advanced
  search page renders the same component.
- The row CV panel (`RowCvPreviewProvider` / `RowCvSplit`): its «Ver ficha» `Link`
  (`row-cv-open-profile`). The candidate list and Búsqueda use the same panel.

`useRowLink()` calls `navigate(to)` for a plain click and `window.open(to)` for Ctrl/⌘- or middle-
click.

## Goals / Non-Goals

**Goals:**

- Carry "opened from position P (title T)" from those three entry points to the candidate page
  without touching the URL or the API.
- Keep the shared components origin-agnostic, so Búsqueda and the candidate list are unaffected
  without any flag.
- Make the three-segment limit structural, not a counter.

**Non-Goals:**

- A general breadcrumb-history mechanism for other page pairs.
- Refreshing a captured title if the position is renamed later.

## Decisions

### D1. React Router location state, not a query parameter

The origin travels as `location.state = { candidateOrigin: { kind: 'position', positionId, positionTitle } }`.

- The URL stays the candidate's canonical address, so copying or sharing it never implies an origin
  and the spec's "typed or pasted address gives `Candidatos`" holds for free.
- The title arrives with the navigation, so the candidate page needs no `GET /api/positions/{id}`
  and no handling for a stale, deleted or forbidden id.
- Browsers keep `history.state` per entry, which gives the required Back/Forward behaviour;
  `window.open` starts a fresh entry, which gives the new-tab fallback. A reload is not a case: the
  access token lives in memory, so a reload returns to the sign-in page (found during apply; the
  e2e harness's `page.reload` re-authenticates and pushes the URL with empty state).

_Alternative considered:_ `?from=position:<id>`. Survives new tabs, but shows in the URL, needs a
fetch for the title and a failure path for an id the viewer cannot read, and a pasted link would
claim an origin the reader never had. Rejected.

### D2. A context supplies the origin; links consume it

`features/candidates/candidate-origin.ts` (a `.ts` sibling, so fast refresh is unaffected) holds:

- the `CandidateOrigin` type;
- `CandidateOriginContext` (default `undefined`) and `useCandidateLinkState()`, returning
  `{ candidateOrigin }` or `undefined` for the current context;
- `readCandidateOrigin(state: unknown): CandidateOrigin | undefined`, a strict shape check
  (`kind === 'position'`, non-empty string id and title).

`PositionDetailPage` wraps its content in `<CandidateOriginContext.Provider value={…}>` once the
position has loaded. `PositionCandidatesPanel`, `SearchResults` and the CV panel's «Ver ficha» call
`useCandidateLinkState()` and pass the result to `<Link state>` and `rowLink(to, state)`. Outside a
provider the hook returns `undefined`, so those components behave exactly as now on Búsqueda, the
candidate list and Inicio.

_Alternative considered:_ a `linkState` prop drilled through `SearchResults`, `RowCvPreviewProvider`
and the panel. Works, but adds a prop to three shared components for one caller and makes it easy
to miss an entry point. Rejected in favour of the context, which also covers future candidate links
added inside the position page.

### D3. `useRowLink` forwards optional state

`rowLink(to, state?)` passes `{ state }` to `navigate`. The new-tab branch ignores it by design (D1).
Existing callers are unchanged.

### D4. Candidate page builds the trail from a validated origin

`CandidateDetailPage` reads `readCandidateOrigin(useLocation().state)`. With an origin the parent
segments are `Posiciones` (`/app/positions`, testId `breadcrumb-positions`) and the title
(`/app/positions/:id`, testId `breadcrumb-position`, the ids already used on the position edit
page), each a link only when `usePermission('positions.read')`. Without one, the current
`Candidatos` segment. The name segment is appended in both cases exactly as today, so the loading,
failure and not-found branches need no change beyond receiving the parent segments.

### D5. Three segments at most, structurally

No component on the candidate page provides `CandidateOriginContext`, and the candidate page's links
to positions pass no state; the position page's trail ignores `location.state`. Every hop therefore
either starts a fresh origin (position → candidate) or drops it (candidate → anything). No depth
counter is needed.

### Stack, data, authorization and storage impact

- **Stack:** no new dependency; `useLocation` and `Link state` are already in React Router.
- **Data model, storage, migrations, grants:** none.
- **Authorization:** unchanged. The trail only decides whether a segment is a link, using the
  existing `usePermission('positions.read')`; route guards and the API still enforce access.
- **Personal data:** the state holds a position id and title, never candidate data. It lives in the
  browser's session history only, not the URL, document title or logs.

### Test strategy

- Unit: `readCandidateOrigin` accepts the valid shape and rejects `null`, strings, wrong `kind`,
  missing or empty fields.
- Unit (`candidate-detail-page.spec.tsx`, `MemoryRouter` `initialEntries` with `state`): default
  trail; position trail and links; loading and failure keep `Posiciones › title`; malformed state
  falls back; without `positions.read` the two segments are plain text.
- Unit (`position-pages.spec.tsx`): clicking a linked candidate's name, a match's name and a row
  lands on the candidate route with the origin in the location state (assert through a probe route
  that renders `useLocation().state`).
- Unit (search / candidate list specs): existing assertions continue to pass unchanged, proving no
  state is added outside the position page.
- E2E (`positions.spec.ts`): create a marked position and a linked candidate, open the candidate
  from the position, assert `breadcrumb-position` text, activate it and land on the position.

## Risks / Trade-offs

- [The title is captured at navigation time; a rename by another user does not show until the next
  navigation] → Accepted: the stale label still links to the right position by id.
- [Ctrl/⌘-click users get `Candidatos`] → Accepted and specified; a URL-borne origin would trade it
  for the problems listed in D1.
- [A future candidate link on the position page bypasses the hook] → The context makes the default
  path the right one; the unit spec covering each entry point catches regressions in the existing
  three.
- [Existing tests asserting origin independence from position matches] → Update them to the new
  scenario in the same change.

## Migration Plan

Frontend-only and stateless: ships with the next SPA build, rolls back by reverting the build. Old
history entries simply have no origin and fall back to `Candidatos`.
