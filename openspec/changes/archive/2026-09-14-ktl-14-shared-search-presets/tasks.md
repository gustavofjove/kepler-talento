## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-14` from an up-to-date `main`

## 1. Backend domain and persistence (shared library, version, migration)

- [x] 1.1 Update `Domain/Search/SearchPreset.cs`:
  - remove `OwnerId`;
  - add an integer `Version`, incremented by `Rename`/`ReplaceFilters`;
  - make `MarkUsed` set only `LastUsedAtUtc` (design D2, D3);
  - make `SearchPresetName.Normalize` delegate to `CatalogName.Normalize` and rewrite its remarks
    (D4).

  Covers: Preset create and update; Preset deletion and last-used tracking.

- [x] 1.2 Update `ISearchPresetRepository` and `SearchPresetRepository`:
  - drop the owner parameter from `ListAsync`/`FindAsync`;
  - add `ExpectVersion`;
  - map `DbUpdateConcurrencyException` to a new `SearchPresetSaveOutcome.ConcurrencyConflict`.
- [x] 1.3 Update `SearchPresetConfiguration`:
  - make `Version` an `IsConcurrencyToken()`, with a comment pointing to D2;
  - add the unique index `UX_ADM_SearchPresets_NormalizedName`;
  - add the check `CK_ADM_SearchPresets_Version`;
  - relax `CK_ADM_SearchPresets_Timestamps`.
- [x] 1.4 Generate the migration `ShareSearchPresets` with `dotnet ef migrations add` and extend it
      per D8:
  - `Up`: delete the existing rows, drop the owner index, check and column, add the version and
    the new index and checks, and add a comment stating the grants are unchanged;
  - `Down`: restore the owner structure on an empty table, with a comment that the discarded rows
    are unrecoverable.

  Covers: Existing private presets are discarded.

## 2. Backend application and HTTP (authorization, contract)

- [x] 2.1 Add `Permissions.PresetsManage = "presets.manage"` to `ICurrentActor.cs` and grant it in
      `Web/Identity/DevelopmentActor.cs`. Covers: Preset management permission.
- [x] 2.2 In `SearchContract.cs`:
  - remove `SearchGuards.RequireOwner` and `PresetOwnerUnknown`;
  - add `RequireManagePresets` and `PresetConcurrencyConflict`
    (`search_preset.concurrency.conflict`);
  - reword the Spanish name-conflict message without owner wording.
- [x] 2.3 In `ManageSearchPresets.cs`:
  - add `GetSearchPresetQuery` and its handler;
  - add `Version` to the update and delete commands;
  - guard writes with `RequireManagePresets` and reads/use with `RequireRead`;
  - call `ExpectVersion`;
  - map the concurrency outcome;
  - add `Version` to `SearchPresetResponse`;
  - add FluentValidation validators for create, update and delete.

  Covers: Shared preset listing and retrieval; Preset create and update.

- [x] 2.4 In `SearchEndpoints.cs`:
  - add `GET /{id:guid}` (`GetSearchPreset`);
  - add `Version` to the PUT request;
  - require `?version=` on DELETE;
  - make `Require(actor, permission)` check each route's own permission before `sender.Send`;
  - declare `.Produces*` metadata, including 409 on DELETE (D5, D6).
- [x] 2.5 Confirm `Web/Observability/PersonalDataRedaction.cs` still redacts preset names and
      filters, and remove owner-specific handling if any remains. Covers: Preset privacy and safe
      diagnostics.
- [x] 2.6 Run `npm run build:all` and confirm the backend compiles with warnings as errors.

## 3. Backend tests (including fail-closed security evidence)

- [x] 3.1 Review and update the existing unit tests in
      `Tests/UnitTests/Features/SearchHandlerTests.cs` for the owner removal:
  - add the guard matrix per handler;
  - add the version expectation;
  - check that `MarkUsed` leaves `Version`/`UpdatedAtUtc` unchanged;
  - check that the accent-folded name normalizes (`Inglés B2` vs `ingles b2`).
- [x] 3.2 Update `Tests/UnitTests/Features/SearchLogRedactionTests.cs` and
      `Tests/UnitTests/Persistence/DatabaseNamingTests.cs` for the new names and absent owner data.
- [x] 3.3 Extend `Tests/IntegrationTests/SearchApiTests.cs` with the fail-closed matrix: for every
      preset route, unauthenticated, `candidates.read`-only and `presets.manage`-only actors each get
      the expected 401/403, including malformed bodies, with no preset data in the refusal.
