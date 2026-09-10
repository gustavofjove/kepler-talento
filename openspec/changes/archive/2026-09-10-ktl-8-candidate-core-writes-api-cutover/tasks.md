## 0. Prerequisite

- [x] 0.1 Confirm KTL-7 (`ktl-7-access-to-postgres-migration`) is implemented and archived: the `CND_`
      candidate and relation tables, the status check constraint, consent/retention columns, catalog
      foreign keys, access-path indexes and runtime-role grants exist, and real candidate data is
      loaded. If not, stop — this change consumes that schema and must not create a second one.

## 1. Domain and persistence contract

- [x] 1.1 Expand `Domain/Candidates/Candidate.cs` to the full field set with private setters and
      intent-revealing methods (`UpdateDetails`, `ChangeStatus`, `Deactivate`, `Reactivate`), keeping
      the existing `Version` row version. `Deactivate` sets `DeletedAtUtc`; `Reactivate` clears it.
- [x] 1.2 Add `CandidateStatus` as a constrained value in the domain, restricted to `new`,
      `available`, `in_process`, `hired`, `rejected`, matching KTL-7's database check constraint.
- [x] 1.3 Add the relation entities (`CandidateLanguage`, `CandidateProgram`,
      `CandidateEducation`, `CandidateExperience`, `CandidateSkill`) owned by `Candidate`, each
      referencing its catalog entry, plus collection-replacement methods on the aggregate.
- [x] 1.4 Add `Application/Abstractions/Persistence/ICandidateRepository.cs` with
      `FindAsync(id, includeInactive)`, `ListAsync(includeInactive)` returning a summary projection,
      `Add`, `ExpectVersion`, and `SaveAsync(auditEventType, subjectId)` — modelled on
      `ICatalogRepository`, with `CandidateSaveOutcome` for saved / concurrency conflict / constraint
      violation.
- [x] 1.5 Implement `Infrastructure/Persistence/CandidateRepository.cs`, writing the aggregate
      change and its audit event in one transaction so a rejected write leaves no applied-change
      event. Default reads exclude `IsActive = false`; `FindAsync` with `includeInactive` does not
      filter.
- [x] 1.6 Align the EF Core configuration with KTL-7's schema (mapping only — no new migration),
      and confirm `DatabaseNamingTests` and the architecture tests still pass.
- [x] 1.7 Add `CandidatesCreate`, `CandidatesUpdate`, `CandidatesDelete` to `Permissions` and
      grant all four candidate capabilities in `DevelopmentActor`.

## 2. Candidate slices

- [x] 2.1 `Application/Features/Candidates/CandidateContract.cs`: request/response records, the
      summary projection, `CandidateGuards` (authorization, not-found, conflict, validation codes),
      and the stable code set with the Spanish messages copied verbatim from today's frontend.
- [x] 2.2 `CreateCandidate` slice: validator for required identity and permitted status,
      handler enforcing `candidates.create`, consent and retention metadata stored exactly as
      supplied with no substituted default.
- [x] 2.3 `UpdateCandidate` slice: enforces `candidates.update` and `ExpectVersion`; identifier
      and creation timestamp are never changed; metadata untouched when not supplied.
- [x] 2.4 `GetCandidate` slice: enforces `candidates.read`, returns the complete aggregate
      including all six collections, and returns logically removed candidates by identifier.
- [x] 2.5 `ListCandidates` slice: enforces `candidates.read`, excludes logically removed
      candidates unless `includeInactive` is requested, returns the summary projection.
- [x] 2.6 `SetCandidateActive` slice: enforces `candidates.delete` for both removal and
      restoration, checks the version, sets/clears `DeletedAtUtc`.
- [x] 2.7 Relation collection slices (languages, programs, education, experience, skills): each
      replaces the whole collection against the candidate's version, resolves each name to its
      catalog entry, and rejects an unresolvable name with a stable validation code rather than
      creating a catalog entry. Writing to a removed candidate is refused.
- [x] 2.8 `SetCandidateDocuments` slice: document metadata only, enforcing at most one primary
      document per candidate and exposing no storage path or key.
- [x] 2.9 Audit event types for create, update, status change, removal, restoration, relation
      change and document metadata change — actor, candidate identifier, kind, outcome, correlation
      identifier; no field values.

## 3. Web endpoints

- [x] 3.1 `Web/Features/Candidates/CandidateEndpoints.cs` mapping the eleven routes from
      `design.md`, each authorizing **before** dispatch so an unauthorized caller cannot probe for a
      candidate's existence. No `DELETE` verb anywhere in the group.
- [x] 3.2 Delete `Web/Features/Candidates/ReferenceCandidateEndpoints.cs`,
      `Application/Features/Candidates/GetReferenceCandidate.cs`, the reference seed in
      `DatabaseInitializer`, and the frontend `ReferenceCandidateService` and its registration.
