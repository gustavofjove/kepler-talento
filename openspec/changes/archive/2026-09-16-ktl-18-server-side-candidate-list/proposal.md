## Why

The candidate list downloads the entire candidate table so the browser can sort it.
`candidate.service.ts` calls `api.list(true)` — every candidate, active and inactive — and
`candidate-list-page.tsx` then runs `filterCandidates`, `sortCandidates` and `paginate` over the
result in `useMemo`. The summary it downloads is not minimal either: `CandidateSummary` carries
`notes`, `consentAt`, `reviewDueAt`, `location`, `province`, `country`, `source` and `availability`
— so every recruiter's browser holds the notes, consent and retention metadata of every candidate in
the table in order to render a list of names.

KTL-10 already built the endpoint that does this properly: filtering, sorting and paging in
PostgreSQL, a documented page envelope, a minimal projection. The list simply never moved onto it.
This is the smallest of the three remaining KTL-16 workstreams and it closes the largest single
personal-data exposure left in the product (brief: `openspec/KTL-18.md`).

## What Changes

- **BREAKING** The candidate list issues one paged request against the search endpoint. Filtering,
  sorting and paging happen in PostgreSQL.
- **BREAKING** The list response carries the minimal search projection — id, names, phone, email,
  status, primary-CV presence, primary document id and update time. It no longer carries notes,
  consent metadata, retention metadata, location or source, which the current summary sends for every
  candidate and the list has never rendered.
- **BREAKING (UI)** The default page size changes from 10 to the search contract's 25, and the
  maximum becomes 100. Sorting and filtering now apply to the whole matching set rather than to
  whatever the browser had downloaded — which is a correctness fix, and will read as a behaviour
  change.
- **The search contract gains `includeInactive`.** Search covers active candidates only today; the
  list must be able to include logically removed ones. The option is guarded by the same permission
  that guards it on the list today, and defaults to excluding them.
- **The search contract gains sort fields.** `updatedAt`, `lastName` and `status`, each ascending or
  descending, with the candidate identifier as the tie-breaker so paging stays deterministic. An
  unknown sort field is refused with a stable validation code, never ignored and never interpolated
  into SQL.
- The list's free-text filter maps onto the search text family. That widens it: search text also
  covers notes, which the browser filter did not. Stated rather than hidden.
- **The in-browser sort and filter are deleted**, not kept as a fallback. `candidate-list.logic.ts`
  loses `filterCandidates`, `sortCandidates`, `paginate` and `totalPages`.
- **`ensureAllAggregates` is deleted.** It loads every listed candidate's full aggregate one request
  at a time. It has no callers — its own comment records that it served pre-KTL-10 advanced search,
  that it is "not on any screen's load path" and that KTL-10 superseded it. Removing dead code is not
  the point of this change, but leaving a whole-table N+1 in a service this change is rewriting would
  be an invitation.
- Page, sort and filter state move into the URL, so a list view can be shared and reopened as it was.
- Bulk selection becomes explicit about paging: a selection is scoped to what the user can see, and
  the page says so, rather than silently operating on a set the user never saw.

**Actors:**

- Recruiters holding `candidates.read`, who browse, filter and sort the list.
- Actors additionally holding the permission that governs removed records, who include them.
- Unauthenticated and unauthorized callers, refused before validation.

**Key entities:** the candidate list item (the existing minimal search projection); the page envelope
(items, page, page size, total); the sort field and direction; the list filter set (text, status,
CV presence, include-inactive).

**Assumptions:**

- KTL-10's page envelope, minimal projection and deterministic ordering are the contract, and this
  change extends rather than replaces them.
- KTL-16 has shipped, so `candidates.read` is the permission name and a real actor backs it.
- The candidate detail and edit screens keep working unchanged through `ensureAggregate(id)`, which
  `use-candidates.ts` and `candidate-relations.service.ts` already call per candidate.
- The `hasCv` filter maps onto the existing primary-CV presence filter, and `status` onto the
  existing status family, with no change to either.

**Edge cases:**

- A page requested past the end of the result set.
- A URL carrying an unknown sort field, a malformed page number, or a status that is not a status.
- Candidates sharing an update time across a page boundary.
- Data changing between page one and page two.
- A selection made on page one while the user pages to page two and acts.
- A caller asking for inactive records without the permission that governs them.
- A filter that matches nothing.
- The list opened directly by URL with no parameters at all.

