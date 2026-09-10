## Why

`rrhh-candidates` holds the entire candidate table — identity, contact details, location,
consent and retention metadata, status, source and notes — as one JSON blob in every user's
browser, per device, in clear. That is review finding `C-4`, open since KTL-3 and deliberately
left unfixed by both the React migration and KTL-5. It is the largest remaining personal-data
exposure in the product, and it cannot be closed by anything smaller than moving the candidate
aggregate itself.

KTL-6 settled the write conventions on catalogs (validation codes, optimistic concurrency,
auditing, logical deactivation, the shape of an API-backed feature service) precisely so that
this change could apply them to the aggregate where mistakes are expensive. KTL-7 puts real
candidate data into PostgreSQL. This change makes the application read and write it.

## What Changes

### Backend

- New vertical slices over the candidate aggregate delivered by KTL-7: create, update, read
  one, list, and **logical delete**. No slice physically deletes a candidate, and default reads
  exclude logically deleted rows while leaving them retrievable by identifier.
- Relation collection writes for languages, programs, education, experience and skills, and
  document **metadata** writes (attach, set primary, detach). Each collection is replaced as a
  whole set against the owning candidate's concurrency token, so a stale editor cannot
  interleave a partial collection.
- Optimistic concurrency on every candidate write through the existing `Version` row version; a
  conflicting write returns `409` with a stable code that the SPA surfaces in Spanish.
- `AUD_` audit events for create, update, status change, logical delete, relation change and
  document metadata change, recording actor, subject identifier and correlation — never field
  values, which are personal data.
- The capability catalogue gains `candidates.create`, `candidates.update`, `candidates.delete`
  alongside the existing `candidates.read`, enforced server-side in every slice and mapped onto
  the frontend `create_candidates`, `edit_candidates`, `delete_candidates`, `view_candidates`
  permissions.
- **BREAKING** (internal API): the KTL-5 `/api/reference/candidates/{id}` template slice is
  retired. Its purpose — proving the read path — is now served by the real read slice, and a
  parallel path to candidate personal data must not survive.
- Serilog redaction is extended to the candidate field set, so names, contact details and notes
  cannot reach logs.

### Frontend

- **BREAKING** (internal API): `CandidateService` is rewritten against the shared
  `ApiTransport`. The `rrhh-candidates` key and the `demo-1` seed fallback are removed, and the
  service exposes loading / loaded / failed state following the pattern KTL-6 established.
- The synchronous-access break is resolved with a **read-through aggregate cache**:
  `CandidateService` holds the loaded aggregates in a signal, `find(id)` stays a synchronous
  read of that cache, and every mutation awaits the API and refreshes the cache. Consumers keep
  their existing shape.
- **BREAKING** (scope): `CandidateRelationsService` and `DocumentService` are cut over in this
  change rather than in KTL-9. They keep their validation rules and Spanish messages, but
  persist through the API instead of mutating a browser blob. This resolves the brief's
  deferred decision — see `design.md`. Real file upload, scanning, quarantine and secure
  download remain KTL-9; only document metadata moves here.
- The candidate list and detail screens gain loading and error states, and the `409` conflict is
  surfaced in Spanish.
- The stale `rrhh-candidates` key is **actively cleared** on first run of the new build, not
  merely abandoned.

## Capabilities

### New Capabilities

- `candidate-management`: the candidate aggregate's application behavior — create, update, read,
  list, logical delete, relation and document-metadata collection writes, permitted status
  transitions, optimistic concurrency, per-operation authorization, and auditing that records
  who changed what without recording personal data.

### Modified Capabilities

- `frontend-api-transport`: the "Incremental persistence cutover" requirement (amended by KTL-6
  to a per-slice cutover) still names candidates among the services retaining legacy behavior.
  It gains a candidate scenario. A new requirement is added for **eviction of superseded browser
  storage**, because abandoning a personal-data key is not the same as removing it, and one for
  the synchronous read-through cache that keeps existing consumers working.
- `business-catalogs`: "Values in use are protected" declares its screen-level refusal
  transitional, "removed once the API can evaluate the rule itself". This is that moment. It is
  replaced by "Referenced values remain resolvable", which keeps every protection that was not
  transitional and drops only the refusal — deactivating a referenced value now succeeds.
- `backend-platform`: the KTL-5 "Reference vertical slice" requirement is removed. The real
  candidate read operation now travels the same boundaries under per-operation authorization,
  so a second, less-guarded route to candidate personal data has no reason to survive.

## Impact

