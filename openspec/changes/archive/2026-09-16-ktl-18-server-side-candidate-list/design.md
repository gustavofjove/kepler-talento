## Context

See `proposal.md` for motivation and `specs/` for the required behaviour. This design covers how the
candidate list moves onto the KTL-10 search query and what has to give in the frontend.

Current state:

- **The list downloads everything.** `candidate.api.ts` exposes `list(includeInactive: boolean)`
  hitting `/candidates?includeInactive=`. `candidate.service.ts:256` calls `this.api.list(true)` —
  always both, with a comment explaining that the screen offers "incluir inactivos" as a filter over
  what it already holds.
- **And the summary is not minimal.** `CandidateSummary` carries `location`, `province`, `country`,
  `availability`, `source`, `notes`, `receivedAt`, `consentAt` and `reviewDueAt` alongside the fields
  the list renders. So the whole-table download is a whole-table download of notes, consent and
  retention metadata.
- **`ensureAllAggregates` is dead code.** It loads every listed candidate's aggregate one request at
  a time, but it has no callers: its own comment says it served pre-KTL-10 advanced search, that it
  is "not on any screen's load path", and that KTL-10 superseded it. It is a whole-table N+1 sitting
  unreferenced in the service this change rewrites.
- **The browser does the work.** `candidate-list-page.tsx:44-53` chains
  `filterCandidates → sortCandidates → paginate` through `useMemo`, with `pageSize` defaulting to
  **10**. `candidate-list.logic.ts` holds all four helpers plus `nextSort`, `sortIndicator`,
  `buildFilterChips` and `removeFilter`.
- **Sort fields are `updatedAt`, `lastName`, `status`.** `sortCandidates` uses `localeCompare` on
  `updatedAt` as a string, on `` `${lastName} ${firstName}` ``, and on `status`.
- **Filters are text, status, hasCv, includeInactive.** `filterCandidates` matches text
  case-insensitively across `firstName`, `lastName`, `email` and `phone` — **not notes**.
- **Search already does this properly.** KTL-10: page envelope with items/page/size/total, default 25
  and maximum 100, ordering by update time descending with identifier ascending as tie-breaker, and a
  minimal projection of id, first name, last name, phone, email, status, primary-CV presence,
  primary document id and update time. Its text family covers identity, contact **and notes**.
- **Search excludes removed candidates unconditionally** — "Logically deleted candidates SHALL never
  appear in search."
- **`buildFilterChips` concatenates Spanish**: `` `Texto: ${...}` ``, `'Incluye inactivos'` and so on,
  in a `.logic.ts` that the i18next lint rule (which targets `.tsx`) does not cover.

## Goals / Non-Goals

**Goals:**

- One query, one envelope, one set of ordering rules for both the list and advanced search.
- Delete the browser-side data pipeline rather than leaving it as a fallback, because a fallback here
  means a screen that is sometimes correct.
- Narrow what crosses the network to what the list renders, and leave no unreferenced whole-table
  loader behind in the service.

**Non-Goals:**

- Reworking the search criteria model or the shared criteria form — settled in KTL-14.
- Adding list filters beyond what the search contract supports.
- Changing the candidate detail or edit screens beyond letting them load their own aggregate.
- Export, and the audit recording of these reads (KTL-19).

## Decisions

### D1 — Extend the search contract; do not build a second list endpoint

The list becomes a search with preset criteria. `includeInactive` and the sort parameters are added
to the existing paged search query, and `/api/search/candidates` serves both screens.

A separate `/api/candidates` list endpoint in front of the same SQL was the alternative. It keeps the
search contract untouched, but it creates a second page envelope, a second place to state the sort
rules and a second projection that will drift from the first — and the projection is exactly what
carries personal data. One contract with one test suite is the safer artefact.

The consequence is that `candidate-search`'s spec changes, which is why this change carries a delta
for it rather than only for `candidate-management`.

### D2 — The sortable set is `updatedAt`, `lastName`, `status`, and the tie-breaker never moves

The three fields the list already offers become the contracted set, each with `asc` and `desc`. The
candidate identifier ascending stays appended as the **final** ordering term under every sort, which
is what makes paging deterministic — without it, sorting by `status` over a table where most
candidates share a status produces pages that overlap and skip.

`lastName` sorts by last name then first name, matching what `sortCandidates` does today, so the
visible order does not change under the default Spanish collation.

