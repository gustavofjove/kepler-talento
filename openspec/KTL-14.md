# KTL-14 — Presets and reuse of the advanced search section

## [original]

Presets and reuse of the advanced search section

The presets management will be moved to its own sub-section under the Admin section. Similarly to the rest of sub-sections such as catalogs, users and roles, presets will also have its own manage_presets permission. The presets will require to be persisted into the database and users with the manage_presets permission will be able to see the full list (similar to "candidatos") where they can perform CRUD operations from.

The presets will be the base and an equivalent for/to the advanced search so the both will share the same edit form and read-only views. Therefore we will have a single component for editing a preset and the advanced search and same for the read-only views, so that changes made to one of them will be automatically applied to both.

Changes and improvements to these componentes will be implemented in the future as part of another ticket.

## [enhanced]

### User story

**As** an HR administrator holding `manage_presets`,
**I want** to maintain a shared library of search presets from **Admin › Presets**, using the same
criteria form and read-only summary as **Búsqueda**,
**so that** every recruiter applies the same, curated searches, and any change to the criteria
editor or summary reaches both screens at once.

**As** a recruiter holding `view_candidates`,
**I want** to pick a shared preset in **Búsqueda** and have its filters applied,
**so that** I don't have to rebuild common searches by hand.

### Context — what exists today (KTL-10)

Presets are **already persisted** in PostgreSQL. This ticket changes their ownership, their
permissions and where they are managed. It does not add storage from scratch.

| Aspect      | Today                                                                                                    | After KTL-14                                               |
| ----------- | -------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| Ownership   | Private per actor (`ADM_SearchPresets.OwnerId`); others' presets are indistinguishable from missing ones | **Shared library**: one organization-wide set              |
| Read        | `candidates.read`, own presets only                                                                      | `candidates.read`, every preset                            |
| Write       | `candidates.read`, own presets                                                                           | **`presets.manage`** (`manage_presets` in the frontend)    |
| Name unique | Per owner, case-insensitive (`UX_ADM_SearchPresets_Owner_NormalizedName`)                                | Global, case- and accent-insensitive                       |
| UI          | Picker, name, «Guardar actual» and «Eliminar» on `advanced-search-page.tsx`                              | Picker only in Búsqueda; full CRUD in `/app/admin/presets` |
| Removal     | Physical `DELETE`                                                                                        | Physical `DELETE` (unchanged, see decisions)               |
| Concurrency | None                                                                                                     | Optimistic `Version` on update and delete                  |

Relevant code: [SearchEndpoints.cs](../backend/Web/Features/Search/SearchEndpoints.cs),
[ManageSearchPresets.cs](../backend/Application/Features/Search/ManageSearchPresets.cs),
[SearchPreset.cs](../backend/Domain/Search/SearchPreset.cs),
[20260910153941_AddSearchPresets.cs](../backend/Infrastructure/Persistence/Migrations/20260910153941_AddSearchPresets.cs),
[search-presets.service.ts](../src/app/features/search/services/search-presets.service.ts),
[advanced-search-page.tsx](../src/app/features/search/pages/advanced-search-page.tsx),
[search-filters.tsx](../src/app/features/search/components/search-filters.tsx),
[filters-summary.tsx](../src/app/features/search/components/filters-summary.tsx),
spec [saved-search-presets](specs/saved-search-presets/spec.md), docs [docs/ktl-10/search.md](../docs/ktl-10/search.md).

### Decisions taken during refinement

1. **Shared library.** Presets no longer have an owner. Only `presets.manage` creates, edits or
   deletes them. Anyone with `candidates.read` lists and applies them.
2. **Búsqueda is apply-only.** The name input, «Guardar actual» and «Eliminar» are removed from
   the search page for every user, including holders of `manage_presets`. All management happens
   in Admin › Presets.
3. **Physical delete stays.** Non-negotiable 5 covers candidates and catalog items only. A preset
   is configuration, not personal-data history, and no other table references it. The design
   must record this explicitly so it is not read as a precedent.
