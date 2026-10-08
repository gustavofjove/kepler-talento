## Context

The catalog slice (KTL-6) is API-owned. `CAT_CatalogItems` holds every family, the API exposes
list, create, update, reorder and activate endpoints under `/api/catalogs/{family}`, and the SPA
loads every family once into `CatalogService` (a signal-backed singleton read through
`useCatalogs()`). Catalog chips are drawn in two places, both orange through hard-coded
`--fj-orange-*` tokens:

- `CatalogValuePicker` (`.catalog-picker-chip`): hosted by `candidate-relation-section.tsx`
  (candidate «Competencias») and `criteria-group.tsx` (search, preset and position criteria).
  Picker items carry the value's **Spanish name**, not its id.
- `SearchCriteriaSummary` (`.filters-summary .chip`): values are pre-formatted strings
  (`«Inglés · al menos B2»`) built by `buildSummaryGroups`.

`ktl_runtime` holds table-level `SELECT, INSERT, UPDATE` on `CAT_CatalogItems` (migration
`AddCatalogItems`). See proposal.md for the motivation and the specs for the requirements.

## Goals / Non-Goals

**Goals:**

- One source of truth for the palette tokens on each side (`CatalogColors` in the domain,
  `CATALOG_COLORS` in the SPA), kept in step by a test.
- An additive, backward-compatible API change, deployable with the usual migrator-then-API order.
- Chip colouring through CSS custom properties keyed by a data attribute, with no inline styles.

**Non-Goals:**

- Resolving chips by catalog id. Picker items and saved criteria are name-based today, and changing
  that is a separate refactor.
- A generic theming system, or user-defined colours.

## Decisions

### D1. Store a token, not a colour value

`Color` is a `varchar(20)` holding one of nine tokens, enforced by `CK_CAT_CatalogItems_Color`,
whose SQL is built from `CatalogColors.All` exactly like `FamilyCheckConstraint`. The hex values
live only in `styles.css`.
_Alternative:_ storing hex values. Rejected because it lets arbitrary colours in, defeats the
contrast guarantee, and couples data to the visual design. Retuning a pastel later is a CSS-only
change.

### D2. Column on every row, with family support enforced by the API

Every row carries `Color` (`NOT NULL DEFAULT 'orange'`), so the item shape stays uniform across
families. `CatalogColors.Supports(family)` (skill, language, program, tag) gates non-default
values in the validators: `catalog.color.not_supported` when a non-colourable family receives
anything but `orange`, and `catalog.color.invalid` for unknown tokens.
_Alternative:_ a nullable column, null for non-colourable families. Rejected because it adds a
three-state field and null handling on both sides for no user-visible gain. A database-level
family/colour check constraint was also considered. It was rejected because the family set that
supports colour is a business rule that may grow, and changing it should not need a migration.
The API is the boundary for that rule.

### D3. Optional `Color` on create and update, where null means "keep" or "default"

`CreateCatalogItemRequest` and `UpdateCatalogItemRequest` get a trailing `string? Color`, and so
do their commands. On create, null means `orange`. On update, null means keep the current colour,
so a client that predates KTL-41, or the reorder and activate flows, can never reset it. The
domain gets `CatalogItem.Recolor(color, updatedAtUtc)`, a no-op when unchanged, mirroring
`SetActive`, plus an optional `color` constructor argument that defaults to
`CatalogColors.Default`, so `DatabaseInitializer` and test fixtures compile unchanged. The update
handler calls `Recolor` after `ExpectVersion`, so a stale version still yields 409, and the change
is saved under the existing `catalog.updated` audit event.

### D4. Authorization unchanged and ahead of validation

The endpoints already throw `ForbiddenException` before dispatching to the MediatR pipeline, where
FluentValidation runs, and the handlers repeat `CatalogGuards.RequireManage`. The new field
inherits this ordering, so an unauthorized caller sending `magenta` gets 403, never 400. No new
permission is needed. Integration tests pin this ordering.

### D5. Migration, grants and rollout

`dotnet ef migrations add AddCatalogItemColor` adds the column with
`defaultValueSql: "'orange'"` (EF `HasDefaultValue("orange")`) and the check constraint. Existing
rows are backfilled by the default in the same `ALTER TABLE`. No grant statement is needed: the
runtime grant is table-level and already covers `UPDATE` of the new column. The schema test
asserts that `ktl_runtime` can update `Color` and still lacks `DELETE`.
Rollout: the migrator runs first, as always. An API instance still running the previous build
inserts without the column, and the database default applies, so the two builds are compatible
during the switch. Rollback: the migration's `Down` drops the constraint and the column. Only
chosen colours are lost, and the data is reference data, not personal data.

