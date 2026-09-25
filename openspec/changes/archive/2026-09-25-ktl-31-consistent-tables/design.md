## Context

See `proposal.md` for motivation and the delta specs for normative behavior. The pieces this
change builds on:

- **KTL-30 row behaviour.** `useRowLink` in `frontend/src/app/shared/components/row-link.ts`
  returns click handlers that navigate unless the target is inside a link or control. It opens a
  new tab on Ctrl/⌘-click and middle-click. `row-link.css` styles `.row-link-row` (cursor,
  hover) and `.row-link` (the plain-looking name link).
- **KTL-30 alignment.** The vertical centring and compact select live in `positions.css`, scoped
  to `.position-links-table` and `.position-stage-select`. They reach the candidate page only
  because that panel imports `positions.css`.
- **The tables to change:**
  - `candidate-table.tsx`: a checkbox column, sort buttons, an «Abrir» link button and a `tel:`
    phone link.
  - `position-list-page.tsx`: a title link, no actions.
  - `preset-list-page.tsx`: an eye button with a dialog, an «Editar» link button and «Eliminar».
  - `catalog-management-page.tsx`: inline edit, move and activate buttons at the default height.
  - `admin-users-page.tsx`: a role `<select>` and a default-height activate button.
- **Styling baseline.** Global styles give cells `vertical-align: top`, text controls a 34px
  `min-height` and buttons a 32px `min-height`, while `.button.small` is 28px.

## Goals / Non-Goals

**Goals:**

- One shared style and one row behaviour for every record table, so that no page keeps its own
  copy.
- Remove every button whose only purpose is to open the row's record.
- Keep keyboard and screen-reader access equivalent to the mouse path.

**Non-Goals:**

- Merging view and edit pages, or a user detail page.
- Any change outside record tables: forms, dialogs and toolbars keep their control sizes.
- A generic table component. Each page keeps its own markup and only adopts the class and the
  hook.

## Decisions

### D1. A shared `.data-table` class in `shared/components/data-table.css`

It contains:

- `.data-table td { vertical-align: middle; }`;
- compact controls inside cells: `select`, text `input` and `.button` at 28px `min-height`,
  with the `.button.small` padding and font size;
- a `.data-table .muted` line spacing that stays readable under the name.

Each table adds `className="data-table"` and imports the stylesheet. `positions.css` loses
`.position-links-table` and the compact select rules. `.position-stage-select` keeps only its
`min-width`. `row-link.css` keeps the row cursor, hover and `.row-link` styles, because they
belong to the behaviour, not to the layout.

_Alternative considered:_ changing the global `td` and control rules in `styles.css`. It was
rejected because forms, dialogs and the criteria editor use the same controls, and the spec
requires them unchanged.

### D2. Excluding a cell with `data-row-link-ignore`

`useRowLink` adds `[data-row-link-ignore]` to the selector it already checks with
`closest(...)`. The candidate list marks its selection `<td>` with it, so a near-miss on the
checkbox never navigates. Nothing else changes in the hook. The KTL-30 rules already cover
links, controls, labels, text selection and modifier keys.

### D3. Presets render one `<tbody>` per preset

Each preset becomes `<tbody className="row-link-group" onClick onAuxClick>` with two rows:

1. **The values row:** the name as a `.row-link` to `/app/admin/presets/:id/edit`, the update
   time, the last-used time and «Eliminar».
2. **The filters row:** one `<td colSpan>` holding the existing `SearchCriteriaSummary` for
   the preset's filters.

Multiple `<tbody>` elements are valid HTML, and they give the group a single hover target
(`.row-link-group:hover td`) and a single click handler. The border between the two rows is
removed, so the group reads as one record. «Eliminar» is a button, so the hook ignores it and
its confirmation flow is unchanged.

Removed from the page:

- the eye button, `EyeIcon`, the `viewing` state and the dialog;
- the «Editar» link, and the `presets.action.view`/`presets.action.edit` and dialog keys that no
  other file uses.

The summary uses the filters already present in each listed preset, so there is no extra
request.

_Alternatives considered:_

- The summary inside the name cell, which would make the name column very wide and the other
  columns misaligned.
- Hover styles per row, which would highlight only half of the preset.

### D4. Phones

