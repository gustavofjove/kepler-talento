## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-18` from an up-to-date `main`, after KTL-16 has
      merged — this change uses the renamed permissions and a real actor

## 1. Backend: extend the search contract (additive, ships alone)

Everything here is additive: a request that omits the new parameters behaves exactly as today, so
advanced search is unaffected while the frontend is still on the old path (design, Migration Plan).

- [x] 1.1 Add `IncludeInactive` to the search query request and its FluentValidation validator,
      defaulting to false. Covers: Candidate status and primary-CV state.
- [x] 1.2 Add `SortField` and `SortDirection` to the request as **enums parsed from a closed set**
      (`updatedAt`, `lastName`, `status` × `asc`, `desc`), defaulting to `updatedAt`/`desc`. Reject an
      unknown field with a stable validation code before the query layer is reached (design D2).
      Covers: Bounded deterministic pagination.
- [x] 1.3 Guard `IncludeInactive` with the permission governing removal and restoration, checked
      **after** authentication and **before** validation, refusing with 403 rather than narrowing the
      result (design D3). Covers: Search authorization and visibility decision.
- [x] 1.4 Update `Infrastructure/Persistence/CandidateSearchQuery.cs`: make the active-only predicate
      conditional, and parameterise the ordering over the contracted fields while **always** appending
      the candidate identifier ascending as the final term. `lastName` orders by last name then first
      name, matching today's visible order.
- [x] 1.5 Add the indexes the contracted sort fields need so deep paging stays bounded, in a migration
      with its grants unchanged.
- [x] 1.6 Update `Web/Features/Search/SearchEndpoints.cs` with the new query parameters and their
      `.Produces*` metadata.
- [x] 1.7 Apply the migration against the local stack and inspect the resulting indexes in PostgreSQL.

## 2. Backend tests

- [x] 2.1 Unit tests for the request validator: each documented sort field accepted, an unknown one
      rejected with the stable code, and pagination bounds unchanged.
- [x] 2.2 Integration tests for `includeInactive`: excluded by default; included on request for a
      permitted actor and distinguishable by removed state; **403 for an actor holding
      `candidates.read` alone**, sent with a malformed body to prove the refusal precedes validation.
- [x] 2.3 Integration tests for each sort field in both directions, asserting non-overlapping,
      non-skipping pages across a data set with many tied values — this is what the identifier
      tie-breaker exists for.
- [x] 2.4 Integration test asserting an unknown sort field never reaches the query, including an
      injection-shaped value.
- [x] 2.5 Integration test asserting a page past the end returns an empty page with the correct total,
      not an error and not the last populated page.
- [x] 2.6 Extend `SearchQueryPlanTests` to cover **each** sort field at a deep offset, not only the
      default ordering.
- [x] 2.7 Assert the list response body carries no relation collections, notes, consent or retention
      data — against the **serialized response**, not the projection type.
- [x] 2.8 Review and update the existing search integration tests affected by the new parameters.
- [x] 2.9 **Run** `npm run test:backend` with Docker running and inspect the output.

## 3. Frontend: move the list onto the paged endpoint

Covers: Candidate read and list; Feature service cache for synchronous consumers.

- [x] 3.1 Add the paged list call to `candidate.api.ts` against the search endpoint, carrying page,
      size, sort, direction, text, status, hasCv and includeInactive. Remove `list(includeInactive)`
      once nothing calls it.
- [x] 3.2 In `candidate.service.ts`: remove the whole-table summary list, and **delete
      `ensureAllAggregates`**, which has no callers and is superseded by KTL-10 (design D5). Keep
      `ensureAggregate(id)` and the per-identifier aggregate cache untouched. Update the class comment,
      which describes the whole-table read-through shape the service no longer has.
- [x] 3.3 Move every consumer of the old synchronous `list()` onto the paged result or onto
      `ensureLoaded(id)`, and confirm the detail and edit screens still load their own aggregate.
- [x] 3.4 Delete `filterCandidates`, `sortCandidates`, `paginate` and `totalPages` from
      `candidate-list.logic.ts`. Keep `nextSort`, `sortIndicator` and `removeFilter` (design D9). No
      fallback path may remain.
- [x] 3.5 Rewrite `candidate-list-page.tsx`: drop the `useMemo` pipeline, request one page, and render
      the envelope's total and page count. Default page size becomes 25, maximum 100 (design D8).
