## Context

See proposal.md (Why) for the motivation and `specs/primary-navigation/spec.md` for the required
trails and behaviour. The relevant current state:

- **Routing.** `createBrowserRouter` in `frontend/src/app/app.tsx`, with the page elements behind
  `RequirePermission` layout routes. Each page renders its own `<section className="page">` with a
  `.toolbar` and a `.page-header` holding the `<h1>`. There is no shared page-header component.
- **Candidate pages.** The detail page returns early for three states: loading, error
  (`data-testid="candidate-detail-error"`) and not found. The error and not-found states each have a
  «Volver» button. After KTL-22, the edit page header holds the heading, the save hint and the
  «Ver candidato» link (`candidate-edit-view`). Both pages read the aggregate through
  `useCandidate(id)`.
- **Position pages.**
  - The detail page keeps the loaded `position` in state. It returns a single `role="status"`
    paragraph until the position arrives. A failed load only raises a toast, so it stays in that
    state.
  - `position-form-page.tsx` loads the position into draft state (`title`, …) and returns a status
    paragraph until `ready`. It keeps no copy of the stored title.
- **Preset edit page.** It loads into a draft `name`. A not-found preset redirects to the list with a
  toast. A failed load shows `preset-load-error`. It keeps no copy of the stored name.
- **Constraints** (`AGENTS.md`):
  - plain co-located CSS, no CSS Modules
  - Kepler tokens and no inline styles
  - `usePermission()` hoisted to the top of the component
  - no hardcoded JSX copy, and no file added to `LEGACY_HARDCODED_COPY`
  - one DOM tree per width, and the 768 px breakpoint stays in `primary-nav.css`
  - e2e selectors by `data-testid`, never by Spanish text

## Goals / Non-Goals

**Goals:**

- One presentational `Breadcrumb` component that every sub-page renders the same way.
- Keep all authorization reasoning in the pages, next to the other `usePermission()` calls. The
  component itself knows nothing about permissions or routes.
- Keep `candidate-edit-view` working, so no existing spec has to change its selector.

**Non-Goals:**

- A route-metadata system (route `handle` plus `useMatches`) or a breadcrumb registry.
- Changing `nav-items.ts`, the primary navigation, or `app-layout.tsx`, which stays in
  `LEGACY_HARDCODED_COPY`.
- Adding error states that a page lacks today, such as position detail's load failure.

## Decisions

### 1. An explicit per-page component, not route metadata

`frontend/src/app/shared/components/breadcrumb.tsx` exports:

```tsx
export interface BreadcrumbItem {
  label: string;
  /** Rendered as a link only when set. The page omits it when the viewer may not open it. */
  to?: string;
  /** Marks the segment naming the current page. Never rendered as a link. */
  current?: boolean;
  testId?: string;
}
export function Breadcrumb({ items }: { items: BreadcrumbItem[] }): JSX.Element;
```

Each page builds its `items` from state it already holds.

**Alternative considered:** a `handle: { crumb }` on each route, collected with `useMatches()` in
`AppLayout`. This was rejected for three reasons. The dynamic labels (candidate name, position
title, preset name) live in page state, so a handle would need a second data path or a context to
pull them back up. It would also put the trail in `AppLayout`, a legacy-copy file. And eight static
call sites do not justify the extra machinery.

### 2. `current` is explicit, not "the last item"

The page marks the current-page segment with `current: true`. While the record loads, the page
leaves out the record segment and everything after it. The remaining parent is then the last item,
but it is not the current page, and the spec requires it to stay a link. An explicit flag keeps
that case correct. The alternative, treating the last item as current, would have to special-case
loading in every page.

A `current` item renders as `<span aria-current="page">` even if `to` is set. An item without `to`
and without `current` renders as a plain `<span>`. That covers «Admin» and segments the viewer may
not open.

### 3. Markup and styling

- The markup is
  `<nav className="breadcrumb" aria-label={t('breadcrumb.ariaLabel')} data-testid="breadcrumb">`
  with an `<ol>` of `<li>`s. Links are React Router `<Link>` elements with the item's `testId`.