`candidate-table.tsx` and `candidate-detail-page.tsx` render the phone as text. `telHref` is
deleted from `contact-links.ts` together with its unit test case, because nothing uses it.
`mailtoHref` stays.

### D5. Table-by-table adoption

| Table                                                    | Class         | Row hook                           | Removed                        |
| -------------------------------------------------------- | ------------- | ---------------------------------- | ------------------------------ |
| Candidatos (`candidate-table.tsx`)                       | `.data-table` | yes; the selection cell is ignored | «Abrir», the `tel:` link       |
| Posiciones (`position-list-page.tsx`)                    | `.data-table` | yes; the title is the `.row-link`  | —                              |
| Presets (`preset-list-page.tsx`)                         | `.data-table` | yes, per `<tbody>`                 | eye, dialog, «Editar»          |
| Catálogos (`catalog-management-page.tsx`)                | `.data-table` | inline edit (D7), not navigation   | «Editar», «Subir»/«Bajar» text |
| Usuarios (`admin-users-page.tsx`)                        | `.data-table` | **no**                             | —                              |
| Search results, position candidates, candidate positions | `.data-table` | already                            | `.position-links-table` class  |

Catálogos and Usuarios deliberately get no navigation hook: a row there has no destination (spec:
tables without a record page have no row navigation). Catálogos rows act in place instead (D7).

### D7. Catalog rows start the inline edit (review follow-up)

User review asked for «Editar» to go and for the row click to start the edit. `row-link.ts` now
exports `isRowClick(event)`, the controls/selection check `useRowLink` already used, so a row that
acts in place applies the same exclusions without navigating. The catalog row calls `startEdit`
on a primary click only while no row is being edited. The value's name becomes a
`button.row-link` (plain text look, focus ring, `aria-label` «Editar {name}») carrying the former
`catalog-edit` test id, so keyboard users keep an equivalent. Locked rows disable it, matching
the other row actions. «Subir»/«Bajar» become 28px square `.icon-button` arrows with
«Subir {name}»/«Bajar {name}» accessible names and the short verb as a tooltip.

### D8. Preset criteria as one line of chips (review follow-up)

`SearchCriteriaSummary` gains `layout="inline"`: every group on one wrapping flex line, a small
label then its chips, with no headings. The column layout stays the default for the search page
and the position page. Review also found that the summary's `.chip` had no CSS at all, so chips
rendered as plain text everywhere; `.filters-summary .chip` now draws the orange pill of the
search form's picker chips, which fixes every host. The preset table moves into a `.panel` so it
has the same background and frame as the other lists.

### D6. Tests

**Unit:**

- the hook's opt-out;
- the candidate list: row navigation, the checkbox cell ignored, no «Abrir», plain phone;
- the positions list row;
- the preset list: the dialog tests replaced by a filters-line test, a row-opens-edit test and
  a delete-does-not-navigate test;
- Catálogos and Usuarios rows not navigating;
- the plain phone on the candidate page;
- `contact-links.spec.ts` trimmed.

**E2E:**

- `advanced-search-presets.spec.ts` opens a preset through its name link, checks the inline
  summary, and no longer uses `preset-view` or `preset-edit`;
- a row-click step in the candidate list and positions specs;
- the existing 390px checks re-run.

The styling itself is only checked indirectly, through the existing layout specs. jsdom has no
layout.

## Risks / Trade-offs

- **[Compact buttons inside `.data-table` also shrink primary row actions such as «Añadir» and
  «Eliminar»]** → That is intended: the spec asks for one control height per row. The buttons
  keep their colour and label.
- **[Catalog inline-edit inputs become 28px]** → They are in-row controls, so the rule applies.
  Labels and focus rings are unchanged.
- **[Removing the preset dialog also removes the only on-screen creation date]** → Accepted.
  The list keeps update and last-used times, and the audit trail keeps creation. A column can be
  added later if users miss it.
- **[Long preset criteria make tall rows]** → The summary wraps inside the full-width line, and
  the list is paginated.
- **[Stale selectors in other e2e specs]** → A search for `preset-view`, `preset-edit`,
  `candidates.list.open` and `tel:` is part of the task list.

## Migration Plan

This is a frontend-only deployment. Rollback is a normal revert of the SPA build.