- [x] 3.6 Move page, sort and filter state into the URL as the single source of truth (design D6).
      Omit defaults from the URL; ignore malformed values in favour of the default, but send an
      unknown sort field to the API and render its refusal.
- [x] 3.7 Make selection page-scoped: clear it on page, sort or filter change, and state in the header
      how many rows on this page are selected. Remove the `candidateService.list(true)` bulk read at
      `candidate-list-page.tsx:103` (design D7).
- [x] 3.8 Gate the "incluir inactivos" control with `usePermission` for the removal permission, so it
      is hidden as well as refused (design D3, Risks). Hoist the hook result to a const at the top of
      the component.
- [x] 3.9 Rewrite `buildFilterChips` to use `t()` with interpolation and `catalogLabel` for the status
      chip instead of concatenated Spanish, and move the copy into `src/assets/i18n/es.json` under
      flat `candidates.list.*` keys.
- [x] 3.10 Remove `candidate-list-page.tsx` and its siblings from `LEGACY_HARDCODED_COPY` in
      `eslint.config.js`. The list only shrinks.
- [x] 3.11 Keep the `name=` attribute and `data-testid` on every control the Playwright suite binds to,
      including the sort headers and the filters bar.

## 4. Frontend tests

- [x] 4.1 Review and update the existing unit tests affected: the list page specs assert the browser
      pipeline and will be asserting the wrong thing, and the candidate service specs assert the
      whole-table summary load.
- [x] 4.2 New specs for the rewritten page: one request per view, paging, sorting, filtering, the
      empty result, the past-the-end page, and the unknown-sort refusal.
- [x] 4.3 Specs for URL state: a URL with page, sort and filter renders that view; defaults are absent
      from the URL; a malformed page number falls back to the default.
- [x] 4.4 Spec asserting selection clears on page, sort and filter change.
- [x] 4.5 Spec asserting the "incluir inactivos" control is absent without the permission.
- [x] 4.6 **Run** `npm test` and inspect the output.

## 5. End-to-end verification

- [x] 5.1 Extend `tests/e2e/candidate-crud.spec.ts` (or add a list spec) covering: paging, sorting by
      each field, filtering, and reopening a copied list URL to the same view. No selector may
      hardcode Spanish text.
- [x] 5.2 **Run** `npm run e2e` with `docker compose up` running, inspect the output, and restore seed
      data afterwards.
- [x] 5.3 **Run** the existing candidate-search and navigation specs to confirm advanced search did not
      regress — it now shares the endpoint this change extended.

## 6. Security gates and documentation

- [x] 6.1 Extend the security specs to cover the list surface: unauthenticated and unauthorized
      callers refused, and `includeInactive` refused without its permission.
- [x] 6.2 **Run** `npm run security:rls` and `npm run security:storage` and inspect the output.
- [x] 6.3 Write `docs/ktl-18/list-contract.md`: the page envelope, default and maximum page sizes, the
      sortable field set and tie-breaker, the URL parameter names, and the `includeInactive` guard.
- [x] 6.4 Update `docs/ktl-10/search.md` to note that the candidate list shares this contract and that
      search gained `includeInactive` and sorting.
- [x] 6.5 Write `docs/ktl-18/release-notes.md` stating plainly: page size is now 25; sorting and
      filtering now apply to the whole matching set; the text filter now also searches notes;
      selection is page-scoped; "incluir inactivos" now requires a permission.
- [x] 6.6 Update `README.md` (Spanish) if anything user-visible in how the list is used changed.

## 7. Done checks

- [x] 7.1 **Run** `npm run build:all` and confirm it is clean (warnings are errors).
- [x] 7.2 **Run** `npm run lint` and `npm run format:check`.
- [x] 7.3 **Run** `npm test` and `npm run test:backend` once more against the final tree and inspect
      both outputs.
- [x] 7.4 Confirm by grep that no candidate sorting, filtering or paging logic remains in `src/`.
- [x] 7.5 Run `openspec validate ktl-18-server-side-candidate-list --strict`.

## 8. Follow-up release (NOT in this change)

The unpaged route must outlive this deployment so the SPA can roll back (design, Migration Plan).

- [ ] 8.1 In the **following** release, once the new list has been in production long enough to trust:
      confirm by grepping `src/` and `tests/e2e/` that nothing calls the unpaged `GET /candidates`,
      then remove the route and its handler. Do not ship this with task 3.