4. **Existing presets are discarded.** The migration removes every row in `ADM_SearchPresets`
   before dropping `OwnerId`. Private presets can't be safely promoted to a shared library:
   their names and filters may hold search terms their owners never meant to share, and names
   collide between owners. Release notes must say they have to be recreated.
5. **Routes like Candidatos.** Each preset has its own URL for the list, create, read-only detail
   and edit views.
6. **One editor, one summary.** The criteria form and the read-only summary are single components
   used by both Búsqueda and Admin › Presets. Visual or behavioural improvements to them are
   **out of scope** (future ticket). This ticket only makes them reusable.

### Functional description

#### Admin › Presets — list (`/app/admin/presets`)

- New Admin child entry **«Presets»**, visible only with `manage_presets`, placed after
  «Catálogos».
- Page header «Presets de búsqueda» and a «Nuevo preset» button.
- Table columns: **Nombre** (link to detail), **Criterios** (the shared read-only summary in its
  compact form, or a count of applied filter families if compact rendering isn't available
  without changing the component), **Última actualización**, **Último uso** («Nunca» when null),
  and **Acciones** («Editar», «Eliminar»).
- Client-side name filter, sorting by name / updated / last used (default: name ascending,
  ignoring case), and the shared `Pagination` component. The list is small and loaded whole,
  as candidates are today.
- Loading, empty («Todavía no hay presets. Crea el primero.») and error states.
- «Eliminar» opens `confirmDialogService` (danger). On confirmation the preset is deleted and
  the list refreshes.

#### Create / edit (`/app/admin/presets/new`, `/app/admin/presets/:id/edit`)

- Field **Nombre** (`name="presetName"`, required, max 120 characters).
- The **shared criteria form**, the same component Búsqueda renders, with the same fields,
  `name=` attributes and `data-testid`s.
- Actions «Guardar» and «Cancelar» (back to the list, or to the detail page when editing). The
  search-only actions («Buscar», «Limpiar», collapse toggle) are not shown here.
- A hint under the name: «Los presets son visibles para todas las personas con acceso a la
  búsqueda. No incluyas datos personales en el nombre ni en el texto libre.»
- Errors are rendered with `errorText(err, t)`:
  - blank or too-long name → field validation;
  - name already used (409 `preset-name-conflict`) → «Ya existe un preset con ese nombre.»;
  - stale version (409 concurrency) → «Otra persona ha modificado este preset. Recarga para ver
    los cambios.»;
  - missing preset (404) → back to the list with a toast.

#### Detail (`/app/admin/presets/:id`)

- Name, created / updated / last-used timestamps (`formatDate`), and the **shared read-only
  summary** of its filters.
- Actions «Editar» and «Eliminar».

#### Búsqueda (`/app/search`)

- Keeps the «Preset guardado» picker (`name="selectedPreset"`), now listing the shared presets.
  Selecting one applies it exactly as today: it calls `/use`, loads the filters, runs the search
  and remembers the last filters locally.
- The «Nombre para guardar» input, «Guardar actual» and «Eliminar» are removed.
- With `manage_presets`, a «Gestionar presets» link to `/app/admin/presets` is shown next to the
  picker.
- The criteria form and its summary are rendered through the shared components. Behaviour stays
  the same: editing a filter does not re-run the search, and collapsing and running work as they
  do today.

### Shared components (reuse contract)

Extract from `src/app/features/search/components/` without changing behaviour:

| Component                                                                                                                 | Replaces / based on                                                                            | Used by                                                    |
| ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `SearchCriteriaForm` (controlled: `filters`, `onFiltersChange`, `actions` slot, optional `collapsed`/`onCollapsedChange`) | `search-filters.tsx` minus its hardcoded «Buscar»/«Limpiar» buttons and the mandatory collapse | `AdvancedSearchPage`, `PresetEditPage`                     |
| `SearchCriteriaSummary` (`filters`, optional `compact`)                                                                   | `filters-summary.tsx` + `buildSummaryGroups`; it computes groups itself                        | `SearchCriteriaForm`, `PresetDetailPage`, `PresetListPage` |

