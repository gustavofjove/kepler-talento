## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-41` from an up-to-date `main`

## 1. Domain and persistence (business-catalogs: Catalog value colour; Catalog families and item shape)

- [x] 1.1 Add `backend/Domain/Catalogs/CatalogColors.cs`: the nine tokens in grid order, `Default`,
      `All`, `IsKnown(color)` and `Supports(family)` for skill, language, program and tag (design D1, D2)
- [x] 1.2 Add `Color` to `CatalogItem` (defaults to `CatalogColors.Default`), an optional `color`
      constructor argument and `Recolor(color, updatedAtUtc)`, which is a no-op when unchanged (design D3)
- [x] 1.3 Map `Color` in `CatalogItemConfiguration.cs`: max length 20, required,
      `HasDefaultValue("orange")`, and `CK_CAT_CatalogItems_Color` built from `CatalogColors.All` (design D1, D5)
- [x] 1.4 Generate the migration with `dotnet ef migrations add AddCatalogItemColor --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`.
      Inspect `Up` (column with default, check constraint) and `Down` (drops both). Confirm that no
      grant statement is needed (design D5)
- [x] 1.5 Add `CatalogItemColorTests` under `backend/Tests/UnitTests/Domain/`: default colour,
      `Recolor` changes `UpdatedAtUtc` only on change, `IsKnown`, `Supports`

## 2. Application and API contract (business-catalogs: Catalog value colour)

- [x] 2.1 In `CatalogContract.cs`, add `Color` to `CatalogItemResponse`, add the error codes
      `catalog.color.invalid` / `catalog.color.not_supported` with the Spanish messages «El color no
      es válido.» / «Esta familia de catálogo no admite color.», and add the `MustBeAKnownColor` and
      family-support validator extensions
- [x] 2.2 Add `string? Color` to `CreateCatalogItemCommand`. The validator applies the rules when
      it is not null, and the handler passes the colour or the default to the constructor
- [x] 2.3 Add `string? Color` to `UpdateCatalogItemCommand`. The validator applies the rules, and
      the handler calls `Recolor` after `ExpectVersion` only when it is not null (design D3)
- [x] 2.4 Add `Color` to `CreateCatalogItemRequest` and `UpdateCatalogItemRequest` in
      `CatalogEndpoints.cs` and pass it to the commands. Leave the authorization checks before
      dispatch untouched (design D4)
- [x] 2.5 Review and update `backend/Tests/UnitTests/Features/CatalogHandlerTests.cs` and any
      `CatalogItem` construction in test fixtures (`SearchParityFixture`, `CandidateHandlerTests`,
      `CatalogSeedDataTests`). Add handler cases: create default/explicit, update keeps on null,
      invalid, not supported, manage guard

## 3. Backend integration and security evidence (business-catalogs: Catalog value colour; Catalog authorization fails closed)

- [x] 3.1 Extend `backend/Tests/IntegrationTests/CatalogApiTests.cs`: `color` round trip on POST,
      PUT and GET, 400 `catalog.color.invalid` and `catalog.color.not_supported`, update without
      `color` keeps it, 409 on a stale `Version`, and a `catalog.updated` audit row on a colour change
- [x] 3.2 Add fail-closed cases: unauthenticated and `catalogs.read`-only → 403, both with
      an invalid colour (proving authorization runs before validation). Assert no row is changed and
      no audit event is written
- [x] 3.3 Add schema assertions: migration backfill gives `orange` on pre-existing rows,
      `CK_CAT_CatalogItems_Color` rejects an unknown token, `ktl_runtime` can `UPDATE` `Color` and
      still cannot `DELETE` from `CAT_CatalogItems`
- [x] 3.4 Run `npm run test:backend` (Docker running) and the targeted
      `dotnet test backend/KeplerTalento.slnx --no-restore --filter "FullyQualifiedName~Catalog"`.
      Inspect the output, then query the local PostgreSQL (`docker compose up`, migrator applied) to
      confirm the `Color` column, default and constraint

## 4. SPA model, API client and service (business-catalogs: Catalog value colour)

