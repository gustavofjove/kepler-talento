## Why

Business catalogs still live in the browser under the `rrhh-catalogs` `localStorage` key, so
every user's vocabulary is private to their browser, unauditable, and unenforceable. KTL-5
proved the read path through HTTP → Application → PostgreSQL with a reference slice; catalogs
are the smallest real aggregate in the product (a flat item across nine families, no nested
collections, no personal data), which makes them the cheapest place to settle the **write**
conventions — validation codes, concurrency, auditing, logical deactivation, and the shape of
an API-backed feature service — before those conventions reach candidates, where mistakes are
expensive. KTL-8 and KTL-10 draw their relation values and search criteria from catalog
families, so they cannot start until server-side catalogs exist.

## What Changes

### Backend

- New `CAT_` tables and EF Core configuration for catalog items (`Family`, `Code`, `NameEs`,
  `NameEn`, `SortOrder`, `IsActive`, row version), created by an explicit migration with
  quoted uppercase names, carrying a unique constraint on (family, normalized `NameEs`) and on
  (family, `Code`), plus the least-privilege runtime grants in the same migration.
- New vertical slices: list by family (including or excluding inactive), create, update
  (rename/code/English name), reorder, and activate/deactivate.
- Deactivation is logical (`IsActive = false`). No endpoint physically deletes a catalog item.
- Optimistic concurrency on writes via the row version, surfaced as a stable conflict problem.
- Stable validation codes and RFC 9457 problem details following the KTL-5 contract, with the
  Spanish messages matching today's frontend copy verbatim (including accents).
- `AUD_` audit events for create, update, reorder, and activation changes.
- Extend the capability catalogue in `Permissions` (today only `candidates.read`) with
  `catalogs.read` and `catalogs.manage`, mapped to the existing `manage_catalogs` frontend
  permission and enforced server-side in every slice.
- Seed the nine `DEFAULT_CATALOGS` families as an explicit, idempotent deployment-time seed —
  never as an implicit runtime fallback.

### Frontend

- **BREAKING** (internal API): `CatalogService` is rewritten to call the API through the shared
  `ApiTransport`. `list()` and `activeNames()` stop being synchronous reads of an in-memory
  signal; the service exposes a loaded/loading/error state that the six calling components
  (`candidate-languages`, `candidate-programs`, `candidate-skills`, `candidate-education`,
  `candidate-experience`, `search-filters`) and the catalog admin page consume. This
  establishes the loading/error pattern the later slices reuse.
- **BREAKING** (user-visible): the catalog admin "delete" action becomes a deactivation.
  `CatalogService.remove()` is removed, since no endpoint physically deletes an item.
- The `rrhh-catalogs` key and the in-memory default-seed fallback are removed; no code under
  `src/` reads or writes that key afterwards.
- `ServicesProvider` remains the component-facing seam; components continue to never call
  `fetch` directly, and test doubles keep working.

### Cutover model

This change records the decision that **each backend slice ships with its React service
already cut over to the API**, amending the blueprint's step 9 single terminal SPA cutover.
The reason (unconsumed endpoints are unproven integration debt; batching defers all risk into
one change), the simpler alternative considered, and the mitigation are documented in
`design.md`.

## Capabilities

### New Capabilities

- `business-catalogs`: server-owned catalog vocabulary — families, item lifecycle (create,
  rename, reorder, logical activation), uniqueness rules, referential protection against
  values in use, authorization, auditing, and the seeded default families.

### Modified Capabilities

- `frontend-api-transport`: the "Incremental persistence cutover" requirement is KTL-5-scoped
  and names catalogs among the services that retain existing behavior. It is replaced by a
  per-slice cutover requirement: each delivered backend slice cuts its own feature service
  over to the API in the same change, and catalogs are now API-backed while candidate, search,
  admin, import, and export services retain their existing behavior.

## Impact

- **Backend**: `Domain/Catalogs/`, `Application/Features/Catalogs/`,
  `Application/Abstractions/Identity/ICurrentActor.cs` (`Permissions`),
  `Infrastructure/Persistence/` (configuration, migration, seed, `DatabaseInitializer`),
  `Web/Features/Catalogs/`, backend unit/integration/architecture tests.
- **Frontend**: `src/app/features/catalogs/` (service, hook, models, admin page),
  `src/app/features/candidates/components/candidate-{languages,programs,skills,education,experience}.tsx`,
  `src/app/features/search/components/search-filters.tsx`, `src/app/core/di/services.ts`.
- **Database**: new `CAT_` tables, indexes, and runtime-role grants; deployment-time seed.
- **Tests**: Vitest suites for the rewritten service and the async-consuming components; xUnit
  unit, architecture, and real-PostgreSQL integration tests; a targeted Playwright catalog
  administration flow.
- **Docs**: `README.md` / `docs/` and the API contract for the new endpoints.

### Personal data and security

Catalog values are **business reference vocabulary, not personal data** — deliberately so, to
let the write conventions be settled without simultaneously carrying principle-1 obligations.
No candidate contact details, consent metadata, documents, or storage paths are touched, and
no RLS policy over personal data changes.

Principle 3 does apply and is upheld: `catalogs.read` / `catalogs.manage` fail closed for
unauthenticated and unauthorized actors (hiding the UI is never the control); the migration
ships the least-privilege runtime database role grants for the new `CAT_` tables in the same
slice; PostgreSQL stays unreachable from the browser; and the KTL-5 development actor remains
development-only, with production still failing closed without a real actor. Authentication
itself remains out of scope.

### Assumptions and edge cases

- Server-side candidates do not exist yet (KTL-8), so the API's "value in use" referential
  check has no populated relations to consult in this change. The frontend keeps its existing
  local in-use guard as a transitional pre-check so the admin screens behave unchanged; the
  server-side rule is implemented now and becomes fully load-bearing when KTL-8 lands.
- Existing `rrhh-catalogs` browser data is not migrated. Catalog values were per-browser and
  seeded from the same defaults; the deployment seed is the new source of truth.
- Name uniqueness is enforced on a normalized (trimmed, case-folded, accent-insensitive) form
  so `Inglés` and `ingles` collide, matching what the in-memory service enforced.
- Reordering a family is a single atomic operation over the whole family, not per-item swaps,
  so concurrent reorders cannot interleave into a corrupt order.

### Success criteria

1. Catalog CRUD succeeds through the real HTTP → Application → PostgreSQL path.
2. `CAT_` tables are created by an explicit migration with quoted uppercase names.
3. Duplicate names within a family are rejected with a stable validation code and today's
   Spanish message.
4. Deactivation is logical; no endpoint physically deletes a catalog item.
5. `rrhh-catalogs` appears nowhere under `src/`.
6. Catalog admin screens and the catalog-consuming candidate/search screens work from the
   user's point of view, with Spanish copy and accents intact.
7. `manage_catalogs` is enforced server-side; unauthorized and unauthenticated calls are
   denied with no data returned.
8. Backend unit, integration (real disposable PostgreSQL), and architecture tests pass, plus
   frontend tests and a targeted Playwright catalog flow.
9. `design.md` records the per-slice cutover departure from the blueprint's step 9.