- `STATUS_LABELS` / `STATUS_OPTIONS` move to a sibling `.logic.ts` (fast refresh rule), and their
  labels become i18n keys.
- `data-testid="filters-summary"`, `data-testid="toggle-filters"`, `name="text"`, `name="hasCv"`,
  `data-status` and every criteria control keep their current identifiers.
- A change to either component must show up in both screens with no further code. Tests assert
  that both pages render the same component, not a copy.

### API contract (`backend/Web/Features/Search/SearchEndpoints.cs`)

The route group `/api/search-presets` is kept. No new routes are invented beyond `GET /{id}`,
which the detail and edit pages need.

| Method | Route                               | Permission        | Result                                                       | Change                 |
| ------ | ----------------------------------- | ----------------- | ------------------------------------------------------------ | ---------------------- |
| GET    | `/api/search-presets`               | `candidates.read` | all presets, by name (case-insensitive)                      | scope: shared          |
| GET    | `/api/search-presets/{id}`          | `candidates.read` | one preset; 404 if missing                                   | **new**                |
| POST   | `/api/search-presets`               | `presets.manage`  | 201 + preset; 400 validation; 409 name conflict              | permission             |
| PUT    | `/api/search-presets/{id}`          | `presets.manage`  | 200 + preset; 400; 404; 409 name conflict or stale `version` | permission + `version` |
| DELETE | `/api/search-presets/{id}?version=` | `presets.manage`  | 204; 404; 409 stale `version`                                | permission + `version` |
| POST   | `/api/search-presets/{id}/use`      | `candidates.read` | 200 + preset; advances `lastUsedAt` only                     | scope: shared          |

- **Response** `SearchPresetResponse`: `id`, `name`, `filters`, `createdAt`, `updatedAt`,
  `lastUsedAt?`, **`version`**. No actor identifiers.
- **Request** `SearchPresetRequest`: `name`, `filters`, plus `version` on PUT.
- **Fail closed:** every route checks `actor.IsAuthenticated` and the permission **before**
  dispatching, as the endpoint class does today. The write handlers repeat the guard
  (`SearchGuards.RequireManagePresets(actor)`), and the read handlers keep
  `SearchGuards.RequireRead`.
- `use` must **not** advance `updatedAt` or `version`. Otherwise every recruiter applying a
  preset would make an administrator's open edit fail with a concurrency conflict.
- Errors are the existing `Application/Common/Errors` types mapped by `GlobalExceptionHandler`.
  The stable codes `preset-name-conflict`, `preset-not-found` and validation codes stay as they
  are, and the concurrency conflict reuses the existing concurrency error type.

### Application / Domain / Persistence

- `Application/Abstractions/Identity/ICurrentActor.cs`: add `Permissions.PresetsManage =
"presets.manage"`.
- `Web/Identity/DevelopmentActor.cs`: grant `PresetsManage`. It must stay impossible to enable in
  Production (unchanged).
- `Application/Features/Search/ManageSearchPresets.cs`:
  - add `GetSearchPresetQuery`;
  - `UpdateSearchPresetCommand(Id, Name, Filters, Version)` and
    `DeleteSearchPresetCommand(Id, Version)`;
  - remove `RequireOwner`; writes require `presets.manage`;
  - a FluentValidation validator per command, per the backend conventions.
- `Application/Abstractions/Persistence/ISearchPresetRepository.cs` +
  `Infrastructure/Persistence/SearchPresetRepository.cs`: the owner parameter goes away from
  `ListAsync`/`FindAsync`, and the save outcome adds a concurrency conflict.
- `Domain/Search/SearchPreset.cs`:
  - drop `OwnerId`;
  - add `Version`, using the same mechanism as the catalog items' optimistic concurrency;
  - `MarkUsed` sets `LastUsedAtUtc` only;
  - `SearchPresetName.Normalize` folds accents like `CatalogName.Normalize`, because the name is
    now a shared vocabulary. Update the remarks that justified the opposite.
