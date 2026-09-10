## Context

See [proposal.md](./proposal.md) for motivation and the two capability specs for observable
behavior. KTL-8 removes the browser's authoritative candidate collection; KTL-9 makes relations
and primary-document state authoritative in PostgreSQL. The current `CandidateSearchService`
therefore performs a temporary request-per-candidate hydration before filtering, while
`SearchPresetsService` is still synchronous over browser storage.

The implementation must fit the established ASP.NET Core 10 vertical-slice boundary, EF Core and
application-owned PostgreSQL, the shared React `ApiTransport`, singleton signal services and
test-swappable DI. Expected production scale is a few concurrent users and a few thousand
candidates, but the query must remain bounded. Search text and preset filters may contain personal
data and cannot enter logs. KTL-6, KTL-8 and KTL-9 are deployment prerequisites; KTL-7 supplies the
representative migrated dataset and existing `CND_` indexes against which plans are checked.

## Goals / Non-Goals

**Goals:**

- Make one server query the source of truth for filtering, counting, ordering and paging.
- Preserve the current filter semantics with SQL shapes that cannot multiply result rows.
- Keep the wire response at the existing `SearchResult` projection and make request bounds part
  of validation, not a UI convention.
- Store presets under the current actor with database-enforced per-owner name uniqueness.
- Prove parity, query plans, least privilege, fail-closed authorization, redaction and actual
  transport cancellation.

**Non-Goals:**

- A generic query language, relevance ranking, fuzzy matching, saved-preset sharing or export.
- Introducing candidate visibility ownership that the domain does not currently model.
- Reading CV content or changing document quarantine, scan or download authorization.
- Migrating local preset data whose browser identity cannot be safely bound to an API actor.
- A new frontend state library, backend runtime package, PostgreSQL full-text configuration or
  Supabase/RLS path.

## Decisions

### 1. Search is a POSTed query contract with a paged response

Add `POST /api/candidates/search` as a `Candidates/SearchCandidates` vertical slice. The request
contains the complete `SearchFilters` value plus `page` and `pageSize`; the response is
`{ items, page, pageSize, totalCount }`. POST is chosen because nested criteria pairs are awkward
and length-prone in a query string, and URLs are commonly logged. POST does not make the operation
stateful: the handler remains a read and has no automatic retry behavior.

Validation trims values, rejects unknown modes/status/CV values, removes blank criteria, and
deduplicates normalized `(value, level)` pairs before querying. Missing pagination becomes page 1
and size 25; values outside the documented range fail before repository access. The order is
`UpdatedAtUtc DESC, Id ASC`. The count and page are computed from the same normalized predicate.
They are separate SQL statements under the normal read-committed boundary; snapshot consistency
between them is not promised when another user writes concurrently.

Alternatives rejected:

- `GET` with serialized JSON or repeated criteria parameters makes personal search terms more
  likely to appear in proxy/access logs and gives a fragile nested contract.
- Cursor pagination complicates arbitrary navigation and total-count UI without adding value at
  the current scale.
- Returning full candidate aggregates would increase payload and personal-data exposure solely
  for implementation convenience.

### 2. Correlated existence predicates implement relation semantics

The repository starts from `CND_Candidate` with the logical-deletion predicate and uses
correlated `EXISTS` expressions for skills, languages, programs and primary documents. For an
`ANY` family, the predicate is an OR/existence match across normalized criteria. For `ALL`, each
distinct criterion must have its own matching `EXISTS`. Candidate rows are never joined into the
outer projection, so relation multiplicity cannot duplicate a candidate and no compensating
`DISTINCT` hides a wrong query.

Relation values are compared through their catalog entry using the same trimmed,
case-insensitive equality as the frontend. A blank criterion level omits the level predicate;
otherwise both catalog value and level must match. An unknown value naturally yields no match.
An empty family emits no predicate. Status uses a normalized `IN` predicate, with empty and all
five statuses both treated as unrestricted. Primary-CV presence uses a correlated existence test
for a non-removed primary metadata row regardless of scan state; KTL-9 remains responsible for
refusing unavailable downloads.

