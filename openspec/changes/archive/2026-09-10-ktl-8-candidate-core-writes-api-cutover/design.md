## Context

See `proposal.md` — Why. The constraints that shape this design:

- **KTL-6 supplied the write conventions and they are not renegotiated here.** MediatR slices
  under `Application/Features/`, `ValidationBehavior`, `ApplicationExceptions`,
  `GlobalExceptionHandler` producing RFC 9457 problems, `ICorrelationContext`, `ICurrentActor`
  with a `Permissions` catalogue, a repository interface that owns the concurrency check and
  writes the audit event in the same transaction (`ICatalogRepository.ExpectVersion` /
  `SaveAsync`), and on the frontend a signal-holding feature service exposing
  `status`/`error` with a `useX()` hook that binds subscription and load together. This change
  instantiates that pattern for candidates; anywhere it appears to invent machinery, it is
  copying `Catalogs/`.
- **KTL-7 owns the schema.** The `CND_` tables, the status check constraint, the consent and
  retention columns, relation tables, catalog foreign keys, indexes and the runtime-role grants
  arrive with KTL-7's migration. This change adds no migration of its own.
- **The synchronous break is the hard part on the frontend.** `CandidateService.find(id)`
  returns `Candidate | undefined` during render today, and `CandidateRelationsService`,
  `DocumentService`, `CandidateSearchService` and the list/detail/edit pages all depend on that
  synchrony. `CatalogService` faced a smaller version of this and solved it by keeping the reads
  synchronous over a loaded state.
- **`CatalogService` holds a transitional dependency on `CandidateService`.** KTL-6's
  `isCatalogValueInUse` pre-check reads `candidateService.list(true)` and is annotated
  "Remove with KTL-8". `ICatalogRepository.IsValueInUseAsync` carries the same note and returns
  `false` today. Both come due in this change.
- Architecture tests enforce the inward dependency direction and `DatabaseNamingTests` asserts
  the physical naming convention; both keep passing.

## Goals / Non-Goals

**Goals:**

- Move the candidate aggregate's reads and writes onto the API without changing what a user
  sees, including Spanish copy, validation messages and accents.
- Leave no candidate personal data in browser storage — including the copy already sitting in
  every existing browser.
- Keep the four consumer services and the three candidate screens working across the cutover,
  with the smallest change to each that is honest about loading and failure.
- Make the relation and document collections server-owned in the same change, so no half-migrated
  aggregate exists at any point.

**Non-Goals:**

- File upload, virus scanning, quarantine and secure download. Document **metadata** moves here;
  the bytes and their pipeline are KTL-9.
- Search, filtering and saved presets (KTL-10). `CandidateSearchService` keeps reading the
  service, and gains nothing new.
- Pagination, incremental hydration, offline mode, or optimistic UI. The dataset is a few
  thousand records at most and every screen loads what it needs.
- Authentication, and retention/purge automation.

## Decisions

### The aggregate boundary: one candidate, loaded whole

A candidate's relation collections are small (a handful of languages, programs, degrees, jobs,
skills, documents) and every screen that wants one wants all of them. `GET /api/candidates/{id}`
therefore returns the complete aggregate, and there are no per-collection read endpoints.

The alternative — lazy per-collection reads — was rejected because it multiplies round trips for
the detail screen, makes the read-through cache hold partially populated candidates, and gives
`find(id)` a meaning ("some of this candidate") that the existing consumers would silently get
wrong.

The list endpoint returns candidates **without** relation collections. The list screen shows
core fields only, and loading six collections per row for a list nobody expands would be waste.
This is the one place where the cache holds a partial record, and it is handled explicitly — see
the cache decision below.

### Collection writes replace whole sets, checked against the candidate's version

`PUT /api/candidates/{id}/languages` (and the four siblings) takes the complete collection and
the candidate's current `Version`. Per-item `POST`/`DELETE` endpoints were rejected for the same
reason KTL-6 rejected per-item catalog reorder swaps: two concurrent editors can interleave into
a state neither intended, and there is no version to check a single item against.