### D6. SPA: resolve colours in the service, render with a data attribute

- `catalog.models.ts` adds `CatalogColor` and `CatalogItem.color`. `catalog-color.logic.ts` (new,
  under `features/catalogs/`) holds `CATALOG_COLORS` in grid order, `DEFAULT_CATALOG_COLOR`,
  `isColorableFamily` and `colorNameKey`. Keeping them in a `.logic.ts` keeps fast refresh working.
- `CatalogService.colorOf(family, name)` looks the value up among active **and** inactive items and
  falls back to `orange`. The service caches a per-family `Map<nameEs, CatalogColor>` that is
  rebuilt only when the catalog state object changes. That keeps the lookup O(1) per chip, with no
  extra requests. Components reach it through `useCatalogs()`, never `useServices()`, so chips
  re-render when an admin recolours a value.
- `CatalogValuePicker` stays presentational. It gets an optional
  `colorOf?: (item: PickerItem) => CatalogColor | undefined` and sets `data-catalog-color` on each
  `Tag`. The two hosts pass `(item) => catalogs.colorOf(valueFamily, item.value)`. The same
  resolver draws an `aria-hidden` circle before each offered value in the add list, so an option
  keeps its plain-text accessible name (which the e2e helpers select by). Level options get none.
- `buildSummaryGroups` changes `values: string[]` to `values: { text: string; color?: CatalogColor }[]`.
  It takes a `colorOf` resolver for the catalog groups only, so non-catalog groups carry no colour.
  `SearchCriteriaSummary` gets it from `useCatalogs()`.
- `styles.css` defines one rule per token, `[data-catalog-color='blue'] { --catalog-chip-bg: …;
--catalog-chip-border: …; --catalog-chip-fg: …; }`. `.catalog-picker-chip`,
  `.filters-summary .chip` and the swatch read `var(--catalog-chip-bg, var(--fj-orange-pale))` and
  so on, so a missing attribute renders today's orange.
  _Alternative:_ inline `style={{ background }}`. Rejected by the frontend conventions, because
  inline styles bypass the Kepler tokens.

### D7. Colour dialog on react-aria-components

`CatalogColorSwatch` renders a read-only `span role="img"`; `CatalogColorPickerSwatch` renders a
native `button` (keeping `title`, `name`-free `id` for a host `<label htmlFor>`, and the test ids)
that opens `CatalogColorDialog` as controlled state. The dialog uses `ModalOverlay` + `Modal` +
`Dialog` + a single-select `ListBox` with `layout="grid"` from `react-aria-components`, which is
already a runtime dependency used by the picker, styled with `shared/components/modal.css`. This
gives focus trapping, Escape handling, focus return and two-dimensional arrow-key navigation
without hand-written handlers, and adds **no new dependency**.

A listbox, not a radio group: in a radio group the arrow keys move **and select**, so "choosing
closes the dialog" would close it on the first arrow press. In the listbox the arrows only move
focus; Enter, Space or a click chooses. A listbox clears its selection on Escape and when the
selected option is pressed again; both arrive as an empty selection, which the dialog treats as
"keep the current colour" and closes without calling `onChange`. The management page keeps
`newColor` and `editColor` in `useState` next to the existing draft fields, sends `color` on update
only when it changed, and `CatalogService.create`/`update` pass it through `catalog.api.ts`.
_Alternatives:_ the existing `confirm-dialog` service (a yes/no dialog with no content slot), and
`RadioGroup` (rejected for the arrow-key behaviour above).

### D8. Palette values and contrast

The orange token maps to `--fj-orange-pale` / `--fj-orange-soft` / `--fj-orange-dark`. The other
eight hues get a pale background (lightness ≈ 93–95%), a soft border and a dark text tone
(≈ 25–30% lightness) of the same hue, measured to ≥ 4.5:1. The measured ratios are recorded in
`docs/CORPORATE_IDENTITY_Kepler.md`. Grey is kept visibly cooler than the KTL-38 neutral status
tone, and catalog chips keep their 1px border, which status badges do not have, so the two are not
confused.