- [x] 3.4 Extend `SearchApiTests` with the behaviour tests:
  - cross-actor visibility;
  - `GET {id}` found and 404;
  - name conflict ignoring case and accents;
  - stale version on PUT and DELETE;
  - apply-then-save with the original version succeeding;
  - delete then 404.
- [x] 3.5 Extend `Tests/IntegrationTests/SearchSchemaTests.cs`:
  - seed owner-scoped rows, apply `ShareSearchPresets`, and assert the table is empty with no
    `OwnerId`;
  - assert the index and check definitions;
  - assert `ktl_runtime` holds exactly `SELECT, INSERT, UPDATE, DELETE` on `ADM_SearchPresets`.
- [x] 3.6 Run `npm run test:backend` with Docker running, inspect the output, and confirm the
      migrated PostgreSQL state (table columns, indexes, grants) matches the assertions.

## 4. Frontend permission, navigation and routes

- [x] 4.1 Add `manage_presets` to `Permission`, `ALL_PERMISSIONS` and `DEFAULT_ROLES`
      (`rrhh_admin`, `system_admin`) in `src/app/shared/models/auth.models.ts`. Add its label key to
      `es.json` if the roles page renders permission labels. Covers: Preset management permission
      (default roles).
- [x] 4.2 Add the `Presets` child after `Catálogos` in `ADMIN_GROUP` and `manage_presets` to
      `NAV_PERMISSIONS` in `nav-items.ts`. Add `usePermission('manage_presets')` to `PrimaryNav`.
      Covers: primary-navigation requirements.
- [x] 4.3 Add the `RequirePermission permission="manage_presets"` layout route in `app.tsx` with
      `admin/presets`, `admin/presets/new` and `admin/presets/:id/edit` (no read-only route).
      Covers: Preset administration section (route guard).

## 5. Shared criteria editor and summary

- [x] 5.1 Create `search-criteria.logic.ts`, moving the status options and empty drafts into it,
      with labels as `search.criteria.*` keys. Create `search-criteria-summary.tsx`
      (`SearchCriteriaSummary({ filters })`), keeping `data-testid="filters-summary"` and moving its
      copy to keys.
- [x] 5.2 Create `search-criteria-form.tsx` (`SearchCriteriaForm`) from `search-filters.tsx`:
  - an `actions` slot;
  - optional `collapsed`/`onCollapsedChange`;
  - `onSubmit`;
  - every `name=`, `id`, `data-status` and `data-testid` preserved;
  - copy moved to keys.

  Covers: Shared criteria editor and summary.

- [x] 5.3 Delete `search-filters.tsx` and `filters-summary.tsx`, update their importers, and remove
      them (and `criteria-group.tsx` if its copy was touched) from `LEGACY_HARDCODED_COPY` in
      `eslint.config.js`.

## 6. Frontend service and search page

- [x] 6.1 Update `search.models.ts` (`SearchPreset.version`) and `search-presets.service.ts`:
  - add `get(id)`;
  - add a version parameter to `updatePreset` and `removePreset`;
  - throw `TranslatableError` for a blank name;
  - rewrite the owner wording in comments.
- [x] 6.2 Update `advanced-search-page.tsx`:
  - render `SearchCriteriaForm` with collapse and Buscar/Limpiar actions;
  - remove the name input and the save and delete handlers and controls;
  - add the «Gestionar presets» link gated by `usePermission('manage_presets')`;
  - clear the selection on a failed apply;
  - move copy to keys.

  Covers: Search page applies presets only.

## 7. Admin › Presets pages

- [x] 7.1 Create `features/admin/presets/preset-list.logic.ts` (pure name filter, sort, paginate)
      and `preset-list-page.tsx` + `.css`:
  - table in `.table-wrap` with the Nombre (with an eye button that opens the criteria dialog),
    Última actualización, Último uso and Acciones columns;
  - loading, empty and error states;
  - `Pagination`;
  - delete through `confirmDialogService` with the version.

  Covers: Preset administration section.

- [x] 7.2 Create `preset-edit-page.tsx` for create and edit:
  - name field `name="presetName"`;
  - the privacy hint;
  - `SearchCriteriaForm` with Guardar/Cancelar;
  - distinct name-conflict and stale-version messages via `errorText`;
  - a 404 goes back to the list with a toast.