- **Backend**: `Domain/Candidates/`, `Application/Features/Candidates/` (new slices; the
  reference slice removed), `Application/Abstractions/Identity/ICurrentActor.cs` (`Permissions`),
  `Application/Abstractions/Persistence/` (candidate repository), `Infrastructure/Persistence/`,
  `Web/Features/Candidates/` (endpoints; `ReferenceCandidateEndpoints.cs` removed),
  `Web/Program.cs` (Serilog redaction), backend unit / architecture / real-PostgreSQL
  integration tests.
- **Frontend**: `src/app/features/candidates/` (service, relations service, models, list and
  detail and edit pages, section components), `src/app/features/documents/services/`,
  `src/app/core/di/services.ts`.
- **Database**: no new tables. This change consumes the `CND_` schema, constraints, indexes and
  runtime grants delivered by KTL-7, and adds no schema of its own.
- **Tests**: Vitest suites for the rewritten services and the async-consuming screens; xUnit
  unit, architecture and disposable-PostgreSQL integration tests; the candidate Playwright
  flows.
- **Docs**: `README.md` / `docs/` and the published API contract.

### Dependencies and sequencing

- **KTL-6** (`catalog-write-slice-api-cutover`) — delivered. Supplies the write conventions,
  the API-backed feature service pattern, and the catalog values relation writes validate
  against. This change also removes the transitional local in-use guard KTL-6 left in
  `CatalogService`, which was explicitly marked for removal here.
- **KTL-7** (`ktl-7-access-to-postgres-migration`) — **hard prerequisite, still in planning.** KTL-7
  owns the `CND_` schema and its migration; this change deliberately does not restate or
  re-own it, so that one migration has one owner. If KTL-7 has not landed when implementation
  starts, this change is blocked rather than duplicating the schema.

### Personal data and security

**High.** This change relocates the primary store of candidate personal data, so principle 1 is
the governing constraint rather than a checklist item.

- **Principle 1.** Consent metadata (`consentAt`), retention metadata (`receivedAt`,
  `reviewDueAt`) and logical-deletion state survive the move exactly: no field is dropped, and
  no field acquires a permissive default — an absent consent date stays absent rather than
  becoming today's date. Logical deletion remains the only removal in normal operation, and a
  logically deleted candidate stays recoverable. Audit events record actor, subject and
  correlation, never field values. Serilog redaction covers the new fields.
- **The stale key.** Once the SPA stops writing `rrhh-candidates`, every existing browser still
  holds a full clear-text copy of the candidate table. The cutover therefore removes the key on
  startup. Stopping at "we no longer write it" would leave the exposure this change exists to
  close sitting on every user's device.
- **Principle 3.** All four candidate capabilities fail closed for unauthenticated and
  unauthorized actors; hiding a control is never the access control. The runtime database role
  holds no `DELETE` on candidate tables, so "no physical delete" is enforced by PostgreSQL and
  not only by the absence of a verb. PostgreSQL stays unreachable from the browser.
  Authentication itself remains out of scope: the KTL-5 development actor stays
  development-only and production continues to fail closed without a real actor.
- **Retention automation** is out of scope. The metadata seam is preserved; the purge job is
  not built.

### Assumptions and edge cases

- The candidate aggregate is small enough (tens of fields, a handful of short collections) that
  the detail screen can load it whole, and the list screen can hold the active set in the
  read-through cache. No pagination or partial hydration is designed here; search and saved
  presets are KTL-10.
- Relation values reference `CAT_` catalog entries. Legacy free-text values that KTL-7 could not
  resolve are KTL-7's reported problem, not silently accepted here.
- A candidate logically deleted by one user while another edits them yields a conflict, not a
  resurrection: the delete moves the version forward like any other write.
- `deactivateMany` / `reactivateMany` (bulk list actions) become per-candidate API calls
  reporting a changed count, preserving today's behavior without adding a batch endpoint.
- Document records carry metadata only until KTL-9; no file bytes are stored or served by this
  change, and `createSecureUrl` keeps its placeholder behavior.

### Success criteria

1. Candidate create, update, read, list and logical delete succeed through the real
   HTTP → Application → PostgreSQL path.
2. No endpoint physically deletes a candidate; logically deleted candidates are excluded from
   default reads and remain retrievable by identifier.
3. A concurrent update returns `409` with a stable code, surfaced in Spanish by the UI.
4. Consent and retention metadata round-trip unchanged, and an absent consent date stays absent.
5. `rrhh-candidates` is neither read nor written anywhere under `src/`, and is actively removed
   from the browser on first run of the new build.
6. Candidate screens behave identically from the user's point of view, including Spanish copy,
   validation messages and accents.
7. Candidate personal data does not appear in application logs.
8. Authorization fails closed for each of the four candidate permissions.
9. The KTL-5 reference candidate slice no longer exists.
10. Backend unit, integration (real disposable PostgreSQL), architecture and security tests
    pass, plus the frontend suites and the candidate Playwright flows.