- `Infrastructure/Persistence/Configurations/SearchPresetConfiguration.cs`: concurrency token,
  and a unique index on `NormalizedName`.
- **Migration** `ShareSearchPresets` (via `dotnet ef migrations add`; never hand-edit the schema
  outside the migration), run by `ktl_migrator`:
  1. `DELETE FROM "ADM_SearchPresets"` (decision 4), with a comment explaining why;
  2. drop `UX_ADM_SearchPresets_Owner_NormalizedName`, `CK_ADM_SearchPresets_Owner` and
     `OwnerId`;
  3. add `Version` and `UX_ADM_SearchPresets_NormalizedName`;
  4. relax `CK_ADM_SearchPresets_Timestamps` to `"UpdatedAtUtc" >= "CreatedAtUtc"`, because
     `LastUsedAtUtc` may now be later than `UpdatedAtUtc`;
  5. **grants unchanged**: `ktl_runtime` keeps `SELECT, INSERT, UPDATE, DELETE` on this table
     only.

  `Down` restores the structure but can't restore the discarded rows. Both the migration and
  the runbook say so.

- `Web/Observability/PersonalDataRedaction.cs`: preset name and filters stay redacted. No owner
  key needs redacting any more.

### Frontend files

| File                                                                                                                     | Change                                                                                                                                                                                                         |
| ------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/shared/models/auth.models.ts`                                                                                   | add `manage_presets` to `Permission`, `ALL_PERMISSIONS`, and `DEFAULT_ROLES` for `rrhh_admin` and `system_admin`                                                                                               |
| `src/app/core/layout/nav-items.ts`                                                                                       | Admin child `{ label: 'Presets', to: '/app/admin/presets', permission: 'manage_presets', testId: 'nav-presets' }`; add to `NAV_PERMISSIONS`                                                                    |
| `src/app/core/layout/primary-nav.tsx`                                                                                    | `const canManagePresets = usePermission('manage_presets')` and add it to the granted map                                                                                                                       |
| `src/app/app.tsx`                                                                                                        | `RequirePermission permission="manage_presets"` layout route with `admin/presets`, `admin/presets/new`, `admin/presets/:id`, `admin/presets/:id/edit`                                                          |
| `src/app/features/admin/presets/preset-list-page.tsx` (+ `.css`, `preset-list.logic.ts`)                                 | new list page                                                                                                                                                                                                  |
| `src/app/features/admin/presets/preset-edit-page.tsx`                                                                    | new create/edit page using `SearchCriteriaForm`                                                                                                                                                                |
| `src/app/features/admin/presets/preset-detail-page.tsx`                                                                  | new read-only page using `SearchCriteriaSummary`                                                                                                                                                               |
| `src/app/features/search/components/search-criteria-form.tsx`, `search-criteria-summary.tsx`, `search-criteria.logic.ts` | shared components (rename/refactor of `search-filters.tsx` / `filters-summary.tsx`)                                                                                                                            |
| `src/app/features/search/services/search-presets.service.ts`                                                             | `get(id)`, `updatePreset(id, name, filters, version)`, `removePreset(id, version)`; drop owner wording; keep `useSearchPresets()` as the render-time hook                                                      |
| `src/app/features/search/models/search.models.ts`                                                                        | `SearchPreset.version: number`                                                                                                                                                                                 |
| `src/app/features/search/pages/advanced-search-page.tsx`                                                                 | remove save/delete UI, add the «Gestionar presets» link                                                                                                                                                        |
| `src/assets/i18n/es.json` (+ `en.json` optional)                                                                         | keys under `presets.*`, `search.criteria.*`, `nav.presets`                                                                                                                                                     |
| `eslint.config.js`                                                                                                       | remove every touched file from `LEGACY_HARDCODED_COPY` (at least `search-filters.tsx`, `filters-summary.tsx`, `advanced-search-page.tsx`, and `criteria-group.tsx` if touched); new files never go on the list |

Conventions: function components, plain `.css`, `usePermission()` hoisted, `useErrorToast()`,
`TranslatableError` for new validation in the service, `.span-all` instead of inline grid styles.
No new npm or NuGet dependency.

### Acceptance criteria

**Permissions and navigation**

- **Given** a profile with `manage_presets`, **when** they open Admin, **then** «Presets» is
  listed and leads to `/app/admin/presets`.
- **Given** a profile without `manage_presets`, **then** «Presets» is absent, and navigating to
  any `/app/admin/presets*` URL is redirected by the route guard.
- **Given** a profile whose only admin permission is `manage_presets`, **then** the Admin parent
  is rendered with «Presets» as its only child.

**API — fail closed**

- **Given** an unauthenticated caller, **when** it calls any `/api/search-presets` route,
  **then** it receives 401/403 with no preset data, and invalid bodies are not validated first.
- **Given** an actor with `candidates.read` but not `presets.manage`, **when** it calls POST,
  PUT or DELETE, **then** it receives 403 and nothing changes; GET, GET `{id}` and `use`
  succeed.
- **Given** an actor with `presets.manage` but not `candidates.read`, **then** it can create,
  update and delete but not list, get or apply. The frontend's admin roles hold both.

**Shared library**

- **Given** administrator A creates preset «Java senior», **when** recruiter B lists presets,
  **then** «Java senior» is returned with identical filters.
- **Given** «Inglés B2» exists, **when** anyone creates or renames a preset to «ingles b2»,
  **then** the request is rejected with 409 `preset-name-conflict` and neither preset changes.
- **Given** two administrators load the same preset at version _n_, **when** the second saves
  after the first, **then** the second gets 409 and the first's change persists.
- **Given** a recruiter applies a preset, **then** `lastUsedAt` advances and `updatedAt` and
  `version` do not change, so an administrator's pending edit still saves.
- **Given** an administrator deletes a preset with the current version, **then** it disappears
  from the list, the Búsqueda picker and GET `{id}` (404); no candidate data changes.

**Migration**

- **Given** a database with KTL-10 private presets, **when** `ShareSearchPresets` runs through
  `--migrate`, **then** the table is empty, `OwnerId` is gone, the global unique index exists,
  and `ktl_runtime` holds exactly `SELECT, INSERT, UPDATE, DELETE` on it.

**UI**

- **Given** `/app/admin/presets/new`, **when** the administrator enters a name and criteria and
  saves, **then** they land on the detail page, which shows the same summary Búsqueda shows for
  those filters.
- **Given** the edit page, **then** its criteria controls are the same component as Búsqueda's,
  with identical `name=` and `data-testid` values.
- **Given** Búsqueda, **then** there is no name input, «Guardar actual» or «Eliminar»; selecting
  a preset runs the search with its filters.
- **Given** a user with `manage_presets` on Búsqueda, **then** «Gestionar presets» links to the
  admin list; without it, the link is absent.
- **Given** a viewport 390 px wide, **then** the list table scrolls inside `.table-wrap` and the
  page body does not scroll horizontally.
- All new copy is Spanish, comes from `es.json`, and passes `npm run lint`.

### Test coverage

**Backend (xUnit)**

- `Tests/UnitTests/Features/SearchHandlerTests.cs`: manage guard on create/update/delete, read
  guard on list/get/use, version mismatch, `use` leaving `version` untouched, accent-folded
  name conflict.
- `Tests/UnitTests/Features/SearchLogRedactionTests.cs`: still no name or filters in logs.
- `Tests/IntegrationTests/SearchApiTests.cs` (Testcontainers):
  - 401/403 matrix per route for unauthenticated, read-only and manage-only actors;
  - cross-actor visibility;
  - 409 name and version conflicts;
  - 404 after delete;
  - `GET {id}`.
- `Tests/IntegrationTests/SearchSchemaTests.cs`: dropped `OwnerId`, the new unique index, the
  relaxed check, the runtime grants, and the migration emptying existing rows.
- `Tests/UnitTests/Persistence/DatabaseNamingTests.cs`: new index and constraint names follow
  the conventions.

**Frontend (Vitest + Testing Library)**

- `tests/unit/nav-items.spec.ts`, `tests/unit/primary-nav.spec.tsx`: the Presets child and
  permission gating.
- `tests/unit/search-presets.service.spec.ts`: `get`, and `version` sent on update and delete.
- `tests/unit/advanced-search-page.spec.tsx`: save/delete removed, picker applies, link gated by
  permission.
- New `tests/unit/preset-list-page.spec.tsx`, `preset-edit-page.spec.tsx`,
  `preset-detail-page.spec.tsx`: states, delete confirmation, 409 messages, rendering of the
  shared components.
- New `tests/unit/search-criteria-form.spec.tsx`, `search-criteria-summary.spec.tsx`: behaviour
  preserved from the current filters and summary.
- `tests/unit/role.service.spec.ts`: `manage_presets` in the permission list.

**E2E (Playwright, selectors without Spanish text)**

- Rewrite `tests/e2e/advanced-search-presets.spec.ts`: an admin creates, edits and deletes a
  preset in Admin › Presets; in Búsqueda it is applied; after a reload it persists. Keep the
  «legacy browser presets are ignored» test.
- `tests/e2e/navigation-responsive.spec.ts`: the Admin group with the extra child at 1280 and
  390 px.

**Security evidence:** the API integration permission matrix above is the fail-closed evidence
this slice requires. `npm run security:rls` / `security:storage` must still pass (not affected).

### Documentation

- New `docs/ktl-14/presets.md`: endpoint table, permission, shared-library semantics,
  concurrency, the migration discarding existing presets, and rollback (restore the previous
  binaries; the discarded rows are not recoverable).
- New `docs/ktl-14/release-notes.md`: existing saved searches must be recreated by an
  administrator; the new permission and which roles hold it.
- `docs/ktl-10/search.md`: mark the preset section as superseded by KTL-14.
- `openspec/specs/saved-search-presets/spec.md`:
  - replace the owner-scoped requirements with the shared library;
  - add management permission, concurrency and physical delete;
  - keep «Last filters remain local» and «Legacy local presets are explicitly unsupported».
- `openspec/specs/primary-navigation/spec.md`: add `Presets` / `manage_presets` to the entries
  and the Admin group.
- `README.md` (in Spanish): mention the Admin › Presets section and the `manage_presets`
  permission wherever permissions are listed.

### Non-functional requirements

- **Security / personal data:**
  - authorization runs in the API before validation;
  - `ktl_runtime` grants are not broadened;
  - no actor identifiers in responses;
  - preset names and filters are never logged.
  - Because presets are now visible to every `candidates.read` holder, the UI warns
    administrators not to put personal data in them.
- **Performance:** a small configuration table, read whole and served by the unique
  `NormalizedName` index; search queries are unaffected.
- **Accessibility:**
  - table headers with `scope`;
  - labelled fields and error messages linked with `aria-describedby`;
  - the confirm dialog keeps and restores focus;
  - visible focus on row actions;
  - 44 × 44 px touch targets.
- **Responsive:** one DOM tree per page; the table sits inside `.table-wrap`; the form stacks on
  a single column below 768 px through the existing grid classes.
- **Copy:** Spanish with correct accents, via `t()` keys; dates via `formatDate`.

### Out of scope

- Visual or behavioural improvements to the shared criteria form and summary (future ticket).
- Private per-user presets, sharing per team, or preset categories.
- Migrating KTL-10 private presets into the shared library.
- An audit trail of preset changes, and exposing who created or edited a preset.
- Opening Búsqueda pre-filled from the admin detail page (e.g. `/app/search?preset=`).
- Positions (KTL-15).

### Open for design

- Whether the list's **Criterios** column can use a compact mode of `SearchCriteriaSummary`
  without changing its behaviour, or should show only a count of filter families.
- The exact `Version` mechanism: a PostgreSQL `xmin` token or an integer column. Follow the
  catalog items' implementation.
