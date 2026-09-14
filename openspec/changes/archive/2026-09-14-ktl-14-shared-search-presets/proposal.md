## Why

Saved searches are private to each recruiter (KTL-10), so every person rebuilds the same common
searches and nobody can curate a consistent set. HR wants one shared, administrator-maintained
preset library, managed under Admin like catalogs, and reuses the advanced search's criteria form
and summary so both screens evolve together (brief: `openspec/KTL-14.md`).

## What Changes

- **BREAKING** Presets become a single organization-wide library. They are no longer owned by an
  actor, and every holder of `view_candidates` lists and applies the same presets.
- **BREAKING** Creating, updating and deleting presets requires a new `manage_presets` permission
  (`presets.manage` in the API) instead of `view_candidates`.
- **BREAKING** The presets already stored in `ADM_SearchPresets` are discarded by the migration.
  An administrator must recreate them.
- Preset names become unique across the whole library, compared without regard to case or
  accents.
- Updates and deletes carry an optimistic-concurrency version, and a stale version is refused
  with a conflict. Applying a preset records its last use without changing that version.
- New read endpoint for a single preset. The existing routes under `/api/search-presets` keep
  their paths.
- New **Admin › Presets** section:
  - a list page;
  - create and edit pages at their own routes, and a compact modal (opened from an eye icon
    beside each name) to view a preset's criteria without leaving the list;
  - a navigation child gated by `manage_presets`.
- **BREAKING (UI)** Búsqueda keeps only the preset picker. Saving and deleting presets move to
  Admin › Presets. Holders of `manage_presets` see a link to that section.
- The advanced search's criteria form and its read-only criteria summary become single shared
  components, rendered by both Búsqueda and the preset pages. Their look and behaviour do not
  change; improving them is a later ticket.
- Presets are still physically deleted. This is not a departure from the no-destructive-delete
  rule, which covers candidates and catalog items. The design records the reasoning.

**Actors:**

- HR administrators holding `manage_presets`, who curate the library.
- Recruiters holding `view_candidates`, who apply presets.
- Unauthenticated or unauthorized callers, who are refused without data.

**Key entities:** search preset (name, complete normalized search filters, created/updated/last-used
timestamps, version); the `manage_presets` permission.

**Assumptions:**

- Default roles `rrhh_admin` and `system_admin` receive `manage_presets`.
- The KTL-10 filter contract, normalization and the local last-filters convenience are unchanged.
- KTL-10 presets are development-era data whose loss is acceptable.

**Edge cases:**

- Two administrators edit the same preset concurrently.
- A preset is deleted while a recruiter has it selected.
- A name differs from an existing one only in case or accents.
- A stored filter document can no longer be understood.
- An actor holds `manage_presets` without `view_candidates`.

**Success criteria:**

- Every preset route fails closed for unauthenticated, read-only and manage-only callers
  according to its permission, proven by API integration tests.
- A preset created by one actor is listed and applied identically by another.
- Stale writes and case- or accent-equivalent names are refused without partial changes.
- The migration leaves an empty, owner-free table with unchanged runtime grants.
- Unit tests show that Búsqueda and the preset editor render the same criteria component.
- Playwright covers create, edit, delete in Admin and apply in Búsqueda.
- Lint and format pass.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `saved-search-presets`:
  - owner-scoped presets are replaced by a shared library;
  - writes require `manage_presets`;
  - adds names unique across the library, optimistic concurrency, single-preset retrieval,
    last-use tracking that leaves the version alone, and the discarding of existing presets;
  - adds the Admin › Presets screens, apply-only preset use in Búsqueda, and the shared criteria
    editor and summary.
- `primary-navigation`: adds the `Presets` administration child, governed by `manage_presets`, to
  the entry list and the Admin group.

## Impact

- **Backend:**
  - `Application/Abstractions/Identity` gets the new permission constant; `DevelopmentActor`
    grants it;
  - the `Search` slice: preset commands, queries, guards and errors;
  - `ISearchPresetRepository` and its implementation;
  - the `SearchPreset` domain type and its EF configuration;
  - a new EF Core migration on `ADM_SearchPresets`;
  - `SearchEndpoints`.
- **Frontend:**
  - `auth.models.ts` (permission and default roles), `nav-items.ts`, `primary-nav.tsx`,
    `app.tsx`;
  - a new `features/admin/presets/` pages folder;
  - search components refactored into the shared criteria form and summary;
  - `SearchPresetsService`, the search models and `AdvancedSearchPage`;
  - `es.json` and the `LEGACY_HARDCODED_COPY` list (it shrinks).
- **Tests:**
  - backend handler, schema, naming and API integration tests;
  - Vitest specs for navigation, the service, the search page, the new pages and the shared
    components;
  - Playwright preset and navigation specs.
- **Docs:** new `docs/ktl-14/`, `docs/ktl-10/search.md` marked as superseded for presets, and
  `README.md`.
- **Personal data, storage and roles:**
  - This change touches personal data: preset names and free-text filters can contain it, and
    they are now visible to every `view_candidates` holder rather than to one owner.
  - Principle 1 is upheld as follows:
    - responses carry no actor identifiers;
    - names and filters stay out of logs;
    - the editor warns administrators not to put personal data in presets;
    - discarding existing private presets avoids exposing search terms their owners never meant
      to share.
  - Principle 3 is upheld as follows:
    - every route checks authentication and its specific permission before validating or
      dispatching;
    - runtime grants on `ADM_SearchPresets` are not broadened;
    - the migration runs under the migrator role;
    - the browser still reaches presets only through the API.
  - Role definitions change: a new permission is added and granted to two default roles.
  - No RLS policy and no document storage access changes.
- **Dependencies:** no new npm or NuGet packages.
