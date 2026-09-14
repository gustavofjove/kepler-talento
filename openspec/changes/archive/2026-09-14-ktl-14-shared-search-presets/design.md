## Context

See `proposal.md` for motivation, and the delta specs for the required behaviour. This design
covers how the KTL-10 preset slice is reshaped.

Current state:

- **Domain.** `ADM_SearchPresets` holds `OwnerId`, `Name`, `NormalizedName`, `Filters` (jsonb),
  `FilterSchemaVersion`, `CreatedAtUtc`, `UpdatedAtUtc` and `LastUsedAtUtc`.
- **Constraints and grants.**
  - Unique index `UX_ADM_SearchPresets_Owner_NormalizedName`.
  - `CK_ADM_SearchPresets_Owner`, and `CK_ADM_SearchPresets_Timestamps`, which currently requires
    `LastUsedAtUtc <= UpdatedAtUtc`.
  - `ktl_runtime` holds `SELECT, INSERT, UPDATE, DELETE` on the table.
- **Application.** `Application/Features/Search/ManageSearchPresets.cs` guards every handler with
  `SearchGuards.RequireOwner` (which is `candidates.read` plus the actor's `ExternalKey`). The
  repository takes the owner on `ListAsync`/`FindAsync`, and `MarkUsed` moves both `LastUsedAtUtc`
  and `UpdatedAtUtc`.
- **Web.** `SearchEndpoints` calls a local `Require(actor)`, which checks `candidates.read`, before
  dispatching every preset route.
- **Existing concurrency pattern.** Catalog items, candidates, documents and operations map
  `uint Version` to PostgreSQL `xmin` with `IsRowVersion()`. The repository method
  `ExpectVersion` sets the original value, and `DbUpdateConcurrencyException` becomes a
  `ConcurrencyConflict` save outcome. The version travels in the JSON body of PUT requests.
  No existing endpoint deletes with a version.
- **Frontend.**
  - `AdvancedSearchPage` owns the picker plus the save and delete controls.
  - `SearchFilters` (the `search-filters.tsx` component) renders the criteria and the
    Buscar/Limpiar buttons, and embeds `FiltersSummary`.
  - `SearchPresetsService` wraps the API behind a signal read through `useSearchPresets()`.
  - The Admin nav group is data in `nav-items.ts`.
  - All touched search components are on `LEGACY_HARDCODED_COPY`.

## Goals / Non-Goals

**Goals:**

- Reshape the existing slice in place (same table, route group, service and hook) rather than
  building a parallel "shared presets" feature beside the private one.
- Make the criteria editor and summary single components with a host-agnostic API, without
  altering what they render.
- Keep every refusal ahead of validation, and keep runtime grants exactly as they are.

**Non-Goals:**

- Server-side paging or filtering of presets: the library is small and read whole.
- Recording who created or changed a preset (no audit columns).
- Opening the search page pre-filled from a preset URL.
- Any visual redesign of the criteria editor or summary.
- A backend role store. Permissions stay as `ICurrentActor.HasPermission` strings plus the
  frontend `DEFAULT_ROLES`.

## Decisions

### D1. Shared library in the existing table, no owner column

Drop `OwnerId` and replace the owner-scoped unique index with `UX_ADM_SearchPresets_NormalizedName`.
`SearchGuards.RequireOwner` and `SearchErrors.PresetOwnerUnknown` are removed.

_Alternative considered:_ keep `OwnerId` as a nullable "created by" column. Rejected because
nothing in the specs reads it, and storing an actor key the product never shows is personal data
held with no purpose (principle 1). Auditing, if wanted, is a separate ticket with an `AUD_` table.

### D2. Integer `Version` column, not `xmin`

`SearchPreset.Version` is an `int` column configured with `IsConcurrencyToken()`. The domain
increments it in `Rename`/`ReplaceFilters` (one increment per update) and leaves it alone in
`MarkUsed`. The API contract still exposes `version` as an unsigned number, like the other slices.

_Why not the house `xmin` pattern:_ `xmin` changes on every row update. Recording a use would then
invalidate the version an administrator loaded, breaking the spec scenario "Apply does not break a
pending edit".

_Alternatives considered:_

- **Move `LastUsedAtUtc` to a separate table**, so the preset row stays untouched on use. Rejected:
  it needs a second table, a join on every read, and a new grant, all just to preserve the `xmin`
  convention.
- **Drop last-used tracking.** Rejected: the admin list shows it, and KTL-10 already records it.

This is a deliberate departure from a codebase convention (not from a standing principle). It is
recorded here and in a code comment on the configuration.

### D3. `MarkUsed` moves only `LastUsedAtUtc`; the timestamp check is relaxed

`CK_ADM_SearchPresets_Timestamps` becomes `"UpdatedAtUtc" >= "CreatedAtUtc" AND ("LastUsedAtUtc"
IS NULL OR "LastUsedAtUtc" >= "CreatedAtUtc")`. "Update time" now means that the content changed,
which is what the admin list's "Última actualización" column promises.

### D4. Names fold case and accents

`SearchPresetName.Normalize` delegates to `CatalogName.Normalize`, which trims, folds accents
through an explicit table (safe under invariant globalization) and lower-cases. The stored
`NormalizedName` column keeps its length of 120.

The KTL-10 remark argued against accent folding because a name was one person's private label.
That reasoning no longer holds for a shared vocabulary, so the remark is rewritten. The migration
empties the table, so no existing rows need re-normalizing.

### D5. Authorization: two guards, checked at the endpoint and repeated in handlers

- **Guards.**
  - `Permissions.PresetsManage = "presets.manage"` is added.
  - `SearchGuards.RequireManagePresets(actor)` checks `IsAuthenticated && HasPermission(PresetsManage)`
    and throws `ForbiddenException`.
  - `SearchGuards.RequireRead` is kept for list, get and use.
- **Endpoints.** The local `Require` becomes `Require(actor, permission)`. Each route passes its
  own permission before `sender.Send`, so an unauthorized caller never reaches model validation.
- **Validation.** FluentValidation validators are added for create, update and delete commands,
  and for the `version` field in particular. MediatR's `ValidationBehavior` runs them _before_ the
  handler, and therefore before the handler's own guard. That is why the endpoint-level check is
  the one that guarantees "authorize before validate". `SearchPresetMapping.Validate` stays the
  single source of name and filter rules, and the validators call into it.
- **Development actor.** `DevelopmentActor` grants `PresetsManage`. Its Production impossibility
  is unchanged and covered by existing tests.

Managing presets deliberately does **not** imply reading them (per the spec). The frontend's
default admin roles hold both permissions, so this only matters for hand-built roles.

_Alternative considered:_ a single permission for both. Rejected because the brief asks for a
dedicated `manage_presets`, as catalogs, users and roles each have.

### D6. HTTP contract

| Route                                              | Change                                                                                                                                  |
| -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/search-presets`                          | now unscoped                                                                                                                            |
| `GET /api/search-presets/{id:guid}`                | **new**; `ListSearchPresets` sibling `GetSearchPreset`                                                                                  |
| `POST /api/search-presets`                         | `presets.manage`                                                                                                                        |
| `PUT /api/search-presets/{id:guid}`                | `presets.manage`; body `SearchPresetUpdateRequest(Name, Filters, uint Version)`                                                         |
| `DELETE /api/search-presets/{id:guid}?version={n}` | `presets.manage`; the version is required. A missing or non-numeric version is a 400 validation problem, returned _after_ authorization |
| `POST /api/search-presets/{id:guid}/use`           | `candidates.read`; unchanged response shape                                                                                             |

- `SearchPresetResponse` gains `Version`.
- `ConcurrencyConflict` gets the code `search_preset.concurrency.conflict`, mirroring
  `catalog.concurrency.conflict`, and the save outcome enum gains it.
- Existing codes (`search_preset.not_found`, `search_preset.name.conflict`, …) are kept, and the
  Spanish message for the name conflict loses its "Ya tiene" owner wording.

_Why the version is in the query string on DELETE:_ bodies on DELETE are poorly supported by
proxies and `fetch` conventions. The version is not personal data, so exposing it in access logs
is harmless.

_Alternative considered:_ an `If-Match` header. Rejected for consistency, since every other slice
carries the version as a field.

### D7. Physical delete is retained

Presets stay physically deleted. Standing domain rule 5 and principle 1's "support logical
deletion" target candidates and catalog items, whose history other data refers to. A preset is
referenced by nothing: filters point at catalog names, not the other way round. It is not a
personal-data record whose retention must be governed. Adding `IsActive` would bring filtering,
reactivation UI and name-uniqueness-among-inactive questions for no user need.

_Mitigations:_

- deletion requires explicit confirmation and the current version;
- the runtime `DELETE` grant already exists and is not broadened;
- this section is the documented exception so it is not cited as precedent for candidate or
  catalog data.

### D8. Migration `ShareSearchPresets`

Generated with `dotnet ef migrations add ShareSearchPresets` and then extended with
`migrationBuilder.Sql`. It runs only through `--migrate` as `ktl_migrator`.

`Up` does the following, in order and in one transaction:

1. `DELETE FROM "ADM_SearchPresets";`, with a comment citing the spec requirement "Existing private
   presets are discarded".
2. Drop the index `UX_ADM_SearchPresets_Owner_NormalizedName`, the check `CK_ADM_SearchPresets_Owner`
   and the column `OwnerId`.
3. Add `Version integer NOT NULL DEFAULT 1` and the check `CK_ADM_SearchPresets_Version`
   (`"Version" >= 1`).
4. Create the unique index `UX_ADM_SearchPresets_NormalizedName`.
5. Drop and recreate `CK_ADM_SearchPresets_Timestamps` with the relaxed expression (D3).
6. Change no grants. An explicit comment says so, and a schema test asserts the grants are
   exactly `SELECT, INSERT, UPDATE, DELETE`.

`Down` restores the columns, index and checks. It re-adds `OwnerId` as `NOT NULL` on an empty table,
which is safe because `Down` also deletes any shared presets before re-adding it, and its comment
says that rows removed by `Up` cannot be restored.

The migration is repeatable in the project sense: EF history makes a re-run a no-op.

### D9. Frontend structure

- **Permissions.** `manage_presets` is added to `Permission`, `ALL_PERMISSIONS`, and the
  `DEFAULT_ROLES` for `rrhh_admin` and `system_admin`. Its role-editor label, if the roles page
  renders permission labels, comes from `es.json`.
- **Navigation.**
  - `nav-items.ts` gets an `ADMIN_GROUP` child placed after Catálogos:
    `{ label: 'Presets', to: '/app/admin/presets', permission: 'manage_presets', testId: 'nav-presets' }`.
  - `manage_presets` is added to `NAV_PERMISSIONS`.
  - `PrimaryNav` gains `const canManagePresets = usePermission('manage_presets')`.
  - `isAdminRoute` already covers the nested routes through its prefix match.
- **Routes.** `app.tsx` adds a `RequirePermission permission="manage_presets"` layout route with the
  children `admin/presets`, `admin/presets/new` and `admin/presets/:id/edit`. There is no
  read-only `admin/presets/:id` route: see "Criteria dialog" below.
- **Shared components**, in `src/app/features/search/components/`:
  - `search-criteria-form.tsx` exports `SearchCriteriaForm`, the current `SearchFilters` body.
    - It keeps `filters`, `onFiltersChange` and the internal criteria drafts.
    - It gains `onSubmit?`, an `actions: ReactNode` slot rendered where Buscar/Limpiar are today,
      and an optional `collapsible` (`collapsed`/`onCollapsedChange`).
    - When not collapsible, the toggle is not rendered and the body is always shown.
    - Every `name=`, `id`, `data-status` and `data-testid` is preserved.
  - `search-criteria-summary.tsx` exports `SearchCriteriaSummary({ filters })`. It calls
    `buildSummaryGroups` itself and keeps `data-testid="filters-summary"`.
  - `search-criteria.logic.ts` holds the status options, as keys resolved through `t()`, and the
    empty-draft constants.
  - `search-filters.tsx` and `filters-summary.tsx` are removed rather than kept as wrappers, so
    that "one component" is literally true. Their importers are updated.
- **`AdvancedSearchPage`.**
  - Renders `SearchCriteriaForm` with the collapse props and Buscar/Limpiar as `actions`.
  - Loses `presetName`, `savePreset` and `deletePreset`, and gains a conditional
    `<Link to="/app/admin/presets">`.
  - On a 404 from `applyPreset`, the selection is cleared and the filters are left untouched;
    this is already the behaviour of the existing `catch`.
- **Pages**, in `src/app/features/admin/presets/`:
  - `preset-list-page.tsx` + `preset-list-page.css` + `preset-list.logic.ts`. The logic file
    holds pure `filterPresets`, `sortPresets` and `paginate`, reusing
    `candidate-list.logic.ts` helpers where they are generic.
  - `preset-edit-page.tsx` handles create and edit. It loads by id through the service, renders
    the name field (`name="presetName"`, `data-testid="preset-name"`), the privacy hint, and
    `SearchCriteriaForm` with Guardar/Cancelar as actions.
    Saving or cancelling returns to the list.
- **Criteria dialog** (replaces both the read-only detail page and a criteria column; decided
  with the product owner after the first implementation). A preset is a name plus a filter set,
  so a separate page duplicated its list row and cost a navigation; a full summary in every row
  made the table tall.
  - `search-criteria-dialog.tsx` exports `SearchCriteriaDialog({ title, filters, onClose,
details?, actions? })` in the search components, beside the form and summary it belongs
    with. It renders `SearchCriteriaSummary` inside the shared `.overlay`/`.dialog` modal styles,
    between the host's `details` and `actions`.
  - Behaviour: focus moves to its close button on open, Tab is kept inside, Escape and a click
    on the overlay close it, and focus returns to the opener. `onClose` is held in a ref so a
    host's inline closure does not re-run the focus effect.
  - The preset list shows name, update time, last use and actions. An icon-only eye button
    beside the name (`data-testid="preset-view"`, accessible name «Ver criterios de {nombre}»)
    opens the dialog with the timestamps as `details` and «Editar» as the action.
  - The same component is intended for Búsqueda (applying) and KTL-15 positions, each with its
    own details and actions. Wiring it into those sections is outside this change.
  - _Alternative considered:_ a side drawer. Rejected as more to build, and awkward on the
    search page where the filters already occupy the width.
- **`SearchPresetsService`.**
  - Gains `get(id)`, and `updatePreset(id, name, filters, version)` and
    `removePreset(id, version)`, which send `version`.
  - Name validation throws `TranslatableError('presets.errors.nameRequired')` before calling the
    API. The server remains the authority.
  - Every mutation reloads the list, as today.
  - Owner-related comments are rewritten.
- **Error rendering.** API problems with `search_preset.name.conflict` and
  `search_preset.concurrency.conflict` map to `presets.errors.nameConflict` and
  `presets.errors.staleVersion` keys. The pages render both through `errorText(err, t)`, which
  reuses the transport's existing `AppError` code.
- **Copy.** All new and moved copy goes to `es.json` under `presets.*`, `search.criteria.*` and
  `nav.presets`. `search-filters.tsx`, `filters-summary.tsx` and `advanced-search-page.tsx`, plus
  `criteria-group.tsx` if its copy is touched, leave `LEGACY_HARDCODED_COPY`. The nav label
  `'Presets'` stays in `nav-items.ts` like its siblings: that file is data, not JSX, and is not
  linted for copy.

_Alternative considered:_ place the shared components in `src/app/shared/`. Rejected because they
depend on the search model and the catalogs feature. They are search-owned components consumed by
an admin page, the same dependency direction admin import already has on candidates.

### D10. Test strategy

- **Backend unit** (`SearchHandlerTests`, hand-written doubles):
  - guard matrix per handler;
  - name validation;
  - version expectation passed to the repository;
  - `MarkUsed` not touching `Version` or `UpdatedAtUtc`;
  - accent-folded normalization.
- **Backend integration** (`SearchApiTests`, `SearchSchemaTests`, Testcontainers):
  - the per-route 401/403 matrix for unauthenticated, read-only and manage-only actors, including
    malformed bodies;
  - cross-actor visibility;
  - name conflict on `Inglés B2`/`ingles b2`;
  - version conflict on PUT and DELETE;
  - apply-then-save succeeding;
  - migration on seeded owner rows leaving the table empty with no `OwnerId`;
  - index and check definitions;
  - exact grants.
- **Naming.** `DatabaseNamingTests` covers the new index and check names.
- **Frontend unit.**
  - Existing specs updated: nav-items, primary-nav, the service, the search page, and the role
    service.
  - New specs for the two shared components (behaviour parity with the old ones) and the three
    pages, using `<ServicesProvider>` doubles.
  - One spec renders the search page and the edit page and asserts that both contain the
    `SearchCriteriaForm` output (same `name` set and test ids).
- **E2E.**
  - `advanced-search-presets.spec.ts` is rewritten around Admin › Presets CRUD plus apply in
    Búsqueda, keeping the legacy-browser-data test.
  - `navigation-responsive.spec.ts` asserts the new child at 1280 and 390 px.
  - Selectors use roles, test ids or `name`, never Spanish text.
- **Security.** The API integration matrix is the fail-closed evidence. The legacy
  `npm run security:rls`/`security:storage` suites are run unchanged.

## Risks / Trade-offs

- [Discarding existing presets loses users' work] → The release notes and the `docs/ktl-14`
  runbook announce it before deploy. An operator who wants a record can export the table under
  the migrator role beforehand; the runbook describes how but does not require it.
- [Shared names and free text may carry personal data to a wider audience] → An editor hint, plus
  names and filters are never logged. Administrators are a small, trusted group, and discarding
  the private presets avoids exposure by migration.
- [Integer version diverges from the `xmin` convention and could be forgotten in a later write
  path] → All writes go through the domain methods that increment it. Integration tests cover a
  stale PUT and DELETE, and the configuration carries a comment pointing to D2.
- [Refactoring the search filters could regress search behaviour] → The existing
  `advanced-search-page.spec.tsx` and `advanced-search.spec.ts` stay green without selector
  changes, and the component specs are ported before the old files are deleted.
- [`manage_presets` without `view_candidates` yields an admin page that cannot list] → The route
  guard only checks `manage_presets`, so the list shows the transport's 403 as an error state. The
  default roles hold both. This is documented in `docs/ktl-14/presets.md`.
- [Frontend `DEFAULT_ROLES` and backend `DevelopmentActor` drift] → Both are updated in the same
  task group, and the role service spec asserts the permission list.

## Migration Plan

1. **Before deploy.** Publish `docs/ktl-14/release-notes.md` announcing that saved searches will be
   reset and that administrators recreate shared ones.
2. **Deploy.** Build the images, then run the `migrator` container (`--migrate`), which applies
   `ShareSearchPresets`. Then start the API and nginx with the new bundle.
3. **Verify.**
   - `GET /api/search-presets` returns `[]`.
   - A development actor can create, get, apply, update and delete a preset.
   - A read-only actor receives 403 on writes.
   - The schema test grants hold.
4. **Rollback.** Restore the previous API and frontend binaries first, then run the migration's
   `Down` if the old binaries must run. `Down` leaves an empty owner-scoped table, and the
   discarded KTL-10 presets are not recoverable. Never broaden grants or re-enable browser preset
   storage as a rollback shortcut.

## Open Questions

None that affect the specs or tasks. The compact criteria column's final CSS can be adjusted
during implementation (D9).