- Separators are drawn in `breadcrumb.css` as a rotated-border chevron on `li + li::before`, not as
  `content: '›'`. Some screen readers announce pseudo-element text, and the spec requires silent
  separators.
- The trail is `display: flex; flex-wrap: wrap`, with small muted text from the Kepler tokens and
  the shared focus ring. Each label is capped with `max-width`, `overflow: hidden`,
  `text-overflow: ellipsis` and `white-space: nowrap`, and carries a `title` attribute with the full
  label. CSS clips the text only visually, so the accessible name stays complete. There are no media
  queries.
- The trail renders as the first child of the page's `<section className="page">`, above the
  `.toolbar`. The candidate detail page's early-return states render the trail as a sibling before
  their panel, inside a fragment, so `candidate-detail-error` keeps the same element.

### 4. Permissions are resolved in the page

Each page hoists the permissions it needs and sets `to` only when the permission is held:

| Page                   | Hooks added                                                       |
| ---------------------- | ----------------------------------------------------------------- |
| Candidate detail, edit | `usePermission('candidates.read')`                                |
| Position detail        | none (the route already requires `positions.read`)                |
| Position form          | `usePermission('positions.read')`                                 |
| Preset edit            | none (the route requires `presets.manage`, the list's permission) |

This is a display courtesy only. The route guards stay as they are.

### 5. Stored labels come from separate state

- `position-form-page.tsx` gains `storedTitle`, set in the existing load effect next to `setTitle`.
- `preset-edit-page.tsx` gains `storedName`, set next to `setName`.
- The trails read these, never the draft. Both pages navigate away after a save, so nothing needs
  to update them.
- Candidate pages read the aggregate from `useCandidate`. It holds the stored record: on the edit
  page the form keeps its own draft, and the aggregate only changes once a save is absorbed. This
  matches the spec ("the stored record").

### 6. Candidate edit page: the name link replaces «Ver candidato»

The `.toolbar` button is removed. The name segment carries `testId: 'candidate-edit-view'` and
`to: /app/candidates/:id` when `candidates.read` is held. `candidate.edit.backToDetail` is removed
from `es.json` and `en.json`, because nothing else references it.

For `/candidates/new`, the trail is `Candidatos` › `Nuevo candidato` (current). The existing
`candidate.edit.titleNew` («Alta de candidato») stays as the heading.

### 7. Dedicated `breadcrumb.*` keys

The trail labels get their own keys: `ariaLabel`, `candidates`, `positions`, `admin`, `presets`,
`edit`, `newCandidate`, `newPosition` and `newPreset`. Some values match existing keys today
(`positions.list.title`, `positions.form.createTitle`, `presets.form.newTitle`), but those keys are
page titles and can drift independently. For example, `presets.list.title` is already «Presets de
búsqueda», while the trail wants «Presets». Dedicated keys keep the trail stable.

### 8. No backend, dependency or data change

Labels come from data already loaded, so there is no request, endpoint, migration, grant or package.
Principles 1 and 3 are unaffected (see the proposal's Impact).

## Risks / Trade-offs

- [Risk] The eight call sites could drift and disagree about a trail. → Mitigation: the unit specs
  assert each trail, and the spec's table is the single reference.
- [Risk] Removing «Ver candidato» leaves the edit page with only a small text link back to the
  profile. → Mitigation: the link keeps its test id and stays keyboard-reachable with a visible
  focus ring. The e2e specs that click it prove it still works.
- [Risk] A custom role with `candidates.update` but not `candidates.read` sees no link back from
  the edit page. → Accepted: that role cannot open the profile anyway. The route guard is the
  control.
- [Trade-off] «Candidatos» does not restore list filters, so a user who filtered the list has to
  reapply them, or use the browser's Back button, which restores them from the URL. This was chosen
  for predictability (ticket decision 3).
- [Trade-off] A failed position-detail load still shows the loading paragraph. The trail at least
  offers «Posiciones» there. Adding a proper error state is out of scope.

## Migration Plan

This is a frontend-only change with no data migration. It ships with the next SPA build, after
KTL-22 is merged. To roll back, revert the change.