Alternatives rejected:

- One multi-join query plus `DISTINCT` makes `ALL` counts and pagination easy to get subtly wrong.
- Loading candidate IDs and filtering relations in memory recreates the scalability and exposure
  problem this change removes.

### 3. Literal substring parity uses escaped PostgreSQL `ILIKE`

Free text remains a case-insensitive literal substring over first name, last name, email, phone
and notes. The query escapes `%`, `_` and the escape character before constructing the `ILIKE`
pattern, so user text does not accidentally become SQL wildcard syntax. Parameters remain bound;
neither SQL nor structured logs interpolate filter values.

The index plan covers the B-tree predicates that reduce structural search work:
active/deletion plus update ordering on candidates; catalog/value lookup; the relation paths
reached from a criterion's catalog entry; and KTL-9's primary-document partial index. A
leading-wildcard text search cannot use a normal B-tree. At the documented size of a few
thousand candidates, a parameterized sequential scan is simpler and avoids a new extension,
write amplification and operational dependency. Implementation must retain
`EXPLAIN (ANALYZE, BUFFERS)` evidence against the reconciled KTL-7 dataset for unfiltered,
text-heavy, `ANY`, `ALL` and combined cases. If that evidence violates the agreed interactive
target, the design must be amended before adding `pg_trgm`; a `tsvector` is not acceptable for
this parity slice because token semantics differ from literal substring matching.

**Amended during implementation.** This section originally called for new relation indexes on
`(catalog item, candidate, normalized level)`. Those were written and measured against the
KTL-7-scale dataset, and the planner never chose one: the composite catalog foreign keys
already carry indexes led by the catalog identifier a criterion filters on, and PostgreSQL
preferred those. They were therefore removed rather than shipped, and KTL-10 adds **no** index
of its own — every predicate is served by an index KTL-6 through KTL-9 already own. The
retained plans in `docs/ktl-10/query-plans.md` show every representative search completing in
single-digit milliseconds at 3 000 candidates, including the leading-wildcard text scan at
roughly 8 ms, which also confirms the decision to ship no trigram index.

### 4. Presets use an owner-scoped `ADM_SearchPreset` table

Add one explicitly mapped PostgreSQL table:

| Column                | Shape                  | Purpose                                                     |
| --------------------- | ---------------------- | ----------------------------------------------------------- |
| `Id`                  | UUID primary key       | Opaque API identity                                         |
| `OwnerId`             | bounded text, required | Stable `ICurrentActor` identifier; never supplied by client |
| `Name`                | bounded text, required | Trimmed display name                                        |
| `NormalizedName`      | bounded text, required | Case-folded uniqueness key                                  |
| `Filters`             | JSONB, required        | Versioned complete `SearchFilters` document                 |
| `FilterSchemaVersion` | integer, required      | The stored document's schema version                        |
| `CreatedAtUtc`        | timestamptz, required  | Server creation time                                        |
| `UpdatedAtUtc`        | timestamptz, required  | Server mutation/last-use time                               |
| `LastUsedAtUtc`       | timestamptz, nullable  | Last successful apply time                                  |

The migration adds length/not-blank checks, JSON-object validation, timestamp-ordering checks,
and a unique index on `(OwnerId, NormalizedName)`. `OwnerId` intentionally has no foreign key
until the authentication/identity schema exists; inventing a parallel user table in KTL-10
would couple presets to an unapproved identity design. Domain/application validation owns the
filter schema; JSONB keeps it atomic and matches the existing value object better than a dozen
preset child tables. A small schema version inside `Filters` permits future explicit upgrades.

**Amended during implementation.** Two details differ from this section as first written:

- The schema version is stored **both** inside the `Filters` document and as its own
  `FilterSchemaVersion` column. The document keeps a stored row self-describing without the
  code that wrote it; the column makes the version queryable, lets a check constraint enforce
  it, and means a future upgrade can find the rows it must migrate without parsing every JSON
  value.
