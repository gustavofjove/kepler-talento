## [original]

Consistent tables across the application

Apply the same styles and UX approach introduced in KTL-30 for the position candidate tables to the tables of Candidatos, Posiciones, Presets, Catálogos and Usuarios.

- The whole row opens the record, except for the actual links and controls inside it (e-mail, dropdowns, buttons). The name stays as a link for keyboard users, and separate "View"/"Ver"/"Abrir"/"Detalle" buttons are removed.
- Values are vertically aligned and dropdowns are compact, so every row reads as one line.
- Phones are plain text.

Some of these sections still have separate view and edit pages (for example presets). Merging them is outside this task and will be done separately.

--

## [enhanced]

# KTL-31 — Consistent tables across the application

## User story

As an **HR user**, I want every list in the application to look and behave the same way, so that I can scan rows at a glance and open a record by clicking it, without hunting for a different button on each screen.

## Background

KTL-30 introduced a table pattern for the position candidate tables and the advanced search results:

- a click on a row opens the record, except on the links and controls inside it;
- Ctrl/⌘-click and middle-click open a new tab;
- the name stays a real link, styled as plain text, so keyboard and screen-reader users reach the same page;
- there are no «Ver»/«Detalle» buttons;
- phones are plain text;
- rows are vertically centred, with compact dropdowns.

The code is `useRowLink` and `.row-link` in `frontend/src/app/shared/components/row-link.{ts,css}`, plus the alignment rules scoped to `.position-links-table` and `.position-stage-select` in `positions.css`. The remaining tables still use the older mix: «Abrir» and «Editar» buttons, an eye icon that opens a dialog, top-aligned cells and tall controls.

## Confirmed product decisions

| Table                              | Row click                                     | Other changes                                                                                                                                                                                                             |
| ---------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Candidatos** (`/app/candidates`) | Opens the candidate page                      | «Abrir» button removed. The name becomes the row's keyboard link. The phone becomes plain text. The selection checkbox cell never navigates.                                                                              |
| **Posiciones** (`/app/positions`)  | Opens the position page                       | The title link keeps its role as the keyboard link, styled as plain text.                                                                                                                                                 |
| **Presets** (`/app/admin/presets`) | Opens the edit page (`/:id/edit`)             | «Editar» button and the eye/view dialog removed. Each preset shows its **filters on a full-width line directly below its values**. «Eliminar» stays. Only `presets.manage` holders reach this list, so everyone can edit. |
| **Catálogos** (`/app/catalogs`)    | **No row navigation**: styling only           | Rows are edited in place, so there is nothing to open. Vertical alignment and compact row controls.                                                                                                                       |
| **Usuarios** (`/app/admin/users`)  | **No row navigation**: styling only (for now) | Vertical alignment and a compact role dropdown. A user detail page, which would make rows clickable, is a future ticket.                                                                                                  |
| **Candidate page header**          | —                                             | The phone becomes plain text (no `tel:` link anywhere in the application).                                                                                                                                                |

**Assumption, to confirm during review.** "A row of filters below the title and the rest of values" is implemented as a second, full-width table row under each preset. It spans all columns and shows the preset's criteria through the existing `SearchCriteriaSummary`, in a compact style. Both rows belong to the same clickable group.

## Functional requirements

### 1. Shared table styling

- Move the KTL-30 alignment rules out of `positions.css` into a shared stylesheet, for example `frontend/src/app/shared/components/data-table.css`, with a `.data-table` class:
  - cells vertically centred;
  - dropdowns and row buttons at the 28px compact height of `.button.small`;
  - the row-link hover and cursor styles.
- Apply `.data-table` to every table listed above, and to the three KTL-30 tables (search results, position candidates, candidate positions). Remove `.position-links-table`.
- Use only the Kepler tokens (`docs/CORPORATE_IDENTITY_Kepler.md`), with no inline styles. The single 768px breakpoint and the "wide tables scroll inside their own container" rule are unchanged.
- Leave the rest of the app's dropdowns and buttons, outside these tables, untouched.

### 2. Clickable rows (Candidatos, Posiciones, Presets)

- Use the existing `useRowLink` with the KTL-30 rules:
  - links, buttons, inputs, selects, labels and text selections never navigate;
  - Ctrl/⌘-click and middle-click open a new tab;
  - the name or title stays a real `Link` with `.row-link`.
- **Candidatos**:
  - The whole selection cell (checkbox column) is excluded, so a near-miss on the checkbox does not open the candidate. Extend `useRowLink` with an opt-out attribute, for example `data-row-link-ignore`.
  - Sort buttons live in `<thead>` and are unaffected.
- **Presets**:
  - Each preset renders as one `<tbody>` group with two rows: the values row (name, updated, last used, «Eliminar») and the filters row (`SearchCriteriaSummary`, spanning every column).
  - Hover highlights the whole group, and a click on either row opens the edit page.
  - An empty preset shows the existing «Sin filtros aplicados.».
- Remove the per-row «Abrir» (Candidatos) and «Editar» (Presets) buttons, the eye button, and the preset view dialog, along with their i18n keys and test ids (`preset-view`, `preset-view-*`, `preset-edit`).

### 3. Phones

- Plain text in the candidates table and the candidate page header.
- Delete `telHref` and its unit test once nothing uses it. `mailtoHref` stays: e-mails remain links.

## Data and API

**No backend change.** The preset list endpoint (`GET /api/search-presets`) already returns each preset's normalized filters, which the removed dialog used, so the filters row needs no new request. No endpoint, table, migration, permission or grant changes.

## Files likely touched