The candidate's own row version is the concurrency token for its collections, not a per-relation
version. This is what makes the aggregate an aggregate: any change to a candidate — a phone
number or a language — advances one token, so a stale detail screen is caught whatever it tries
to submit. It costs a false conflict when two users edit unrelated collections at once, which
for this product's concurrency level is the right trade.

### Logical delete is a state transition, not a verb

```
POST   /api/candidates                              candidates.create
GET    /api/candidates?includeInactive=false        candidates.read
GET    /api/candidates/{id}                         candidates.read
PUT    /api/candidates/{id}                         candidates.update
PUT    /api/candidates/{id}/active                  candidates.delete   (body: isActive, version)
PUT    /api/candidates/{id}/languages               candidates.update
PUT    /api/candidates/{id}/programs                candidates.update
PUT    /api/candidates/{id}/education               candidates.update
PUT    /api/candidates/{id}/experience              candidates.update
PUT    /api/candidates/{id}/skills                  candidates.update
PUT    /api/candidates/{id}/documents               candidates.update
```

There is deliberately no `DELETE` verb anywhere in the group, mirroring catalogs. Removal and
restoration are the same sub-resource with `isActive: false` / `true`; `candidates.delete`
governs both, because restoring a removed record is as consequential as removing it. Clearing
`IsActive` sets `DeletedAtUtc`; setting it clears `DeletedAtUtc`.

**Deactivation and logical deletion are the same thing here.** Today's UI already labels this
"desactivar", and the KTL-7 schema carries both `IsActive` and `DeletedAtUtc`. Introducing a
second, distinct "deleted" state would give the product two ways to be removed and force every
query to check both. One state, two columns: `IsActive` is what queries filter on, `DeletedAtUtc`
is when it happened.

Default reads (list, and the relation-value in-use check) exclude `IsActive = false`. Read by
identifier does **not** filter — a removed candidate must stay retrievable, and the detail screen
already renders inactive candidates with a restore action.

### Bulk deactivation stays a client-side loop

`deactivateMany` / `reactivateMany` back the list screen's multi-select. They become sequential
per-candidate calls, counting successes exactly as the current implementation counts changed
rows. A batch endpoint was rejected: it needs its own partial-failure contract, its own audit
shape, and its own concurrency story, for an action used on a handful of rows at a time. A
conflict or refusal on one candidate leaves the rest applied, and the count the UI reports is the
count that actually changed — which is today's contract.

### Repository shape: `ICandidateRepository`, one save, one audit event

Modelled directly on `ICatalogRepository`:

```csharp
Task<Candidate?> FindAsync(Guid id, bool includeInactive, CancellationToken ct);
Task<IReadOnlyList<CandidateSummary>> ListAsync(bool includeInactive, CancellationToken ct);
void Add(Candidate candidate);
void ExpectVersion(Candidate candidate, uint version);
Task<CandidateSaveOutcome> SaveAsync(string auditEventType, string subjectId, CancellationToken ct);
```

`SaveAsync` writes the pending aggregate change and its audit event in one transaction, so a
rejected write cannot leave an applied-change event behind. `CandidateSaveOutcome` distinguishes
`Saved`, `ConcurrencyConflict` and the constraint violations the slices translate into stable
codes. `ListAsync` returns a summary projection rather than full aggregates, which is what keeps
the list query from fanning out into six collection joins.

### The transitional catalog in-use guard is deleted, not moved

`business-catalogs` is explicit that the API **permits** deactivating a value candidate records
reference — the value stays resolvable, it just stops being offered for new selections — and that
the administration screen's refusal is a transitional stand-in "removed once the API can evaluate
the rule itself, after which the API's permission to deactivate becomes the product's behavior".

This change is that moment. So:

- `CatalogService.isCatalogValueInUse` and `matchesFamilyValue` are deleted, along with
  `CatalogService`'s constructor dependency on `CandidateService` — the one edge KTL-6 added to
  the service graph as a stopgap, and the last reason a catalog concern reaches into candidate
  data.
- `ICatalogRepository.IsValueInUseAsync` is deleted too. It was added to hold the place for a
  rule that, once the API can see relations, turns out not to block anything: nothing in the
  specified behavior consults it. Keeping an unused query because it was once anticipated is how
  dead abstractions survive.

**This is a deliberate, user-visible behavior change**, specified in the `business-catalogs`
delta rather than left as an implementation detail: an administrator can now deactivate a catalog
value that candidates reference, and the Spanish refusal message disappears. What protects the
data is what always protected it — deactivation is not deletion, and the referencing candidate
records keep resolving the value.

### Relation values reference catalog entries; the wire contract stays names

KTL-7's schema makes relation fields reference `CAT_` entries rather than free text. The API
contract, however, continues to speak the names the TypeScript models already use
(`language: 'Inglés'`, `level: 'B2'`). The application resolves a name to its catalog entry on
write and projects the entry's name on read.

Exposing catalog identifiers on the wire was rejected: it would force a rewrite of all five
section components and the search filters, all of which bind names today, for no user-visible
gain in this change. An unresolvable name is a validation failure with a stable code — never a
silently created catalog entry, which would let a typo permanently pollute the vocabulary.

### Frontend: a read-through aggregate cache, reads stay synchronous

```ts
type CandidateState = {
  status: 'idle' | 'loading' | 'loaded' | 'error';
  summaries: CandidateSummary[]; // from the list endpoint
  aggregates: Record<string, Candidate>; // from the detail endpoint
  error?: AppError;
};
```

`find(id)` reads `aggregates[id]` synchronously and returns `undefined` when the aggregate has
not been loaded — exactly today's signature. `list(includeInactive)` reads `summaries`. Every
mutation awaits the API and then replaces the affected entry from the response, so the cache
never holds a locally computed guess. The cache is in-memory only and dies with the tab; nothing
is written to browser storage.

The gap this leaves is real and is handled rather than papered over: `find(id)` returning
`undefined` now means "not loaded yet" as well as "no such candidate". The consumers that call
it (`CandidateRelationsService`, `DocumentService`) are only ever invoked from the detail screen,
which has already awaited `ensureLoaded(id)` — but "only ever" is an invariant that erodes. So
`status` is exposed alongside the data, `ensureLoaded(id)` is idempotent and shares an in-flight
promise per identifier, and the two consumer services call it before they read. Their public
methods therefore become `async`; their validation rules and Spanish messages are untouched.

An `AsyncCandidateService` returning promises from `find` was rejected: it forces every consumer
and every screen to be rewritten in the same change that moves the data, which is precisely the
combination that makes a personal-data cutover hard to review.

### The `demo-1` seed is not replaced by anything

The seed exists because an empty `localStorage` produced an empty product. With the API, an empty
database produces an empty list and that is the truth. No frontend fallback, no development seed
in `DatabaseInitializer` for candidates — KTL-7's migration is where real data comes from, and a
fabricated candidate in a table of personal data is a liability, not a convenience.

### Evicting `rrhh-candidates`

A one-line `localStorage.removeItem('rrhh-candidates')` at application bootstrap, inside a
`try/catch` so a storage-denied browser still starts, running before and independently of any API
call. It is not conditional on a successful load: the point is that the stale personal data goes
even if the backend is down.

A version-stamped migration registry was considered and rejected as premature — there is one key
to evict, and the call is harmless once the key is gone. The removal is covered by a Vitest case
asserting the key is absent after bootstrap.

### Serilog redaction

