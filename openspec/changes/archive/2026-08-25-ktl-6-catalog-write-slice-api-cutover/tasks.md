## 0. Create Feature Branch

- [x] 0.1 Create and check out `feat/KTL-6` from an up-to-date `main`.

## 1. Domain and persistence

- [x] 1.1 Add `Domain/Catalogs/CatalogItem.cs` with `Id`, `Family`, `Code`, `NameEs`,
      `NameNormalized`, `NameEn`, `SortOrder`, `IsActive`, `Version`, plus the closed
      `CatalogFamily` set covering the nine business families. (Spec: catalog families and
      item shape.)
- [x] 1.2 Add the normalization function (trim, case-fold, strip accents) used for
      `NameNormalized`, with unit tests over the Spanish defaults (`Inglés`/`ingles`,
      `Tecnología`, `Básico`). (Spec: names are unique within a family.)
- [x] 1.3 Add `Infrastructure/Persistence/Configurations/CatalogItemConfiguration.cs`:
      `CAT_CatalogItems` with quoted uppercase naming, a check constraint on `Family`, unique
      indexes on `(Family, NameNormalized)` and `(Family, Code)`, and `Version` as row version.
      Register the `DbSet` on `ApplicationDbContext`.
- [x] 1.4 Generate the explicit EF migration for `CAT_CatalogItems` and add the runtime-role
      grants (`SELECT, INSERT, UPDATE`, no `DELETE`) to the same migration. (Design: least
      privilege grants; principle 3.)
- [x] 1.5 Extend `DatabaseNamingTests` to cover the new table and confirm it passes.
- [x] 1.6 Add `DatabaseInitializer.SeedCatalogsAsync` seeding the nine families from the
      Spanish defaults, per-family only when that family has no rows, and invoke it from the
      existing startup seed path. (Spec: default families are seeded at deployment.)

## 2. Authorization and audit foundations

- [x] 2.1 Add `CatalogsRead = "catalogs.read"` and `CatalogsManage = "catalogs.manage"` to
      `Permissions` in `Application/Abstractions/Identity/ICurrentActor.cs`.
- [x] 2.2 Grant both capabilities in `Web/Identity/DevelopmentActor.cs`, leaving the production
      fail-closed behavior untouched.
- [x] 2.3 Add the catalog audit event types (`catalog.created`, `catalog.updated`,
      `catalog.reordered`, `catalog.activation_changed`) and a helper that writes an
      `AuditEvent` with the actor, subject, and `ICorrelationContext` correlation id in the
      same transaction as the change. (Spec: catalog changes are audited.)

## 3. Read slice

- [x] 3.1 Add `Application/Features/Catalogs/ListCatalogFamily.cs` (query, validator, handler)
      returning a family's items ordered by `SortOrder`, filtering inactive unless
      `includeInactive` is set, requiring `catalogs.read`, and rejecting unknown families with
      `catalog.family.invalid`. (Spec: inactive values are excluded by default.)
- [x] 3.2 Add `Web/Features/Catalogs/CatalogEndpoints.cs` with
      `GET /api/catalogs/{family}?includeInactive=`, documented statuses and problem shapes.
- [x] 3.3 Unit-test the list handler: ordering, inactive filtering, unknown family, missing
      permission, unauthenticated actor.

## 4. Write slices

- [x] 4.1 Add `CreateCatalogItem` (validator: name required → `catalog.name.required` with
      "El nombre es obligatorio."; code derivation and per-family uniquifying) creating the
      item active at the end of the family order, requiring `catalogs.manage`. Translate the
      unique-index violation to `catalog.name.duplicate` with "Ya existe un valor con ese
      nombre." (Spec: catalog value creation; names are unique within a family.)
- [x] 4.2 Add `UpdateCatalogItem` changing `NameEs`, `NameNormalized`, `Code`, and `NameEn`
      only, honouring the row version and returning `catalog.concurrency.conflict` on a stale
      write and `catalog.not_found` with "No se encontró el elemento del catálogo." for a
      missing item. (Spec: catalog value update.)
- [x] 4.3 Add `ReorderCatalogFamily` taking the complete ordered identifier list for the family,
      rewriting `SortOrder` as consecutive integers in one transaction, and rejecting an
      incomplete or foreign list with `catalog.reorder.incomplete`. (Spec: catalog family
      reordering.)
- [x] 4.4 Add `SetCatalogItemActive` performing logical activation and deactivation only, with
      the server-side in-use lookup for the referential rule (inert until KTL-8, commented as
      such). (Spec: values are deactivated, never deleted; values in use are protected.)
- [x] 4.5 Map `POST /api/catalogs/{family}`, `PUT /api/catalogs/{family}/{id}`,
      `PUT /api/catalogs/{family}/order`, and `PUT /api/catalogs/{family}/{id}/active`. Confirm
      no `DELETE` verb is exposed anywhere in the catalog contract.