- [x] 4.1 Add `CatalogColor` and `color` to `CatalogItem` in `catalog.models.ts`. Add
      `features/catalogs/catalog-color.logic.ts` with `CATALOG_COLORS`, `DEFAULT_CATALOG_COLOR`,
      `isColorableFamily` and `colorNameKey` (design D6)
- [x] 4.2 Add optional `color` to `CatalogItemPayload` in `catalog.api.ts`. `CatalogService.create`
      and `update` accept and send `color`
- [x] 4.3 Add `CatalogService.colorOf(family, name)` over active and inactive items, falling back to
      `orange`, with a per-family name map cached against the current state object (design D6)
- [x] 4.4 Review and update `tests/unit/catalog.service.spec.ts` and the shared catalog doubles in
      `tests/unit/support/` (items now carry `color`). Add cases for payloads, `colorOf` with an
      inactive item, the fallback, and the cache refreshing after `update`
- [x] 4.5 Add a palette parity spec that reads `backend/Domain/Catalogs/CatalogColors.cs` through
      `tests/repo-root.ts` and asserts the token list equals `CATALOG_COLORS`

## 5. Palette styles and i18n (business-catalogs: Catalog value colour — contrast)

- [x] 5.1 In `frontend/src/styles.css`, add one `[data-catalog-color='<token>']` rule per token
      defining `--catalog-chip-bg`, `--catalog-chip-border` and `--catalog-chip-fg`, with orange
      mapped to the existing `--fj-orange-*` tokens (design D6, D8)
- [x] 5.2 Measure each token's text/background contrast (≥ 4.5:1 at 12px/600) and adjust until all
      nine pass
- [x] 5.3 Add the `catalogs.management.column.color`, `catalogs.management.form.color` and
      `catalogs.color.*` keys from the brief to `es.json` (and `en.json`)

## 6. Colour swatch and dialog (business-catalogs: Catalog colour is chosen in administration)

- [x] 6.1 Add `features/catalogs/components/catalog-color-swatch.tsx` (+ `.css`): a read-only
      `span role="img"` and a trigger `Button`, both titled with the colour's Spanish name and given
      the accessible names from the brief
- [x] 6.2 Add `catalog-color-dialog.tsx` (+ `.css`) on `react-aria-components`
      `ModalOverlay`/`Modal`/`Dialog` and a single-select grid `ListBox` (design D7) with a 3×3 grid, visible names,
      a check icon on the current colour, close on choose, and Escape/«Cancelar» without change.
      Test ids: `catalog-color-dialog`, `catalog-color-option` with `data-value` (design D7)
- [x] 6.3 Add `tests/unit/catalog-color-dialog.spec.tsx`: nine named options, current option
      checked, arrow-key navigation, choosing calls `onChange` and closes, Escape leaves the colour
      unchanged and returns focus to the trigger

## 7. Catalog administration page (business-catalogs: Catalog colour is chosen in administration)

- [x] 7.1 In `catalog-management-page.tsx`, for colourable families only, add the «Color» column
      between «Nombre» and «Estado»: the read-only swatch (`catalog-color`), or the trigger in the
      edited row (`catalog-color-trigger`), backed by an `editColor` draft that «Cancelar» and
      Escape discard and `saveEdit` sends
- [x] 7.2 Add the «Color» field to the create form (`new-catalog-color-trigger`), backed by
      `newColor`. It resets to `orange` after a successful «Añadir» and when the family changes
- [x] 7.3 Review and update `tests/unit/catalog-management-rows.spec.tsx`,
      `catalog-management-level-order.spec.tsx` and `catalog-loading-states.spec.tsx`. Add cases for
      the column only on colourable families, tooltip and accessible name, edit-save sending `color`,
      cancel discarding it, and the create form reset

## 8. Coloured chips (catalog-value-picker: Chips carry their value's catalog colour; saved-search-presets: Criteria summary chips carry catalog colours)

- [x] 8.1 Add the optional `colorOf` prop to `CatalogValuePicker`, set `data-catalog-color` on each
      chip, and make `.catalog-picker-chip` read the custom properties with the orange fallbacks