The field arrives as an enum parsed from a closed set before it reaches the query layer. There is no
path where caller text becomes an identifier in generated SQL — the spec states this as a
requirement rather than leaving it to implementation discipline, because it is the one place in this
change where a mistake is an injection.

### D3 — `includeInactive` is guarded by the removal permission, and refused rather than narrowed

Search today never returns removed candidates. Adding the option raises the question of who may use
it, and the answer is the permission that already governs removal and restoration
(`candidates.delete`, which KTL-16 documents as covering both).

A caller without it who asks for removed candidates gets **403**, not an active-only result. Silently
narrowing is worse than refusing: the caller believes they have seen everything. This is a real
behaviour decision, so it is in the spec, not only here.

### D4 — The text filter widens to include notes, and that is stated

The list's `filterCandidates` matches name, email and phone. Search's text family matches identity,
contact **and notes**. Mapping the list onto search therefore widens the text filter.

This is not worth preserving a separate narrower text family for — two text semantics on one query is
how a "why does search find it but the list doesn't" bug is born. The release notes say the text
filter now also searches notes.

### D5 — The service keeps its per-identifier cache and loses everything whole-table

`candidate.service.ts` stops holding a whole-table summary list. `ensureAggregate(id)` and the
per-identifier aggregate cache stay exactly as they are: they are idempotent, share an in-flight
promise per identifier, and `use-candidates.ts` and `candidate-relations.service.ts` already call
them before reading. Nothing about the detail or edit screens has to change.

`ensureAllAggregates` is deleted outright. It has no callers and is superseded by KTL-10, as its own
comment says. It is not this change's job to remove dead code, but leaving an unreferenced
whole-table N+1 inside a service this change is rewriting is leaving a loaded gun on the table.

The reduction that matters is in the payload, not the call count: the list stops receiving every
candidate's `notes`, `consentAt`, `reviewDueAt`, `location`, `province`, `country`, `source` and
`availability` and receives the minimal projection instead.

`list()` consumers move to the paged result; the selection path at `candidate-list-page.tsx:103`,
which reads `candidateService.list(true)`, is reworked under D7.

### D6 — Page, sort and filter live in the URL, as the single source of truth

The page reads its state from the URL through React Router's search params and writes changes back,
rather than holding `useState` and mirroring it. One source means the back button, a reload and a
pasted link all behave, and there is no reconciliation step between two copies.

Unknown or malformed parameters are ignored in favour of the default rather than throwing, with one
exception: an unknown **sort field** is sent to the API, which refuses it, because a silently
ignored sort is a wrong answer presented as a right one. The page renders that refusal.

Defaults are omitted from the URL, so a plain `/app/candidates` stays clean.

### D7 — Selection is scoped to the current page, explicitly

Today `selectedIds` accumulates across filter and sort changes and the bulk action reads
`candidateService.list(true).filter(...)` — the whole table. With server paging the browser no longer
holds the other pages, so that cannot work.

Selection becomes **page-scoped**: changing page, sort or filter clears it, and the header says how
many are selected on this page. The alternative — a "select all N matching" that acts on rows the
user never saw — is a bulk operation on personal data driven by a filter the user may have
misread, and it is not something to add as a side effect of a paging change. If it is wanted, it is
its own ticket with its own confirmation design.

### D8 — Page size default moves from 10 to 25

The search contract's documented default is 25 and its maximum is 100. The list adopts both rather
than keeping 10 as a special case. Users will notice; the release notes say so. Keeping 10 would mean
the list and search disagree about what a page is, which is the drift D1 exists to prevent.

### D9 — `candidate-list.logic.ts` keeps only what is still logic

`filterCandidates`, `sortCandidates`, `paginate` and `totalPages` are deleted outright. `nextSort`,
`sortIndicator` and `removeFilter` stay — they manipulate the filter and sort _state_, which is still
the browser's job. `buildFilterChips` stays but stops concatenating Spanish: each chip becomes a
`t()` key with interpolation, and the status chip renders through `catalogLabel` rather than the raw
status code.

Keeping these in the sibling `.ts` preserves fast refresh for the page, as the file's existing shape
already does.

### D10 — Decisions made during implementation

Recorded here because implementation surfaced them; each is reflected in `docs/ktl-18/`.

