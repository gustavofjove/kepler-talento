# KTL-23 — Breadcrumb trail on sub-pages

## [original]

Breadcrumb

Add a breadcrumb so for example When you are on an edit or view page you can go back to the index by clicking. This may apply to Candidates, Positions and Presets at least but you can review the pages so it may get applied to some other pages.

## [enhanced]

**Status:** Proposed
**Depends on:** KTL-22 (`ktl-22-edit-page-owns-editing`, in flight). It reshapes the candidate
edit page header that this ticket replaces. Start this change only after KTL-22 is merged.

### Summary

Today, a user on a detail, create or edit page has no consistent way back to the list that page
belongs to. Each page solves this differently, or not at all:

| Page                 | Route                                  | Way back today                                                                   |
| -------------------- | -------------------------------------- | -------------------------------------------------------------------------------- |
| Candidate detail     | `/app/candidates/:id`                  | None when the page loads. «Volver» button only in the error and not-found states |
| Candidate new / edit | `/app/candidates/new`, `…/:id/edit`    | Edit: «Ver candidato» button (`data-testid="candidate-edit-view"`). New: none    |
| Position detail      | `/app/positions/:id`                   | None                                                                             |
| Position new / edit  | `/app/positions/new`, `…/:id/edit`     | «Cancelar» button at the bottom of the form                                      |
| Preset new / edit    | `/app/admin/presets/new`, `…/:id/edit` | None (the page navigates to the list after a save)                               |

This ticket adds one shared, accessible breadcrumb above the heading of every **sub-page** (any
page that sits below a list). It is a **frontend-only** change: no endpoint, contract, permission,
table or grant changes.

### User stories

- **As a recruiter** looking at or editing a candidate, I click «Candidatos» in the breadcrumb to
  go back to the candidate list, without using the browser's Back button or the top navigation.
- **As a recruiter** on the candidate edit page, I click the candidate's name in the breadcrumb
  to return to their read-only profile.
- **As a hiring manager** on a position, or an administrator on a preset, I can see where I am in
  the application and jump back to the parent list in one click.

### Decisions taken (2026-09-23)

1. **Sub-pages only.** Top-level pages (the Dashboard, Candidatos, Búsqueda and Posiciones lists,
   Catálogos, Usuarios, Roles, Importación, Auditoría, and the presets list) get no breadcrumb,
   because the primary navigation already marks them as current.
2. **The breadcrumb replaces «Ver candidato».** On the candidate edit page, the name segment of
   the trail replaces the header button. The button's `data-testid="candidate-edit-view"` moves
   to that breadcrumb link, so existing unit and e2e specs keep working unchanged.
3. **List links go to the plain list URL.** «Candidatos» always links to `/app/candidates`, the
   same target as the nav entry. List filters, sort and page are not restored. The browser's Back
   button still restores them, because they live in the URL.
4. **Admin trails start with a non-link «Admin».** It is rendered as plain text, matching the
   `primary-navigation` rule that the Admin parent has no destination.
5. **The trail is static, not based on where the user came from.** A candidate opened from
   Búsqueda or from a position's matches still shows «Candidatos › …». This keeps the trail
   predictable and able to survive a reload or a pasted link.

### Trails

`›` is the visual separator. The **last** segment is the current page: plain text, never a link.

| Route                         | Trail                                         |
| ----------------------------- | --------------------------------------------- |
| `/app/candidates/:id`         | Candidatos › _Nombre Apellido_                |
| `/app/candidates/new`         | Candidatos › Nuevo candidato                  |
| `/app/candidates/:id/edit`    | Candidatos › _Nombre Apellido_ › Editar       |
| `/app/positions/:id`          | Posiciones › _Título de la posición_          |
| `/app/positions/new`          | Posiciones › Nueva posición                   |
| `/app/positions/:id/edit`     | Posiciones › _Título de la posición_ › Editar |
| `/app/admin/presets/new`      | Admin › Presets › Nuevo preset                |
| `/app/admin/presets/:id/edit` | Admin › Presets › _Nombre del preset_         |

Rules:

- **Labels for loaded data** use the loaded record: the candidate's `firstName lastName`, the
  position's `title`, the preset's `name`. They never use the value being edited in the form, so
  typing a new title does not change the trail until the record is saved and reloaded.
- **While loading**, the parent segments are shown and the record segment is omitted. The page
  never shows an empty or «undefined» label. In the error and not-found states the trail keeps
  its parent link. The existing «Volver» buttons on the candidate detail page may stay.