- [x] 8.2 Pass `colorOf` from `useCatalogs()` in `candidate-relation-section.tsx` (value family of
      the relation) and `search/components/criteria-group.tsx` (value family of the group)
- [x] 8.3 Change `buildSummaryGroups` values to `{ text, color? }`, with colours for the skill,
      language, program and tag groups only. Render `data-catalog-color` in
      `search-criteria-summary.tsx`, and make `.filters-summary .chip` read the custom properties
- [x] 8.4 Review and update `tests/unit/catalog-value-picker.spec.tsx`, the search criteria
      summary/logic specs and any spec asserting `values` strings. Add cases for colour on picker
      chips, inactive value colour, the fallback to orange, and only catalog summary chips coloured
- [x] 8.5 Show an `aria-hidden` colour circle left of each offered value in the picker's add list
      (catalog-value-picker: Chips carry their value's catalog colour, added on request during
      apply), with picker spec cases for the circle and its absence without a resolver

## 9. Frontend suites and end-to-end verification

- [x] 9.1 Run `npm test` from `frontend/` and inspect the results (unit, integration and security
      projects)
- [x] 9.2 Run `npm run security:rls` and `npm run security:storage` and confirm they still pass
      (no grant or storage change expected)
- [x] 9.3 Extend `tests/e2e/catalogs-crud.spec.ts`: create a `Date.now()`-marked tag with a colour
      through the dialog, change it while editing, assert the row's `data-catalog-color`, assign the
      tag to a candidate, and assert the «Competencias» chip's `data-catalog-color`, with no
      Spanish-text selectors
- [x] 9.4 With `docker compose up` running, run `npx playwright test tests/e2e/catalogs-crud.spec.ts`
      and inspect the result. Confirm the global teardown purged the marked records
- [x] 9.5 Open `/admin/catalogs` on the dev server (:4300) and visually check the nine swatches, the
      dialog at 360px width, and the coloured chips on a candidate, the search summary, the preset
      list and a position page

## 10. Quality gates and documentation

- [x] 10.1 Run `npm run build:all`, `npm run lint` and `npm run format:check` from `frontend/` and
      fix any finding
- [x] 10.2 Record the palette tokens and their measured contrast ratios in
      `docs/CORPORATE_IDENTITY_Kepler.md`
- [x] 10.3 Add `docs/ktl-41/release-notes.md`: the additive API contract (`color` on responses,
      optional on create and update, error codes), the `AddCatalogItemColor` migration and rollback,
      and the colourable families
- [x] 10.4 Add a line to the `README.md` «Catálogos» section (in Spanish) describing the colour
      choice

## 11. Catalog administration actions (data-tables: Catalog values are edited from their row actions; requested during apply)

- [x] 11.1 Lay out the add form as name in one half and colour + code in the other
      (`.catalog-create-pair` in the new `catalog-management-page.css`) (design D9)
- [x] 11.2 Hide the add form behind «Nuevo» (`catalog-new`) to the right of the family selector,
      on the same line at every width; add «Cancelar» (`catalog-create-cancel`); close and reset
      the form after a successful «Añadir»; lock «Nuevo», the form and the row edits against each
      other (design D9)
- [x] 11.3 Stop the row click from starting the edit; make the name plain text; add the pencil
      edit icon button (`catalog-edit`, «Editar <value>») and turn «Desactivar»/«Activar» into the
      `catalog-toggle-active` icon button; add `PencilIcon`, `BanIcon`, `RestoreIcon` and the
      `es.json` keys (design D9)
- [x] 11.4 Rewrite `tests/unit/catalog-management-rows.spec.tsx` for the inert row, the pencil, the
      icon toggle, the locks and «Nuevo»; update `catalog-management-colors.spec.tsx` to open the
      form with «Nuevo»
- [x] 11.5 Update `tests/e2e/catalogs-crud.spec.ts` and `tests/e2e/candidate-tags-notes.spec.ts`
      to open the form with «Nuevo» and edit or toggle through test ids; run them with
      `tests/e2e/data-tables.spec.ts` against the stack and inspect the result
- [x] 11.6 Update the `README.md` «Tablas coherentes KTL-31» paragraph and the KTL-41 release note
      for the new catalog actions
