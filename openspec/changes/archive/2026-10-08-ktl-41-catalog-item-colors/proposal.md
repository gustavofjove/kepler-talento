## Why

Every catalog chip (skills, languages, programs, tags) is the same pale orange, so a recruiter
scanning a candidate's «Competencias» panel, a search's criteria or a position's requirements has
to read each chip to tell the families and values apart. Administrators want to give values their
own pastel colour, chosen from a small, clearly distinct palette, so chips can be recognised at a
glance wherever they appear (brief: `openspec/KTL-41.md`).

## What Changes

- Catalog items gain a **colour**, one token from a closed palette of nine pastel colours:
  `orange` (default, today's look), `yellow`, `green`, `teal`, `blue`, `indigo`, `violet`, `pink`,
  `grey`. Only the four chip families (`skill`, `language`, `program`, `tag`) can take a colour
  other than the default. Level families, education types/statuses and sectors cannot, because
  nothing renders them as chips.
- `CAT_CatalogItems` gets a `Color` column (`NOT NULL DEFAULT 'orange'`, check-constrained to the
  palette) through an EF Core migration. Existing rows become `orange`, so nothing changes visually
  until an administrator picks a colour.
- The catalog API returns `color` on every item, and the create and update requests accept an
  optional `color`. Omitting it keeps the current colour on update and uses the default on create,
  so existing clients keep working. Unknown colours and colours on non-colourable families are
  rejected with stable codes (`catalog.color.invalid`, `catalog.color.not_supported`). A colour
  change goes through the existing optimistic-concurrency check and is audited as
  `catalog.updated`.
- Catalog administration («Catálogos»): for colourable families, a «Color» column shows a circle
  in the item's colour, with the colour name as its tooltip. In the inline row editor and the
  create form the circle opens an «Elegir color» dialog with the nine named swatches. The choice is
  saved with the row's «Guardar» or the form's «Añadir».
- Chips take their value's colour in the shared catalog value picker (candidate «Competencias»,
  search criteria, position requirements) and in the read-only criteria summary (search page,
  criteria dialog, preset list, position page). Non-catalog summary chips stay orange. The values the
  picker offers for adding show a small circle in their colour to the left of their name.
- Catalog administration actions, requested during implementation: the add form is hidden behind
  a «Nuevo» button to the right of the family selector, and lays out the name in one half and the
  colour plus code in the other. Rows no longer start the inline edit on click (reverting the KTL-31
  behaviour for Catálogos): «Acciones» gains a pencil edit button, and «Desactivar»/«Activar»
  become icon buttons with the value in their accessible names.
- Unchanged: status chips (KTL-38), permissions, grants, and search semantics.

Actors: catalog administrators (`catalogs.manage`) set colours. Every reader of candidate
competencies, search criteria, presets and positions sees them.

Edge cases: inactive values keep their colour. A criterion whose name no longer resolves to a
catalog item, such as an old preset, falls back to orange. Several values may share a colour.
Cancelling the row edit discards a picked colour.

Success criteria: the brief's 14 acceptance criteria hold. Each palette colour measures ≥ 4.5:1
text-to-background contrast. `npm run lint`, `npm run format:check`, `npm test`,
`npm run test:backend` and the catalog e2e spec pass.

Personal data and security: catalog values are business reference data, not personal data, and
the colour is a presentation attribute of a catalog value. No personal data, storage path or role
is touched. Authorization stays fail-closed: reading needs `catalogs.read`, and creating or
updating, including the colour, needs `catalogs.manage`, checked in the endpoint before validation
and again in the handler. `ktl_runtime` keeps its existing table-level `SELECT, INSERT, UPDATE` on
`CAT_CatalogItems` and still has no `DELETE`, so principles 1 and 3 are upheld without a new grant.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `business-catalogs`: the item shape gains a colour. New requirement for the closed palette, the
  default, the colourable families, validation, and how a colour is set and displayed in catalog
  administration.
- `catalog-value-picker`: chips are drawn in their value's catalog colour.
- `saved-search-presets`: catalog chips in the shared read-only criteria summary are drawn in their
  value's colour, and other summary chips keep the default.
- `data-tables`: «Catalog rows open their inline editor» is replaced by «Catalog values are edited
  from their row actions» (edit, reorder and activation icon buttons; no row-click edit; the add
  form opens from «Nuevo»).

## Impact

- Backend: `Domain/Catalogs` (new `CatalogColors`, `CatalogItem.Color`/`Recolor`),
  `Application/Features/Catalogs` (`CatalogContract`, `CreateCatalogItem`, `UpdateCatalogItem`),
  `Infrastructure/Persistence/Configurations/CatalogItemConfiguration.cs`, new migration
  `AddCatalogItemColor`, `Web/Features/Catalogs/CatalogEndpoints.cs`.
- Frontend: `features/catalogs` (model, API, service `colorOf`, new colour swatch and dialog
  components, management page, value picker), `candidates/components/candidate-relation-section.tsx`,
  `search/components/criteria-group.tsx`, `search-criteria.logic.ts`,
  `search-criteria-summary.tsx`, `styles.css` palette tokens, `es.json`/`en.json`, the shared
  icons (pencil, deactivate, restore), and a page stylesheet for the toolbar and add form.
- E2E: `catalogs-crud.spec.ts` and `candidate-tags-notes.spec.ts` open the add form with «Nuevo»
  and edit or toggle a value through test ids instead of a row click or button text.
- API contract: additive `color` field on catalog responses and optional `color` on create and
  update requests. Not breaking.
- Dependencies: none new. The dialog uses `react-aria-components`, which is already installed.
- Tests: xUnit domain, handler, API and schema tests. Vitest service, management page, dialog,
  picker and summary specs. Playwright `catalogs-crud.spec.ts`.
- Docs: `docs/CORPORATE_IDENTITY_Kepler.md` (palette and contrast), `docs/ktl-41/` release note,
  and the `README.md` «Catálogos» section.