### D9. Catalog administration: explicit row actions and a «Nuevo» form

Requested during implementation. KTL-31 made a click anywhere on a catalog row open its inline
editor, with the name as a keyboard button. With a colour circle now in the row, and with other
tables using a row click to open a detail page, that reads as navigation. So the row is inert:
`CatalogManagementPage` drops `isRowClick` and `row-link-row`, the name becomes plain text, and
«Acciones» gains a pencil `button ghost icon-button` that keeps the `catalog-edit` test id and the
«Editar <value>» accessible name the specs already used. «Desactivar»/«Activar» become one icon
toggle (`catalog-toggle-active`, `data-active`) keeping `button danger`/`button secondary` tones,
with «Desactivar <value>»/«Activar <value>» accessible names and the plain verb as tooltip.
`PencilIcon`, `BanIcon` and `RestoreIcon` join `shared/components/icons.tsx`, in the existing 16px
stroke style.

The add form is rendered only while `creating` is true. «Nuevo» (`catalog-new`, `aria-expanded`,
`aria-controls`) sits in a `.catalog-family-bar` beside the selector. The bar does not wrap and
lets the selector shrink, so the button stays to its right even at 360px. «Cancelar» and a
successful «Añadir» both call `closeCreate`, which hides and resets the form. The edit, «Nuevo»
and the form are mutually exclusive: each disables the others' entry points, as row edits already
locked one another. The form's second grid cell is a `.catalog-create-pair` flex row: the colour
field (`flex: none`, circle centred on the 34px input line) then the code field (`flex: 1`). The
styles live in a new `catalog-management-page.css`, not in `styles.css`.

_Alternative:_ keep the row click and add the pencil as a second way in. Rejected because the
request is precisely that the row stop behaving like a link.

### Test strategy

- **xUnit unit:** `CatalogColors` (`IsKnown`, `Supports`), `CatalogItem.Recolor`, and the
  create/update handlers with hand-written doubles (default, keep on null, invalid, not supported,
  manage guard).
- **xUnit integration (Testcontainers):** API round trip of `color`, 400 codes, 409 on a stale
  version, 403 before validation for unauthenticated and read-only callers (the catalog endpoints
  answer an unauthenticated caller with 403, as before KTL-41) with no audit row, a `catalog.updated` audit row on success,
  migration backfill to `orange`, the check constraint rejecting an unknown value, and
  `ktl_runtime` update allowed with no delete.
- **Vitest:** service `create`/`update` payloads and `colorOf` (inactive, fallback, cache refresh);
  the management page (column only for colourable families, tooltip and accessible name, edit,
  cancel and create reset); the dialog keyboard flow; `data-catalog-color` on picker chips and on
  catalog summary chips only; a palette parity spec that reads
  `backend/Domain/Catalogs/CatalogColors.cs` through `tests/repo-root.ts` and compares its tokens
  with `CATALOG_COLORS`.
- **Playwright:** `catalogs-crud.spec.ts` recolours a `Date.now()`-marked tag and asserts
  `data-catalog-color` on the candidate's «Competencias» chip, without Spanish-text selectors.
- **D9:** `catalog-management-rows.spec.tsx` covers the inert row, the pencil by pointer and
  keyboard, the icon toggle with its confirmation, the locks, and «Nuevo» (hidden by default,
  focus, cancel, close after add). The e2e specs that create catalog values open the form with
  `catalog-new` and use `catalog-edit` and `catalog-toggle-active` instead of a row click or
  button text.

## Risks / Trade-offs

- [Name-based colour lookup misses a renamed value in old saved criteria] → It falls back to
  orange, which the specs require. Search itself is unaffected.
- [Nine pastels look alike for colour-blind users] → Colour is never the only cue: chips keep their
  text, and the dialog shows names and a check mark.
- [Frontend and backend palettes drift] → The parity spec fails the frontend suite when the token
  lists differ.
- [A custom-property rule left out of a token] → The fallback renders orange, and the dialog spec
  asserts every token has a swatch name key.
- [Grey chip mistaken for a neutral status chip] → It uses a different hue and has a border (D8).
- [Users used to KTL-31's row click find it does nothing] → The pencil is the first action in
  every row, with a tooltip; the release note and README describe the change.
- [Icon-only actions are less discoverable] → Each has a tooltip and an accessible name that names
  the value; deactivation keeps its confirmation dialog.