- The unique index **is** the listing index. `(OwnerId, NormalizedName)` is exactly the shape
  and order the owner-scoped, case-insensitively alphabetical list reads, so the separate
  listing index this section originally called for would have duplicated it and cost writes
  for nothing.

Expose `GET/POST /api/search-presets`, `PUT/DELETE /api/search-presets/{id}`, and
`POST /api/search-presets/{id}/use`. Every repository predicate includes `OwnerId`; cross-owner
IDs return the same not-found result as absent IDs. The unique constraint is authoritative under
concurrency and maps to a stable conflict problem. Apply/use returns filters and advances both
timestamps in one update.

Alternatives rejected:

- Candidate-adjacent `CND_` storage misrepresents user-owned configuration as candidate data.
- Client-supplied owner IDs permit confused-deputy access.
- Normalized relational filter child tables add write complexity without enabling any required
  server query over presets.

### 5. Local preset migration is explicitly unsupported

The former browser presets have no trustworthy, portable owner identity. Automatically attaching
them to whichever API actor first opens a shared browser could disclose names and search terms or
assign them to the wrong employee. KTL-10 therefore takes the allowed unsupported branch: remove
all `rrhh.search.presets.v1` code, do not inspect or delete any retained browser value, and state
in Spanish release/user documentation that presets must be recreated. This also satisfies the
source-level removal criterion without obfuscated legacy-key construction.

`rrhh.search.last-filters.v1` stays local and retains defensive normalization, including the
older `languageValues`/`programValues` conversion if already supported for last filters. It never
becomes a preset automatically.

Alternative rejected: a one-time uploader would need to keep the forbidden legacy key in source
and cannot prove that browser state belongs to the current server identity. A separate opt-in
import could be specified later if the business supplies an ownership-safe workflow.

### 6. Authorization is server-side; `view_all_candidates` remains inert

The canonical backend capability remains `candidates.read`, mapped to the frontend's existing
`view_candidates`. It is checked before validation or repository access on search and every
preset endpoint. Preset ownership is additionally derived from `ICurrentActor`. Production with
no real actor therefore fails closed; `DevelopmentActor` remains development-only.

`view_all_candidates` has no scoping effect in this slice. The data model has no recruiter owner,
team, office assignment or other truthful visibility boundary, so filtering actors without that
permission would be arbitrary. Both actors receive the same eligible population. The permission
is neither removed nor granted to new roles; a future authorization change may activate it only
alongside an explicit domain scope and backfilled data.

Search request bodies, preset names and filter JSON are excluded from request logging, exception
logging and audit payloads. Safe events may carry actor/candidate/preset opaque IDs, operation,
outcome and correlation ID. Search itself is read-only and does not create per-result audit rows.

### 7. Database migration and grants ship as one repeatable deployment action

