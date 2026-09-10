# KTL-8 — Candidate core writes, logical deletion, and `CandidateService` cutover

**Status:** Ready for OpenSpec planning
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (write conventions, catalogs), KTL-7 (real data to cut over to)
**Blocks:** KTL-9, KTL-10

## Summary

Move the candidate aggregate root from `localStorage` to PostgreSQL: expand the domain
entity to the full field set, implement create/update/logical-delete slices, and rewrite
[candidate.service.ts](src/app/features/candidates/services/candidate.service.ts) to call
the API instead of the `rrhh-candidates` key.

This is the core business-data ticket of the migration.

## Why

`rrhh-candidates` currently holds the entire candidate table — identity, contact,
location, consent and retention metadata, status, source, notes — as one JSON blob in the
browser, per device, in clear. It is review finding `C-4`, open since KTL-3 and
deliberately not fixed by either the React migration or KTL-5. This ticket closes it for
the core record.

The backend entity is currently a stub:
[Candidate.cs](backend/Domain/Candidates/Candidate.cs) carries only `Id`, `FirstName`,
`LastName`, `IsActive`, timestamps, `DeletedAtUtc` and `Version` — enough for KTL-5's
read-only reference slice, not enough for the product. Expanding it is the bulk of the
schema work here.

## In scope

### Backend

- Expand the `Candidate` domain entity and `CND_` mapping to the full field set already
  modelled in [candidate.models.ts](src/app/features/candidates/models/candidate.models.ts):
  `firstName`, `lastName`, `phone`, `email`, `location`, `province`, `country`,
  `availability`, `status`, `source`, `notes`, `receivedAt`, `consentAt`, `reviewDueAt`,
  `isActive`, `createdAt`, `updatedAt`.
- `CandidateStatus` as a constrained value (`new`, `available`, `in_process`, `hired`,
  `rejected`) — enforced by a check constraint or lookup, not by convention.
- Slices: create, update, read one, list, and **logical delete**.
- Logical deletion sets inactive/deleted state and `DeletedAtUtc`; no endpoint physically
  deletes a candidate in normal operation. Default queries exclude logically deleted rows.
- Optimistic concurrency using the existing `Version` column; a conflicting write returns
  a `409` problem with a stable code.
- Indexes for the access paths the list and detail screens use.
- Audit events (`AUD_`) for create, update, status change, and logical delete, recording
  actor and correlation, never field values that are personal data.
- Extend the capability catalogue with `candidates.create`, `candidates.update`,
  `candidates.delete` (and the existing `candidates.read`), mapped to the frontend
  `create_candidates`, `edit_candidates`, `delete_candidates`, `view_candidates`
  permissions.
- Retire the KTL-5 `/api/reference/candidates/{id}` template slice, or fold it into the
  real read slice — it should not survive as a parallel path.

### Frontend

- Rewrite `CandidateService` against the shared transport; remove the `rrhh-candidates`
  key and the `demo-1` seed fallback.
- Resolve the synchronous-access break. `find(id)` returns `Candidate | undefined`
  synchronously today and is relied on by
  [candidate-relations.service.ts](src/app/features/candidates/services/candidate-relations.service.ts)
  and [document.service.ts](src/app/features/documents/services/document.service.ts).
  Those two services are **not** migrated here (KTL-9) but must keep working, so this
  ticket must define how they read candidate state during the interim.
- Loading and error states on the candidate list and detail screens, following the
  pattern KTL-6 established.

## Out of scope

- Candidate relations and documents (KTL-9) — the nested collections keep their current
  behaviour until then.
- Search and saved presets (KTL-10).
- Authentication. The KTL-5 development actor remains the actor in development;
  production endpoints continue to fail closed without a real actor.
- Retention/purge automation. The seam is preserved, the job is not built.

## Personal-data and security impact

**High.** This ticket relocates the primary store of candidate personal data.

- Principle 1: consent metadata (`consentAt`), retention metadata (`reviewDueAt`,
  `receivedAt`) and logical-deletion state must survive the move exactly. No field is
  dropped, and no field gains a permissive default.
- Serilog redaction must cover the new fields; candidate names, contact details and notes
  must not reach logs.
- Principle 3: every write endpoint fails closed for unauthenticated and unauthorized
  actors. The least-privilege runtime database role ships with the migration.
- Once `rrhh-candidates` is no longer written, existing browsers still hold a stale copy.
  The cutover must **actively clear** the key, not merely stop using it — otherwise the
  ticket leaves personal data sitting in clear on every user's device.

## Acceptance criteria

1. Candidate create, update, read, list and logical delete succeed through the real HTTP
   → Application → PostgreSQL path.
2. An explicit migration creates the expanded `CND_` mapping with quoted uppercase names,
   constraints and indexes.
3. No endpoint physically deletes a candidate; logically deleted candidates are excluded
   from default reads and remain recoverable.
4. A concurrent update returns `409` with a stable code, and the UI surfaces it in
   Spanish.
5. Consent and retention metadata round-trips unchanged.
6. The `rrhh-candidates` key is no longer read or written in `src/`, and is actively
   cleared from browsers on first run of the new build.
7. Candidate screens behave identically from the user's point of view, including Spanish
   copy, validation messages and accents.
8. Candidate personal data does not appear in application logs.
9. Authorization fails closed for each of the four candidate permissions.
10. Backend unit, integration (real disposable PostgreSQL), architecture, security and
    frontend tests pass, plus the candidate Playwright flows.

## Deferred decisions

- How relations and documents read candidate state between this ticket and KTL-9.
  Options: ship them together (larger change), or have the interim services read through
  the API-backed `CandidateService` cache. Decide in design — do not reintroduce a
  `localStorage` read path.

## Next step

```
/enrich-us openspec/KTL-8.md
/opsx:new
```
