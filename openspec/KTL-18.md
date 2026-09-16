# KTL-18 — Move the candidate list onto the paged search endpoint

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-10 (paged search endpoint, page envelope, minimal projection), KTL-16 (identity
and access control — supplies the renamed permissions and a real caller)

## Summary

This is workstream 3 of [KTL-16](./KTL-16.md), split out as its own ticket alongside
[KTL-17](./KTL-17.md) and [KTL-19](./KTL-19.md).

`GET /candidates?includeInactive=` returns **every** candidate, and the browser then sorts and
filters them. That is a full copy of the candidate table — identity, contact details, consent and
retention metadata — crossing the network so the browser can put it in order. KTL-10 already built
the endpoint that does this properly in PostgreSQL, with a documented page envelope and a minimal
projection. The candidate list simply never moved onto it.

It is the smallest of the three remaining workstreams and it closes the largest single
personal-data exposure left in the product.

## Context — what exists today

| Concern        | Today                                                              | Evidence                                          |
| -------------- | ------------------------------------------------------------------ | ------------------------------------------------- |
| List request   | One unpaged request returning every candidate                      | `src/app/core/api/candidate.api.ts`               |
| Projection     | The full aggregate, relations and all                              | same                                              |
| Sorting        | In the browser, over the whole downloaded set                      | `candidate-list-page.tsx`                         |
| Filtering      | In the browser, over the whole downloaded set                      | `candidate.service.ts`, `candidate-list-page.tsx` |
| Shareable view | None — sort and filter state lives in component state, not the URL | `candidate-list-page.tsx`                         |
| Search         | Already paged, already server-side, already minimally projected    | KTL-10, `openspec/specs/candidate-search/spec.md` |

## In scope

- **The list moves to the paged search endpoint.** Filtering, sorting and paging happen in
  PostgreSQL. The response carries the documented page envelope, and the default and maximum page
  sizes match the search contract (25 and 100).
- **Minimal projection.** The list returns the same minimal shape search returns. It must not return
  relation collections, notes, consent or retention data. A row in a list is not an aggregate.
- **Sorting becomes contract.** The sortable fields are named in the spec and validated server-side.
  An unknown sort field is refused with a stable validation code, not silently ignored and not
  interpolated into SQL.
- **The browser stops sorting and filtering.** `candidate-list-page.tsx` and `candidate.service.ts`
  drop their in-browser sort and filter entirely, rather than keeping them as a fallback.
- **The view is shareable.** Page, sort and filter state live in the URL, so a list view can be sent
  to a colleague and reopened as it was.
- **`includeInactive` stays available and stays guarded**, with the same permission that governs it
  today.

## Out of scope

- Reworking the search criteria model or the shared criteria form — that is KTL-14 territory and it
  is settled.
- Adding new filters to the list beyond what the search contract already supports.
- The candidate detail page, its reads and its writes.
- Export (its own ticket) and the audit trail ([KTL-19](./KTL-19.md)), although recording list and
  detail reads is exactly what KTL-19 adds on top of this.

## Decisions to make in the design

1. **One endpoint or two.** Whether the list calls the search endpoint directly with an empty
   criteria set, or a thin list endpoint sits in front of the same query. Say which, and why the
   other was rejected.
2. **The sortable field set.** Which fields are sortable, what the default order is, and how ties
   are broken so that paging is deterministic (KTL-10 already requires bounded deterministic
   pagination).
3. **URL parameter shape.** The names and encoding of the page, sort and filter parameters, and what
   happens when a URL carries an unknown or malformed one.
4. **Empty and out-of-range pages.** What the API answers for a page past the end, and what the page
   shows.
5. **Whether the old unpaged endpoint is removed or kept.** If anything still calls it, say what; if
   nothing does, remove it rather than leaving a route that returns the whole table.

## Acceptance criteria

- The candidate list issues **one** paged request, and the response contains no relation
  collections, notes, consent or retention data.
- No candidate sorting or filtering code remains in the browser.
- An unknown sort field is refused with a stable validation code, and no unknown field reaches the
  query.
- A list URL carrying page, sort and filter state reopens to the same view.
- Paging is deterministic: no candidate appears on two pages and none is skipped, across a stable
  data set.
- An unauthenticated caller gets 401 and a caller without `candidates.read` gets 403, before
  validation runs.
- `includeInactive` remains guarded by the same permission as today.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Security evidence required

This ticket touches personal data and permissions:

- An integration test asserting the list response body contains none of the excluded personal-data
  fields — asserted against the serialized response, not the projection type.
- Per-endpoint tests for unauthenticated and unauthorized callers, refused before validation.
- A test proving an unknown or injected sort field is refused rather than reaching the query.
- A test proving `includeInactive` is refused for a caller without its permission.
- Log assertions: no candidate personal data and no filter payload reaches the logs.

## Risks

- **Behaviour change for users.** Sorting a page is not sorting the whole table. Users accustomed to
  the browser sorting everything will see different results and should not be surprised by it —
  say so in the release notes.
- **Silent scope creep into search.** The list and search sharing a query is the point; the list
  quietly acquiring criteria that search does not have would undo KTL-14. Keep the criteria model
  single.
- **Deep paging cost.** Large offsets are slow. KTL-10's deterministic-pagination requirement is the
  place to check whether the existing approach holds at the expected table size.

## Documentation to update

- `docs/ktl-18/` — the list contract: envelope, page sizes, sortable fields, URL parameters.
- `docs/ktl-10/search.md` — note that the candidate list now shares this contract.
- `README.md` (Spanish) if anything user-visible changes in how the list is used.