- **A segment is a link only when the viewer may open its destination.** Otherwise it is plain
  text. «Candidatos» needs `candidates.read`, «Posiciones» needs `positions.read`, «Presets»
  needs `presets.manage`, and the candidate name on the edit page needs `candidates.read`. Use
  `usePermission()` at the top of each page. This is a courtesy only: the route guards and the API
  remain the control.
- **Position new/edit** keeps its «Cancelar» button. The breadcrumb is an extra route back, not
  a replacement.

### UI changes (`frontend/`)

**New shared component:** `src/app/shared/components/breadcrumb.tsx` and `breadcrumb.css`
(plain CSS, not CSS Modules).

```tsx
export interface BreadcrumbItem {
  label: string;   // already translated, or loaded record data
  to?: string;     // omit for plain-text segments ("Admin", or a link the viewer may not open)
  testId?: string;
}
// The last item is always rendered as the current page, whatever its `to` says.
<Breadcrumb items={[...]} />
```

- Markup:
  `<nav aria-label={t('breadcrumb.ariaLabel')} data-testid="breadcrumb"><ol>…</ol></nav>`.
  Each item is an `<li>`. Links use React Router `<Link>`. The last item is a `<span aria-current="page">`.
- Separators are drawn in CSS on `li + li::before` as a shape (for example a rotated border, as
  in the WAI-ARIA breadcrumb example), not as `content: '›'`. Some screen readers announce
  pseudo-element text.
- Styling uses the Kepler tokens (`docs/CORPORATE_IDENTITY_Kepler.md`): small, muted text above
  the `page-header`, with a visible focus ring and no inline styles.