One EF Core migration creates `ADM_SearchPreset`, its constraints and indexes, adds only measured
search indexes not already owned by KTL-7/KTL-9, and applies explicit runtime-role grants. The
runtime role receives `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on the preset table and only the
existing read privileges required on candidate/catalog/relation/document tables; it receives no
DDL, role-management or broad schema privileges. Migration execution remains owned by the
separate migration role and EF migration history makes redeployment idempotent. Integration
evidence queries PostgreSQL privileges and exercises deployment twice.

This change does not add or alter Supabase RLS. PostgreSQL is unreachable from the browser; API
authorization and database least privilege replace browser-facing RLS for the migrated slice.

### 8. React services stay the only feature boundary

`CandidateSearchService` depends on `ApiTransport`, accepts a caller `AbortSignal`, and returns the
paged contract. It no longer depends on `CandidateService` or hydrates aggregates. The advanced
search page keeps presentation state, starts a roughly 300 ms debounced request, aborts its prior
controller before starting another, and suppresses the shared transport's cancellation outcome.
The service does not silently ignore stale promises as a substitute for aborting the request.

`SearchPresetsService` becomes asynchronous and API-backed for preset operations while retaining
only last-filter local helpers. Because presets are read during render, the service holds a plain
signal for loading/loaded/failed state and gets a subscribing `useSearchPresets()` hook; components
must not read it directly through `useServices()`. Tests replace both services with
`ServicesProvider` doubles. Errors flow through `useErrorToast()` and all controls preserve
`name` and `data-testid` attributes.

### 9. Layered tests use one shared parity fixture

- Backend unit tests cover normalization, validation, `ANY`/`ALL`, blank level, bounds, owner
  scoping and stable errors.
- PostgreSQL integration tests load a shared fixture containing duplicate-capable joins,
  multi-row `ALL` matches, deleted candidates and every document state. The expected IDs are
  also evaluated by a small preserved reference implementation of the old in-memory semantics.
- Query-plan tests retain `EXPLAIN (ANALYZE, BUFFERS)` output from the migrated KTL-7-scale data;
  database tests inspect indexes, constraints and runtime grants and verify repeat deployment.
- Authorization/security tests prove unauthenticated and unauthorized failure before data access,
  cross-owner not-found behavior, response minimization and absence of terms/filter JSON from
  captured logs. Unchanged legacy Supabase paths keep their existing checks.
- Frontend Vitest tests cover request serialization, async preset state, legacy-key absence, local
  last-filter retention, debounce, `AbortSignal` propagation and suppression of cancelled results.
- The targeted candidate-search Playwright flow exercises filters, both modes, pagination,
  presets and a superseded request through same-origin `/api`, then restores fixture/seed state.

## Risks / Trade-offs

- [Leading-wildcard text scans grow with the candidate table] → retain KTL-7-scale query-plan
  evidence and amend the design before adding a measured trigram index.
- [Count and page can differ during concurrent writes] → deterministic ordering prevents
  duplication for unchanged data; document read-committed behavior rather than holding an
  expensive repeatable-read transaction across two statements.
- [JSONB allows shapes beyond database checks] → version and validate the complete value in the
  application on every write/read, with integration fixtures for malformed stored JSON.
- [Owner IDs precede the identity schema] → derive them only from the current actor, bound their
  length, avoid a speculative foreign key, and include identity reconciliation in future auth
  planning.
- [Existing browser presets are not migrated] → document the choice in Spanish before rollout;
  retained browser values are ignored and never disclosed or assigned to another actor.
- [KTL-8/KTL-9 contracts may still move while in flight] → implement after their migrations and
  API shapes are complete, rebase the repository query on their final mapped columns, and run
  their affected suites.

No standing-principle departure is required. The API remains the sole backend boundary, personal
data is minimized and redacted, authorization fails closed, storage is untouched, migrations
include least privilege, and each behavior has executable evidence.

## Migration Plan

1. Confirm KTL-6 through KTL-9 are deployed, KTL-7 reconciliation passes, and the final relation
   and document mappings match this design. Capture baseline parity results from the shared
   fixture.
2. Apply the EF migration with the migration role; verify constraints, indexes and runtime grants,
   re-run it to prove repeat deployment, and capture representative query plans.
3. Deploy search and preset slices behind the same `/api` route. Exercise authorized,
   unauthorized, cross-owner, pagination and redaction checks before enabling the UI.
4. Deploy the React cutover in the same release, including Spanish notice/documentation for
   unsupported local presets. Verify `rrhh.search.presets.v1` is absent from source and
   `rrhh.search.last-filters.v1` still restores locally.
5. Run backend, frontend, integration, architecture, security and targeted Playwright suites;
   inspect resulting PostgreSQL rows/grants and restore the test seed afterwards.

Rollback first restores the previous frontend and API binaries while leaving the additive preset
table in place; it contains only new API-created presets and is harmless to the old build. If a
database rollback is later required, export/retain preset rows under restricted operational
access, revoke runtime grants, then drop only the KTL-10 table/indexes through a reviewed down
migration. Do not restore browser preset persistence or broaden database grants as a shortcut.
