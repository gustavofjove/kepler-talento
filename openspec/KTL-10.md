# KTL-10 — PostgreSQL candidate search and saved presets

**Status:** Ready for OpenSpec planning
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (catalogs), KTL-8 (candidate aggregate), KTL-9 (relations, CV state)

## Summary

Move candidate search from in-browser filtering over the whole `localStorage` dataset to
a server-side PostgreSQL query, and move saved search presets to the API.

## Why

`CandidateSearchService` filters an in-memory array that only exists because the entire
candidate table was loaded into the browser. After KTL-8 and KTL-9 that array is gone, so
search has to become a query — this ticket is a direct consequence of the earlier
cutovers, not an optional enhancement.

It is also the point at which search stops being bounded by what fits in one browser.

## Domain rules to preserve

The standing rules in [openspec/config.yaml](./config.yaml) are the specification here and
must hold identically in SQL:

- empty filters are ignored;
- different filter families combine with `AND`;
- within a multi-value family, both `ANY` and `ALL` semantics are supported;
- a candidate is never returned twice.

The last two are the ones that break naively: `ALL` over a joined relation table and
duplicate suppression across multiple joins are exactly where a straightforward
translation goes wrong.

## In scope

### Backend

- A search slice implementing the full
  [SearchFilters](src/app/features/search/models/search.models.ts) contract:
  - `text` — free text across candidate identity and contact fields;
  - `statusValues` — the five `CandidateStatus` values;
  - `skillCriteria`, `languageCriteria`, `programCriteria` — each a list of
    `{ value, level }` pairs where an empty `level` means any level, each with its own
    `ANY`/`ALL` mode;
  - `hasCv` — `''` / `'yes'` / `'no'`, resolved against primary-CV state from KTL-9.
- Returns the existing `SearchResult` projection, not full candidate records — it already
  carries only what the results grid renders, which keeps it aligned with principle 1.
- Logically deleted candidates are excluded.
- Server-side pagination and a bounded result set; the current implementation has neither
  because the dataset was small enough to hold in memory.
- Indexes chosen against the real migrated dataset from KTL-7, not against the demo seed.
- Saved presets: `ADM_`-scoped or candidate-adjacent tables for `SearchPreset`
  (`name`, `filters`, `createdAt`, `updatedAt`, `lastUsedAt?`), with create, rename,
  update, delete and last-used tracking.
- Preset name uniqueness per owner, matching current behaviour.

### Frontend

- Rewrite `CandidateSearchService` to call the search endpoint; remove in-memory
  filtering.
- Rewrite `SearchPresetsService` preset handling against the API; remove
  `rrhh.search.presets.v1`.
- Carry the legacy preset-shape migration across. The service currently normalises
  pre-criteria presets that stored plain `languageValues` / `programValues` arrays
  ([search-presets.service.ts](src/app/features/search/services/search-presets.service.ts)).
  Any preset still in a browser in that shape must be normalised during the one-time
  upload, or explicitly declared unsupported — decide, do not leave it implicit.
- **Keep `rrhh.search.last-filters.v1` in `localStorage`.** It is a per-browser
  convenience, not shared state, and KTL-5's design explicitly permits local UI
  preferences to remain. This is the one key the migration deliberately does not move.
- Debounce and cancellation: search is now a network call, so the transport's
  `AbortSignal` support must be used to cancel superseded queries.

## Out of scope

- CSV export of search results — a separate ticket, and its requirements are not ready.
- Authentication.
- Full-text ranking, fuzzy matching, or relevance scoring. Behaviour parity first.
- Sharing presets between users.

## Personal-data and security impact

- Principle 1: the `SearchResult` projection stays minimal. Do not widen it to full
  candidate records for convenience.
- Search terms may themselves be personal data (a name typed into `text`) — they must not
  be logged.
- `view_candidates` is enforced server-side on every query. The `view_all_candidates`
  permission exists in the model and its scoping effect must be decided here rather than
  silently ignored, since search is the first place a visibility scope becomes
  observable.
- Pagination bounds are a denial-of-service control as well as a UX choice.

## Acceptance criteria

1. Search returns identical results to the current in-memory implementation for a shared
   fixture dataset, across every filter family and both `ANY` and `ALL` modes.
2. Empty filters are ignored; families combine with `AND`; no candidate is returned twice.
3. `ALL` mode over multi-value criteria is correct where a candidate has several matching
   relation rows.
4. Level-less criteria (`level: ''`) match any level.
5. `hasCv` agrees with primary-CV state from KTL-9.
6. Logically deleted candidates never appear.
7. Results are paginated and bounded; the bound is enforced server-side.
8. Presets create, rename, update, delete and track last use; the
   `rrhh.search.presets.v1` key is gone from `src/`.
9. Legacy-shape presets are handled per the decision recorded in design.
10. `rrhh.search.last-filters.v1` still works and is still local.
11. Superseded queries are cancelled, not merely ignored.
12. Search terms do not appear in logs; `view_candidates` fails closed.
13. Backend unit, integration, architecture, security and frontend tests pass, plus the
    candidate-search Playwright flow.

## Deferred decisions

- What `view_all_candidates` scopes to. It is currently in
  [auth.models.ts](src/app/shared/models/auth.models.ts) and granted only to
  `rrhh_admin` and `system_admin`, but nothing in the product restricts visibility, so it
  has no observable effect today. Decide whether search honours it or whether it stays
  inert pending the authorization work.
- Whether free-text search uses `ILIKE`, trigram indexing, or `tsvector`. Decide against
  the migrated dataset size from KTL-7.

## Next step

```
/enrich-us openspec/KTL-10.md
/opsx:new
```