- Responsive without media queries (the shell's only breakpoint lives in `primary-nav.css`):
  items wrap, and a long record label is truncated with an ellipsis, with the full label kept
  in `title`.
- Pages render the component explicitly and build their own `items` from data they already
  hold. No route `handle`/`useMatches` machinery is needed: every dynamic label is already
  loaded on its page.

**Pages to update:**

| File                                                  | Change                                                                                                                                                                |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `features/candidates/pages/candidate-detail-page.tsx` | Add the trail above the header in the loaded, error and not-found states                                                                                              |
| `features/candidates/pages/candidate-edit-page.tsx`   | Add the trail. Remove the «Ver candidato» header button and move `candidate-edit-view` to the name link. Remove `candidate.edit.backToDetail` if nothing else uses it |
| `features/positions/position-detail-page.tsx`         | Add the trail above `position-detail-toolbar`                                                                                                                         |
| `features/positions/position-form-page.tsx`           | Add the trail. Use the loaded title, not the `title` draft state                                                                                                      |
| `features/admin/presets/preset-edit-page.tsx`         | Add the trail. Use the loaded preset name, not the `name` draft state                                                                                                 |

**Copy** (`src/assets/i18n/es.json`, flat keys; no hardcoded JSX copy; `en.json` optional):

| Key                       | Spanish            |
| ------------------------- | ------------------ |
| `breadcrumb.ariaLabel`    | Ruta de navegación |
| `breadcrumb.candidates`   | Candidatos         |
| `breadcrumb.positions`    | Posiciones         |
| `breadcrumb.admin`        | Admin              |
| `breadcrumb.presets`      | Presets            |
| `breadcrumb.edit`         | Editar             |
| `breadcrumb.newCandidate` | Nuevo candidato    |
| `breadcrumb.newPosition`  | Nueva posición     |
| `breadcrumb.newPreset`    | Nuevo preset       |

Where an existing key already holds the same text (for example `nav.positions`), reuse it
instead of adding a duplicate. No file is added to `LEGACY_HARDCODED_COPY`.

### Data contract and API

None. Every label comes from data the page already loads (candidate aggregate, position,
preset). No new endpoint, request, permission, table, migration or `ktl_runtime` grant.

### Acceptance criteria

```gherkin
Scenario: Returning to the candidate list from a candidate
  Given a user with candidates.read viewing /app/candidates/{id}
  Then a breadcrumb reads "Candidatos › {Nombre Apellido}"
  And "{Nombre Apellido}" is not a link and carries aria-current="page"
  When the user activates "Candidatos"
  Then the candidate list opens at /app/candidates

Scenario: Returning to the profile from the edit page
  Given a user with candidates.read and candidates.update on /app/candidates/{id}/edit
  Then the breadcrumb reads "Candidatos › {Nombre Apellido} › Editar"
  And the header no longer contains a separate "Ver candidato" button
  When the user activates the element with data-testid "candidate-edit-view"
  Then /app/candidates/{id} opens

Scenario: Creating a candidate
  Given a user on /app/candidates/new
  Then the breadcrumb reads "Candidatos › Nuevo candidato"

Scenario: Position trails
  Given a user with positions.read viewing a position
  Then the breadcrumb reads "Posiciones › {title}" and "Posiciones" links to /app/positions
  And on the edit route it reads "Posiciones › {title} › Editar" with {title} linking to the detail
  And editing the title field does not change the breadcrumb before saving

Scenario: Preset trails start with a non-link Admin segment
  Given a user with presets.manage on /app/admin/presets/{id}/edit
  Then the breadcrumb reads "Admin › Presets › {name}"
  And "Admin" is plain text, not a link
  And "Presets" links to /app/admin/presets

Scenario: Top-level pages have no breadcrumb
  When a user opens /app, /app/candidates, /app/search, /app/positions, /app/catalogs,
    /app/admin/presets, /app/admin/users, /app/admin/roles, /app/admin/import or /app/admin/audit
  Then no element with data-testid "breadcrumb" is rendered

Scenario: Loading never shows an empty label
  Given the candidate aggregate is still loading
  Then the breadcrumb shows "Candidatos" only, with no empty or placeholder record segment

Scenario: A segment the viewer may not open is plain text
  Given a profile holding candidates.update but not candidates.read on the edit page
  Then "Candidatos" and the candidate name render as text, not links

Scenario: Accessible structure
  Then the breadcrumb is a navigation landmark named "Ruta de navegación"
  And it contains an ordered list, and separators are not exposed to assistive technology
```

### Test coverage

- **Unit** (`frontend/tests/unit/`, Vitest + Testing Library):
  - New `breadcrumb.spec.tsx`: the landmark and its name, `<ol>` structure, links vs plain
    text, `aria-current` on the last item even when it has `to`, and `testId` pass-through.
  - Extend `candidate-edit-page.spec.tsx` (the `candidate-edit-view` href assertion keeps
    passing), `position-pages.spec.tsx` and `preset-edit-page.spec.tsx` with the trail, the
    loading state and the permission-dependent link rule. Add a case to a candidate detail spec.
  - Assert on Spanish text resolved from `es.json`, and locate elements by role, name or `data-testid`.
- **E2E** (`frontend/tests/e2e/`, Playwright): extend `candidate-crud.spec.ts`
  (list → detail → edit → breadcrumb back to list), `positions.spec.ts` and
  `advanced-search-presets.spec.ts` (preset edit → «Presets»). Use `data-testid` selectors only,
  never Spanish text. Existing `candidate-edit-view` clicks in `candidate-crud`,
  `candidate-profile` and `secure-access` must pass unchanged.
- `navigation-responsive.spec.ts`: at narrow width the breadcrumb wraps or truncates without
  horizontal page scroll.
- **Backend / security:** no changes. Existing route-guard specs already cover direct URL access.

### Non-functional requirements

- **Security:** no new data paths. The candidate name is already shown in the page's `<h1>`, so
  the breadcrumb exposes nothing new. It must not be copied into `document.title`, the URL or logs.
  Hiding a link is never the control: route guards and API authorization stay unchanged.
- **Accessibility:** follows the WAI-ARIA breadcrumb pattern: a `nav` landmark with a Spanish
  accessible name, an ordered list, `aria-current="page"`, CSS separators, keyboard-reachable
  links and a visible focus ring. It adds no extra tab stop for the current page.
- **Responsive:** one DOM tree for every width, with no `matchMedia` or `innerWidth` checks.
- **Performance:** no extra requests. Labels come from data already loaded.

### Documentation

- `openspec/specs/primary-navigation/spec.md`: add a requirement, «Breadcrumb trail on sub-pages»,
  with the trails, the plain-text Admin rule, the permission-dependent link rule and the
  no-breadcrumb rule for top-level pages (through the change's delta spec).
- `openspec/specs/candidate-management/spec.md`: if KTL-22's delta spec names the «Ver candidato»
  button, update it to the breadcrumb link.
- `README.md` (Spanish): no change needed unless it describes the edit page's «Ver candidato» button.

### Out of scope

- Breadcrumbs on top-level pages, the Búsqueda page or the Admin pages that have no sub-pages.
- Restoring list filters, sort or page from the breadcrumb.
- Trails that depend on the page the user came from (Búsqueda, position matches).
- Moving the `nav-items.ts` labels to i18n keys, or removing `app-layout.tsx` from `LEGACY_HARDCODED_COPY`.