**Success criteria:**

- The list issues **one** paged request, and the response body contains no relation collections,
  notes, consent or retention data — asserted against the serialized response, not the projection
  type.
- No candidate sorting or filtering code remains in the browser.
- An unknown sort field is refused with a stable validation code and never reaches the query.
- A list URL carrying page, sort and filter state reopens to the same view.
- Paging is deterministic across a stable data set: no candidate appears on two pages and none is
  skipped.
- An unauthenticated caller gets 401 and a caller without `candidates.read` gets 403, before
  validation runs; `includeInactive` is refused for a caller without its permission.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Capabilities

### New Capabilities

None. This change moves an existing screen onto an existing capability and extends that capability's
contract.

### Modified Capabilities

- `candidate-search`: the filter contract gains an explicit include-inactive option and states how
  logically removed candidates are treated; pagination gains a contracted sort field set with
  server-side validation of unknown fields; the authorization requirement gains the guard on
  including removed records.
- `candidate-management`: the candidate list requirement is restated — listing is paged, minimally
  projected and server-sorted, and is no longer an unbounded read of every candidate.
- `frontend-api-transport`: the feature-service cache requirement is restated, because the candidate
  service stops holding a whole-table summary list and keeps only its per-identifier aggregate cache,
  and because a cache stops being the means by which a screen filters, sorts or pages.

## Impact

- **Backend:**
  - `Application/Features/Search`: the search query and its validator gain `IncludeInactive`,
    `SortField` and `SortDirection`, with the sort field validated against a closed set.
  - `Infrastructure/Persistence/CandidateSearchQuery.cs`: the ordering becomes parameterised over the
    contracted fields with the identifier tie-breaker retained, and the active-only predicate becomes
    conditional.
  - `Application/Abstractions` search contract types and the guard that governs including removed
    records.
  - `Web/Features/Search/SearchEndpoints.cs`: the new query parameters and their metadata.
  - Possibly the removal of the unpaged `GET /candidates` list route once nothing calls it.
- **Frontend:**
  - `candidate-list.logic.ts` loses `filterCandidates`, `sortCandidates`, `paginate` and
    `totalPages`, and keeps the filter-chip and sort-indicator helpers.
  - `candidate-list-page.tsx` drops its `useMemo` pipeline, reads page/sort/filter from the URL and
    writes changes back to it.
  - `candidate.service.ts` stops holding a whole-table summary list and loses the unreferenced
    `ensureAllAggregates`; `list()` consumers move to the paged result. `ensureAggregate(id)` and the
    per-identifier cache stay as they are.
  - `candidate.api.ts` gains the paged list call and loses `list(includeInactive)`.
  - `candidate-filters-bar` and the list table keep their controls, `name=` attributes and
    `data-testid` values.
  - `buildFilterChips` currently builds Spanish labels by concatenation; it moves to `t()` with
    interpolation and `catalogLabel`.
- **Tests:** backend unit and integration tests for the new sort and include-inactive contract and
  its refusals; a query-plan check that deep paging stays bounded; Vitest specs for the rewritten
  page and service; Playwright list, paging and shared-URL specs.
- **Docs:** new `docs/ktl-18/` (the list contract: envelope, page sizes, sortable fields, URL
  parameters); `docs/ktl-10/search.md` noting that the list shares this contract.
- **Dependencies:** none added or removed.
- **Personal data, storage and roles:**
  - This change touches personal data directly, and reduces its exposure sharply: the browser stops
    receiving every candidate's notes, consent metadata, retention metadata, location and source, and
    receives one page of a minimal projection instead.
  - Principle 1 is upheld as follows: the list projection carries only what the list renders; notes,
    consent and retention metadata stop crossing the network for list purposes; no document path or
    storage key is exposed; filter values stay out of logs, as the search spec already requires.
  - Principle 3 is upheld as follows: every request checks authentication and `candidates.read`
    before validating; including removed records is separately guarded; the sort field is validated
    against a closed set so no caller-supplied text reaches the query; no database grant changes.
  - No role definition changes and no RLS policy changes.