- [x] 4.6 Unit-test each write handler: happy path, validation codes and Spanish messages,
      duplicate name, concurrency conflict, not found, incomplete reorder, missing permission,
      unauthenticated actor.
- [x] 4.7 Review and update existing backend unit tests affected by the `Permissions` and
      `DatabaseInitializer` changes (`GetReferenceCandidateTests`, architecture tests, any
      startup/seed tests).

## 5. Backend integration and security evidence

- [x] 5.1 Add `Tests/IntegrationTests/CatalogApiTests.cs` on the existing disposable PostgreSQL
      fixture covering the full HTTP → Application → PostgreSQL path: list, create, rename,
      reorder, deactivate, reactivate, and verify the resulting rows.
- [x] 5.2 Add an integration test that concurrent creation of the same normalized name in a
      family yields exactly one success and one `catalog.name.duplicate`.
- [x] 5.3 Add integration tests that catalog operations fail closed for an unauthenticated
      actor and for an actor holding only `catalogs.read`, returning no data and storing no
      change. (Principle 3, principle 4.)
- [x] 5.4 Add an integration test asserting the runtime database role cannot `DELETE` from
      `CAT_CatalogItems`.
- [x] 5.5 Add an integration test that the seed is idempotent: run it twice plus an
      administrator edit in between, and confirm the edit survives.
- [x] 5.6 Run the backend unit, architecture, and integration suites (`dotnet test`) and inspect
      the output; verify the resulting PostgreSQL state.

## 6. Frontend service cutover

- [x] 6.1 Add `src/app/features/catalogs/services/catalog.api.ts` (or equivalent) issuing the
      five catalog calls through the shared `ApiTransport`.
- [x] 6.2 Rewrite `CatalogService` onto the load-state signal from `design.md`: `status`,
      `items`, `error`; `list()` and `activeNames()` remain synchronous reads returning `[]`
      while loading; write methods await the API and refetch the affected family. Remove
      `remove()`, the `rrhh-catalogs` key, and the default-seed fallback. (Spec: server-owned
      catalog vocabulary; asynchronous feature data loading.)
- [x] 6.3 Update `useCatalogs` to trigger the load on first use and keep the subscription
      pattern intact; keep `ServicesProvider` as the component-facing seam and confirm no
      component calls `fetch`.
- [x] 6.4 Update `catalog-management-page.tsx`: loading and error states, the delete control
      replaced by deactivate, up/down buttons submitting the full family order, and Spanish
      copy and accents preserved.
- [x] 6.5 Update the six consuming components (`candidate-languages`, `candidate-programs`,
      `candidate-skills`, `candidate-education`, `candidate-experience`, `search-filters`) to
      branch on the loading and error states instead of rendering an empty option list as a
      complete result.
- [x] 6.6 Keep the local in-use guard as a transitional pre-check, marked in code as removable
      at KTL-8. (Design: referential protection given candidates are not yet server-side.)
- [x] 6.7 Verify `rrhh-catalogs` no longer appears anywhere under `src/` (repo-wide search).

## 7. Frontend tests

- [x] 7.1 Review and update existing Vitest suites affected by the `CatalogService` rewrite and
      the removal of `remove()`, including any test doubles registered through
      `ServicesProvider`.
- [x] 7.2 Add Vitest coverage for the rewritten service: load success, load failure surfacing
      the Spanish `AppError` message, create/update/reorder/activation refetching the family,
      and duplicate-name error propagation.
- [x] 7.3 Add Vitest coverage that a consuming component renders its loading branch and its
      error branch rather than an empty option list.
- [x] 7.4 Run the frontend unit suite and inspect the output.

## 8. End-to-end evidence

- [x] 8.1 Write a targeted Playwright catalog spec: open catalog administration, create a value,
      rename it, reorder it, deactivate it, confirm it disappears from a candidate form's
      options, reactivate it, and confirm the duplicate-name error message appears with correct
      accents.
- [x] 8.2 Run the catalog spec (`npm run e2e -- <catalog spec>`) against the running stack and
      inspect the result.
- [x] 8.3 Restore seed data afterwards so the environment is left as found.

## 9. Documentation and gates

- [x] 9.1 Update `README.md` / `docs/` with the catalog endpoints, the capability codes, the
      seed behavior, and the note that existing `rrhh-catalogs` browser data is abandoned.
- [x] 9.2 Regenerate and check in the API contract so it shows the catalog routes, problem
      shapes, and the absence of a delete verb.
- [x] 9.3 Run `npm run lint` and `npm run format:check` and fix anything they report.
- [x] 9.4 Re-run the full backend and frontend suites once more after documentation and lint
      fixes, and confirm every acceptance criterion in `proposal.md` — Success criteria has
      evidence.
