# KTL-6 — Catalog write slice and `CatalogService` cutover

**Status:** Ready for OpenSpec planning
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-5 (complete)
**Blocks:** KTL-8, KTL-10

## Summary

Move business catalogs from browser `localStorage` to the API, end to end: `CAT_` tables
in PostgreSQL, full CRUD vertical slices, and [catalog.service.ts](src/app/features/catalogs/services/catalog.service.ts)
rewritten to call the API instead of the `rrhh-catalogs` key.

KTL-5 proved the read path with a reference slice. KTL-6 is the equivalent proof for the
**write** path, and the first ticket that removes a `localStorage` key from operational
use.

## Why

Catalogs are the smallest real aggregate in the product: a flat `CatalogItem`
(`code`, `nameEs`, `nameEn?`, `sortOrder`, `isActive`) across nine families, with no
nested collections and no personal data. That makes them the cheapest possible place to
settle the write conventions — validation codes, concurrency, auditing, logical
deactivation, and the shape of an API-backed feature service — before those conventions
are applied to candidates, where mistakes are expensive.

Catalogs also gate the tickets that follow: candidate relation values and search criteria
are both drawn from catalog families, so KTL-8 and KTL-10 need server-side catalogs to
exist first.

## Cutover model (decision to record)

This ticket establishes that **each backend slice ships with its React service already
cut over to the API**, rather than deferring all frontend integration to a single late
change. The blueprint's [adoption sequence](./kepler-talento-stack-blueprint.md) step 9
implies a single terminal SPA cutover; that is amended here.

Reason: a slice whose endpoints have no consumer is unproven integration debt, and
batching it defers all risk into one change. The change's `design.md` must document this
departure with the reason and mitigation, per the `design` rule in
[openspec/config.yaml](./config.yaml).

## In scope

### Backend

- `CAT_` tables and EF Core configuration for catalog items, with a unique constraint on
  (family, normalized `nameEs`) and on (family, `code`), matching the duplicate-name rule
  the current service enforces in memory.
- Vertical slices: list by family (including/excluding inactive), create, rename/update,
  reorder, activate/deactivate.
- Deactivation is logical — `isActive: false`, never a physical delete.
- Referential rule: a catalog value that is in use by a candidate relation may be
  deactivated but not renamed to collide with another value, and not hard-deleted.
- Stable validation codes and RFC 9457 problem details, following the KTL-5 contract.
- Audit events (`AUD_`) for create, update, reorder, and activation changes.
- Extend the capability catalogue in
  [ICurrentActor.cs](backend/Application/Abstractions/Identity/ICurrentActor.cs) —
  currently only `candidates.read` — with the codes this slice needs
  (`catalogs.read`, `catalogs.manage`), mapped to the existing `manage_catalogs`
  frontend permission.
- Seed the nine families from `DEFAULT_CATALOGS` as a deployment-time seed, not as an
  implicit runtime fallback.

### Frontend

- Rewrite `CatalogService` to call the API through the shared
  [api-transport.ts](src/app/core/http/api-transport.ts); remove the `rrhh-catalogs` key
  and the in-memory default-seed fallback.
- Absorb the synchronous-to-asynchronous break: `list()` and `activeNames()` are called
  synchronously today by candidate forms and search. Establish the loading/error pattern
  the later slices will reuse.
- Keep `ServicesProvider` test doubles as the component-facing seam; components must not
  call `fetch`.

## Out of scope

- Candidate CRUD, relations, documents, and search (KTL-8, KTL-9, KTL-10).
- Authentication — the KTL-5 development actor remains the actor in development, and
  production business endpoints continue to fail closed without a real actor.
- Any other `localStorage` key.
- Catalog import/export.

## Personal-data and security impact

Catalog values are **not personal data** — they are business reference vocabulary. This
is deliberate: it lets the write conventions be settled without simultaneously carrying
principle-1 obligations.

Principle 3 still applies: `manage_catalogs` must fail closed for unauthenticated and
unauthorized actors, and the least-privilege runtime database role must ship with the
migration in the same slice.

## Acceptance criteria

1. Catalog CRUD succeeds through the real HTTP → Application → PostgreSQL path.
2. `CAT_` tables are created by an explicit migration with quoted uppercase names.
3. Duplicate names within a family are rejected with a stable validation code, and the
   Spanish message matches today's copy.
4. Deactivation is logical; no endpoint physically deletes a catalog item.
5. The `rrhh-catalogs` key is no longer read or written anywhere in `src/`.
6. Catalog admin screens work unchanged from the user's point of view, including Spanish
   copy and accents.
7. `manage_catalogs` is enforced server-side; hiding UI is not the control.
8. Backend unit, integration (real disposable PostgreSQL), architecture, and frontend
   tests pass, plus a targeted Playwright catalog flow.
9. The design records the per-slice cutover departure from the blueprint's step 9.

## Next step

```
/enrich-us openspec/KTL-6.md
/opsx:new
```