- [x] 3.3 Extend Serilog redaction in `Web/Program.cs` to the candidate field names, and confirm
      problem responses expose no stack trace, database detail or internal path.

## 4. Catalog transitional guard removal

- [x] 4.1 Delete `ICatalogRepository.IsValueInUseAsync` and its implementation — nothing in the
      specified behavior consults it (see `design.md`).
- [x] 4.2 Delete `CatalogService.isCatalogValueInUse` / `matchesFamilyValue` and the
      `CandidateService` constructor dependency; update `services.ts` and the catalog service tests.
- [x] 4.3 Update the catalog administration Vitest and Playwright expectations: deactivating a
      referenced value now succeeds with no refusal message.

## 5. Frontend service cutover

- [x] 5.1 Add `candidate.api.ts` — a `CandidateGateway` interface over `ApiTransport` covering
      the eleven operations, so tests substitute the interface rather than the network.
- [x] 5.2 Rewrite `CandidateService` over a `CandidateState` signal (`status`, `summaries`,
      `aggregates`, `error`) with `ensureLoaded()` for the list, `ensureLoaded(id)` for one
      aggregate (idempotent, sharing an in-flight promise per identifier), synchronous `find(id)` and
      `list(includeInactive)` reads of the cache, and every mutation awaiting the API and replacing
      the cached entry from the response.
- [x] 5.3 Remove the `rrhh-candidates` key and the `demo-1` seed fallback; the service reads and
      writes no browser storage.
- [x] 5.4 `deactivateMany` / `reactivateMany` become sequential per-candidate calls returning the
      count that actually changed, preserving today's contract on partial failure.
- [x] 5.5 Add a `useCandidates()` hook mirroring `useCatalogs()`, binding the signal subscription
      and the load together so a bare read cannot silently stop updating.
- [x] 5.6 Evict the stale key at application bootstrap: `localStorage.removeItem('rrhh-candidates')`
      in a `try/catch`, before and independently of any API call, so it runs even when the API is
      unreachable or storage is denied.

## 6. Frontend consumer cutover

- [x] 6.1 `CandidateRelationsService`: methods become `async`, call `ensureLoaded(id)` before
      reading, and persist through the collection endpoints. Validation rules and Spanish messages
      are unchanged.
- [x] 6.2 `DocumentService`: `upload`, `setPrimary` and `remove` become `async` and persist
      document metadata through the API; `createSecureUrl` keeps its placeholder behavior (KTL-9).
- [x] 6.3 Candidate list page: loading and error states following the KTL-6 pattern, and the
      multi-select bulk actions awaiting the per-candidate calls.
- [x] 6.4 Candidate detail and edit pages: loading and error states, `409` surfaced in Spanish as
      a reload prompt, and the version carried from the loaded aggregate into every write.
- [x] 6.5 Update the five candidate section components and `CandidateSearchService` for the now
      asynchronous relation calls, leaving their rendered output unchanged.

## 7. Tests and evidence

- [x] 7.1 Backend unit tests: status constraint, consent metadata not defaulted, version
      conflict leaves no partial change and no audit event, per-collection replacement, unresolvable
      catalog name rejected, at most one primary document.
- [x] 7.2 Backend integration tests against disposable PostgreSQL: full create → read → update →
      relation write → logical delete → restore round trip; default list excludes removed candidates
      while read-by-identifier returns them; consent and retention metadata round-trip unchanged.
- [x] 7.3 Security tests: each of the four capabilities fails closed for an unauthenticated actor
      and for an authenticated actor lacking it; a refusal does not disclose whether the candidate
      exists; the runtime role cannot `DELETE` from candidate tables.
- [x] 7.4 Log test: a candidate write and a candidate failure produce log output containing none
      of a set of distinctive sentinel field values.
- [x] 7.5 Architecture tests: dependency direction holds with the new slices, and no reference
      candidate slice remains.
- [x] 7.6 Vitest: the rewritten `CandidateService` (cache, load states, no browser storage,
      bulk count), both consumer services on the not-yet-loaded path, the key eviction at bootstrap,
      and the loading/error branches of the three screens.
- [x] 7.7 Playwright: create, edit, add a relation, upload a document, logically delete and
      restore a candidate, plus the conflict message, through the same-origin `/api` route.
- [x] 7.8 Repository-wide check that `rrhh-candidates` appears nowhere under `src/` except the
      one-line eviction call.

## 8. Documentation

- [x] 8.1 Document the candidate endpoints and their stable error codes in the API contract.
- [x] 8.2 Update `README.md` / `docs/` for the removed reference slice, the new candidate
      capabilities, and the release note that browsers discard their local candidate copy on first
      run of the new build.