- [x] 7.3 Create the shared `search-criteria-dialog.tsx` (`SearchCriteriaDialog`: title, shared
      summary, host details and actions; Escape, outside click, focus trap and focus return) and
      open it from the preset list's eye button with `formatDate` timestamps («Nunca» when never
      used) and «Editar». Remove the read-only detail page and route; saving or cancelling the
      edit page returns to the list. Covers: Preset administration section (criteria dialog).
- [x] 7.4 Add every new `presets.*` and `nav`-adjacent key to `src/assets/i18n/es.json`, with
      correct accents, and the optional `en.json` values.

## 8. Frontend tests

- [x] 8.1 Review and update the existing unit specs affected by the change:
  - `tests/unit/nav-items.spec.ts` and `tests/unit/primary-nav.spec.tsx` (the Presets child, the
    group with only `manage_presets`, the active state on nested preset routes);
  - `tests/unit/search-presets.service.spec.ts` (`get`, version on update and delete);
  - `tests/unit/advanced-search-page.spec.tsx` (no management controls, apply, permission-gated
    link);
  - `tests/unit/role.service.spec.ts` (the permission list).
- [x] 8.2 Add `tests/unit/search-criteria-form.spec.tsx` and `search-criteria-summary.spec.tsx`,
      porting the behaviour previously covered for the filters and summary. Include a spec asserting
      that the search page and the preset edit page expose the same criteria `name` and test-id set.
- [x] 8.3 Add `tests/unit/preset-list-page.spec.tsx`, `preset-edit-page.spec.tsx` and
      `search-criteria-dialog.spec.tsx`:
  - the eye button opens the dialog, and Escape closes it back to the button;
  - states;
  - cancelled deletion sends nothing;
  - 409 name and version messages keep the entered values;
  - 404 handling.
- [x] 8.4 Run `npm test` (unit, integration and security projects) and inspect the output. All
      green, with the legacy Supabase integration checks for unchanged paths still passing.

## 9. End-to-end verification

- [x] 9.1 Rewrite `tests/e2e/advanced-search-presets.spec.ts`:
  - an admin creates, edits and deletes a preset in Admin › Presets;
  - a preset is applied in Búsqueda;
  - it persists after a reload;
  - keep the legacy-browser-presets test;
  - no Spanish-text selectors.
- [x] 9.2 Update `tests/e2e/navigation-responsive.spec.ts` for the Presets child at 1280 and 390 px.
- [x] 9.3 Start the stack (`docker compose up --build`, so the `migrator` applies
      `ShareSearchPresets`), then actually run `npx playwright test tests/e2e/advanced-search-presets.spec.ts`
      and `tests/e2e/navigation-responsive.spec.ts`, and inspect the results.
- [ ] 9.4 Run the full `npm run e2e` suite and confirm no regression in `advanced-search.spec.ts` or
      the other journeys. Afterwards, delete any presets the run created so the development database
      is left as it was.

## 10. Security and least-privilege checks

- [x] 10.1 Against the running stack, verify manually with the development actor that a write
      succeeds, and confirm through the integration matrix (3.3) that unauthenticated and read-only
      callers are refused before validation on every preset route.
- [x] 10.2 Query the migrated database as the migrator and confirm the `ktl_runtime` privileges on
      `ADM_SearchPresets` are unchanged and that no new grant exists elsewhere.
- [x] 10.3 Run `npm run security:rls` and `npm run security:storage` and confirm they still pass
      (legacy paths unchanged).

## 11. Documentation

- [x] 11.1 Write `docs/ktl-14/presets.md`:
  - endpoint table and permissions;
  - shared-library semantics;
  - integer version rationale;
  - physical-delete exception;
  - the `manage_presets`-without-`view_candidates` note;
  - migration and rollback.
- [x] 11.2 Write `docs/ktl-14/release-notes.md` (existing saved searches are reset; the new
      permission and the roles that hold it). Mark the preset section of `docs/ktl-10/search.md` as
      superseded by KTL-14.
- [x] 11.3 Update `README.md` (in Spanish) to mention Admin › Presets and `manage_presets` wherever
      sections or permissions are listed.

## 12. Quality gates

- [ ] 12.1 Run `npm run lint` and `npm run format:check`, fix any findings, and re-run them until
      they pass.
- [x] 12.2 Run `npm run build:all` once more and `openspec validate ktl-14-shared-search-presets --strict`.