| Area       | Files                                                                                                                                                                                                                                                                 |
| ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Shared     | `shared/components/row-link.ts` (opt-out attribute), new `shared/components/data-table.css`                                                                                                                                                                           |
| Candidatos | `features/candidates/components/candidate-table.tsx`, `features/candidates/pages/candidate-detail-page.tsx` (header phone), `features/candidates/contact-links.ts`                                                                                                    |
| Posiciones | `features/positions/position-list-page.tsx`, `positions.css` (rules moved out), `components/position-candidates-panel.tsx`, `features/candidates/components/candidate-positions-panel.tsx`, `features/search/components/search-results.tsx` (switch to `.data-table`) |
| Presets    | `features/admin/presets/preset-list-page.tsx` and `.css`                                                                                                                                                                                                              |
| Catálogos  | `features/catalogs/pages/catalog-management-page.tsx` and its CSS                                                                                                                                                                                                     |
| Usuarios   | `features/admin/users/admin-users-page.tsx`                                                                                                                                                                                                                           |
| Copy       | `assets/i18n/es.json`: remove `candidates.list.open`, `presets.action.view`, `presets.action.edit` if unused, and the preset view dialog keys. No new copy is expected beyond an accessible name for the filters row, if needed.                                      |

## Acceptance criteria

1. **Candidatos row:** given the candidate list, when the user clicks a row outside its e-mail link and checkbox, then the candidate page opens. There is no «Abrir» button, and the phone is plain text.
2. **Selection is safe:** given a manager on the candidate list, when they click the checkbox or anywhere in its cell, then only the selection changes and no navigation happens.
3. **Posiciones row:** given the positions list, when a row is clicked, then the position page opens, and Ctrl-click opens it in a new tab.
4. **Presets row:** given the presets list, when a preset's values row or filters row is clicked, then its edit page opens. «Eliminar» still asks for confirmation and does not navigate. There is no eye button, view dialog or «Editar» button.
5. **Preset filters inline:** given presets with and without criteria, when the list renders, then each preset shows its criteria summary on a full-width line below its values, or «Sin filtros aplicados.» when it has none.
6. **Catálogos and Usuarios:** given either screen, when a row is clicked, then nothing navigates. Their existing controls (inline edit, move, activate, role dropdown, deactivate) work unchanged and render at the compact height, vertically centred.
7. **Keyboard:** given any clickable-row table, when the user tabs through it, then each row's name or title is a focusable link to the same destination, with a visible focus ring. No row is a tab stop by itself.
8. **Consistent styling:** given every listed table, the KTL-30 tables included, when rendered, then values are vertically centred and dropdowns and row buttons share the compact height. No other screen's controls change size.
9. **No `tel:` links:** given the candidate table and the candidate page header, when a phone is shown, then it is plain text.
10. **Narrow viewport:** given a 390px viewport, when each listed table is shown, then the page does not scroll horizontally; the table scrolls inside its own container.

## Test coverage

- **Unit (`frontend/tests/unit`):**
  - `useRowLink` opt-out attribute;
  - `candidate-list-page.spec.tsx`: row navigation, the checkbox cell not navigating, no «Abrir», plain phone;
  - the positions list row;
  - `preset-list-page.spec.tsx`: the dialog tests replaced by filters-row and row-navigation tests, «Eliminar» not navigating;
  - Catálogos and Usuarios rows not navigating;
  - `candidate-detail-page.spec.tsx`: plain header phone;
  - `contact-links.spec.ts`: the `telHref` case removed.
- **E2E (`frontend/tests/e2e`):**
  - update `advanced-search-presets.spec.ts`, which clicks `preset-view` and `preset-edit` today, to open a preset by its row and check the inline filters;
  - add a row-navigation step to the candidate-list and positions specs;
  - run the 390px checks;
  - selectors use role, label or `data-testid`, with no Spanish text.
- **Security:** no boundary changes. The existing route guards already cover every destination, so there is no new security spec. Run the security gates anyway.
- **Gates:** `npm test`, `npm run lint`, `npm run format:check`, the targeted `npm run e2e`, and `npm run build:all`.

## Documentation and specification impact

- `openspec/specs/saved-search-presets`: modify «Preset administration section». The view control and dialog are replaced by a row that opens the edit page and an inline filters line. The scenario «no criteria are rendered in the rows» is reversed.
- A new small capability for the shared table interaction (for example `data-tables`): clickable-row rules, the keyboard link, the controls exclusion, modifier keys, plain phones, compact aligned controls, and which tables have no row navigation (Catálogos, Usuarios) and why.
- `docs/ktl-31/release-notes.md`, and a short Spanish section in `README.md`.

## Non-functional requirements

- **Accessibility:** a row is never the only way to reach a destination. Focus rings stay visible, and no `tabindex` is added to rows.
- **Personal data:** unchanged. No new data is shown: preset filters were already loaded, and phones and e-mails were already visible.
- **Performance:** the presets list renders one summary per visible row, and the list is already paginated. There are no new requests.

## Out of scope

- Merging separate view and edit pages (presets, positions), and a user detail page. Each is a future ticket.
- The Roles screen, which uses cards, not a table.
- Sorting, filtering or pagination changes, and new columns.

## Amendments

**2026-09-25, product review during implementation.** These decisions supersede the Catálogos row in _Confirmed product decisions_ and the Catálogos part of acceptance criterion 6. See design D7 and D8 in the archived change `ktl-31-consistent-tables`.

- **Catálogos:** a click on a value row, outside its controls, opens that row's inline editor. «Editar» is removed, and the value's name is the keyboard button to the same action. It still never navigates. «Subir»/«Bajar» become up/down arrow icon buttons with accessible names.
- **Presets:** the criteria line shows one wrapping row of chips like the search form's chips, and the table sits in a panel like the other lists.
- **Usuarios** is unchanged: styling only, no row action.