- **The free-text filter stays out of the URL.** D6 puts filter state in the URL, but the
  `candidate-search` spec forbids search terms in logs and search is a POST for exactly that
  reason; a reloaded or shared `/app/candidates?q=<name>` would write the term into the Nginx
  access log. Page, sort, status, CV and include-inactive are in the URL; the text is page state
  and resets on reload.
- **The dashboard was a second whole-table consumer** of `list()`, which this design did not
  mention. Its counts now come from one-row searches' `totalCount` (active, without primary CV,
  with primary CV, and inactive for `candidates.delete` holders). "Pendientes de revisión" and
  "Recibidos este mes" were removed rather than served by a new endpoint (decided with the
  product owner). "Sin CV adjunto" becomes "Sin CV principal", because search's CV family is
  primary-CV presence.
- **Bulk actions load each selected candidate's aggregate** for its concurrency version, since
  the list projection carries none. This is one read per selected row, bounded by the page.
- **`reload()` became `invalidate()`**, which drops cached aggregates. With no list cache there is
  nothing to reload; the import page calls it so no screen shows a pre-import copy.
- **The status chip and column use the existing `search.criteria.status.*` keys**, not
  `catalogLabel`: statuses are a fixed code set, not catalog items.
- **Validation follows the search slice's existing collected-issues pattern**
  (`SearchSort.Parse` beside `SearchPaging.Validate`) rather than a FluentValidation validator,
  which this handler has never used. The new members travel in the POST body, as search's do.
- **Sort changes build on the latest written URL**, not the last render, so two changes made
  before a re-render (a filter change then a sort click) cannot undo each other. Found by the
  e2e "reopen a copied URL" test.
- **`status` sorts by code, alphabetically** (Open Questions), matching the previous browser
  ordering and allowing an index.

## Risks / Trade-offs

- **Deep paging cost.** A large offset is slow, and sorting by `lastName` over a big table without a
  supporting index is slower. → Add the indexes the contracted sort fields need in the same change,
  and extend the existing `SearchQueryPlanTests` to cover each sort field at a deep offset rather
  than only the default.
- **Users lose whole-table sorting.** Sorting a page was never what they thought it was, but the
  _feeling_ of the change is a regression. → Release notes, and the fact that sorting now genuinely
  orders the whole matching set is the headline, not a footnote.
- **Selection behaviour changes (D7).** Anyone relying on cross-page selection loses it. → It never
  worked in a defensible way; it operated on a table the browser happened to hold. Say so plainly.
- **The text filter widens (D4).** A search for a common word may now match on notes and return more
  than before. → Stated in the release notes; the alternative is two divergent text semantics.
- **`includeInactive` becomes permission-gated (D3).** A recruiter who used the "incluir inactivos"
  toggle and lacks `candidates.delete` loses it. → Correct — including removed records is a view of
  data that was deliberately removed. The control must be hidden for those callers as well as
  refused, so the toggle is gated by `usePermission` too.
- **One endpoint now serves two screens.** A change made for the list can break advanced search. →
  That coupling is the point of D1 and is why the search integration suite must run green in the same
  change; it is a benefit as long as the tests are honest.

## Migration Plan

1. Backend first: add `includeInactive` and the sort parameters to the search query, its validator
   and `CandidateSearchQuery`, plus the supporting indexes. Both are additive — a request that omits
   them behaves exactly as today, so advanced search is unaffected while the frontend is still on the
   old path.
2. Deploy the API. Nothing calls the new parameters yet.
3. Frontend: move the list onto the paged endpoint, delete the browser pipeline and
   `ensureAllAggregates`, move state into the URL.
4. Remove the unpaged `GET /candidates` route once nothing calls it — confirmed by grepping the
   frontend and the e2e suite, not assumed.

**Rollback.** Steps 1 and 2 are additive and need no rollback. Step 3 rolls back by redeploying the
previous SPA, which still calls the unpaged route — so **step 4 must not ship in the same release as
step 3**. Removing the route is the following release, once the new list has been in production long
enough to trust. This ordering is the reason step 4 is listed separately rather than folded into the
backend work.

## Open Questions

- Whether the `status` sort should order by the status's business meaning (`new → available →
in_process → hired → rejected`) rather than alphabetically. Today's `localeCompare` on the code is
  alphabetical and arguably meaningless. Either is a single `ORDER BY` expression behind the same
  contracted field name, so it changes no spec, no endpoint shape and no task — it can be settled
  when the sort is implemented.