The redaction policy is extended to the candidate field names (`firstName`, `lastName`, `phone`,
`email`, `location`, `province`, `notes`, `receivedAt`, `consentAt`, `reviewDueAt`) rather than
relying on slices not to log them. A backend test asserts that a candidate write and a candidate
failure produce log output containing none of a set of distinctive sentinel values, which is the
only form of this check that stays true as slices are added.

### Authorization

`Permissions` gains `CandidatesCreate`, `CandidatesUpdate`, `CandidatesDelete` beside the
existing `CandidatesRead`. Each endpoint authorizes **before** dispatching, as
`CatalogEndpoints` does, so an unauthorized caller cannot probe for a candidate's existence
through validation or not-found problems. `DevelopmentActor` grants all four; production still
fails closed with no real actor.

The four map onto the existing frontend `view_candidates`, `create_candidates`,
`edit_candidates`, `delete_candidates` permissions, which continue to gate navigation and
controls only. The UI is not the control.

## Risks / Trade-offs

- **This change is large: five backend slice groups, four frontend services, three screens** →
  It is large because splitting it leaves a half-migrated aggregate, which is worse. It is
  sequenced so each backend slice group lands with its tests before the frontend cutover starts,
  and the frontend cutover is one commit that can be reverted on its own.
- **`find(id)` returning `undefined` for "not loaded" can produce a spurious "Candidato no
  encontrado"** → `ensureLoaded(id)` is called by the two consumer services before they read, is
  idempotent, and shares in-flight requests per identifier; Vitest covers the not-yet-loaded path
  for both services asserting they load rather than throw.
- **One version per aggregate causes false conflicts between unrelated edits** → Accepted. Two
  users editing the same candidate simultaneously is rare here, and a false conflict is a Spanish
  message asking the user to reload — a bounded annoyance, against a silent lost update, which is
  a personal-data integrity failure.
- **Depending on KTL-7 for the schema means this change cannot start until KTL-7 lands** → Stated
  as a hard prerequisite in the proposal and as task 0 in `tasks.md`. The alternative, owning the
  schema here too, means two changes writing the same migration.
- **Relation names resolved to catalog entries can fail for data KTL-7 migrated with unresolved
  values** → KTL-7 reports unresolved values rather than accepting them, so this cannot arrive
  silently; if it does, the read projection surfaces the stored value and the write refuses with
  a stable code rather than corrupting it.
- **Removing the reference slice removes KTL-5's smoke evidence** → Replaced by the candidate
  Playwright flows and the candidate integration tests, which travel the same boundaries with
  stricter authorization. The removal is specified, not incidental.
- **No candidate data in browser storage means an API outage empties the candidate screens** →
  Intentional and consistent with the catalog decision: a stale private copy is worse than a
  visible failure. The error state carries the Spanish message and the correlation identifier.

## Migration Plan

1. **Prerequisite**: KTL-7 is deployed — the `CND_` schema, its constraints, indexes and runtime
   grants exist, and the candidate data is loaded and reconciled.
2. Deploy the API with the candidate slices. The reference endpoint disappears in the same
   deployment; nothing outside the KTL-5 smoke flow consumed it.
3. Deploy the SPA. On first load each browser removes `rrhh-candidates` and every candidate read
   goes to the API.
4. Verify the candidate Playwright flows against the deployed environment, and confirm the
   absence of candidate values in the deployment's logs.

**Rollback**: revert the SPA. The previous build re-seeds `demo-1` into `localStorage` and is
self-sufficient again — but any candidate written through the API in the interim is invisible to
it, and the per-browser copies of the old data are gone. Rollback is therefore a
data-availability decision, not a free one; it is viable in the deployment window and stops being
viable once users have created candidates. The API can be left deployed; it shares no state with
the reverted SPA.

## Open Questions

- Whether a future retention job removes logically deleted candidates physically after a
  retention period is a product and legal question. It does not affect this design: the state and
  the timestamp it would key on are both stored, and the job is KTL-11+ work either way.
