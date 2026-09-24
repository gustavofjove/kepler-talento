## Why

A level criterion matches only that exact level today, so searching «Inglés · B2» leaves out every
candidate with C1 or C2. Recruiters read a level as a minimum. The search, saved presets and live
position matches therefore quietly miss the strongest candidates. KTL-25 makes a level mean "this
level or higher", with "higher" taken from the catalog order administrators already maintain.

## What Changes

- **BREAKING (search semantics):** a non-empty level on a skill, language or program criterion
  matches relations whose level is **the same or later in that level family's catalog order**. An
  empty level still matches any level. Unknown levels still match nothing. Tags still carry no level.
- The order of each level family (`language_level`, `program_level`, `skill_level`) is its rank: the
  first value is the lowest. Deactivated levels keep their place in the rank.
- Saved presets and position requirements are not rewritten. Because the meaning changes, those
  with levels may now return more candidates. The release note announces this.
- The filter contract (`{ value, level }` per criterion, `ANY`/`ALL` per family), endpoints,
  permissions and stored data are unchanged.
- UI:
  - Added search chips and the read-only criteria summary show «≥ B2».
  - The search chip's level choice is labelled «Nivel mínimo».
  - The catalog management page explains, on the three level families only, that their order
    defines what counts as higher in search.

**Actors.**

- Recruiters and hiring managers holding `candidates.read`, using search, presets and position
  matches.
- Catalog administrators holding the catalog management permission, who reorder level families.

**Key entities.**

- Search criterion `{ value, level }`.
- Catalog level families and their `SortOrder`.
- Candidate relations (language, skill, program) referencing a level.

**Assumptions.**

- Every level family is maintained lowest to highest. The seeded families already are (A1…C2,
  Básico…Experto).
- Level families are small (under about ten values), so expanding one level to "it and every higher
  one" stays a short identifier list.

**Edge cases.**

- The highest level of a family matches only itself.
- A candidate holds a level that has been deactivated: it still ranks, so it is found by lower
  criteria.
- An administrator reorders a level family: results follow the new order from the next search.
- The same value is given twice at different levels: both criteria are kept as written. Under `ANY`
  the lower one decides; under `ALL` the higher one does.
- A level name that exists in another level family but not in this one matches nothing, as today.

**Success criteria.**

1. A criterion at level L returns exactly the candidates holding the value at L or any later level
   of the family, for languages, skills and programs, in `ANY` and `ALL` modes, with no duplicates.
2. Presets and positions return the same results as an equivalent ad-hoc search, with no change to
   their stored filters.
3. The search query remains a correlated existence test backed by the existing relation indexes
   (plan evidence re-captured).
4. Unauthenticated and unauthorized searches are still refused before any criterion is resolved.
5. Build, backend, frontend, e2e, lint and format checks pass.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `candidate-search`: «Multi-value criteria semantics». A non-empty level matches that level or any
  higher level of its family, ranked by catalog order, including deactivated levels.
- `business-catalogs`: «Catalog family reordering». The order of a level family defines its rank
  for search, and the management page states this for level families.

## Impact

- **Backend:**
  - `backend/Infrastructure/Persistence/CandidateSearchQuery.cs` (level resolution expands to the
    ranked set; predicates unchanged)
  - comments in `backend/Application/Features/Search/SearchFilterContract.cs`
  - no endpoint, contract, migration, grant or permission change
- **Frontend:**
  - `features/search/components/search-criteria.logic.ts` (summary label)
  - the KTL-24 search picker adapter `criteria-group.tsx` (chip label, «Nivel mínimo»)
  - `features/catalogs/pages/catalog-management-page.tsx` (hint)
  - `es.json`
- **Tests:**
  - `SearchApiTests`, `ReferenceSearchEvaluator` / `SearchParityFixture`, `SearchQueryPlanTests`
    and `PositionApiTests` where they assume exact levels
  - frontend summary, picker and catalog page specs
  - e2e `advanced-search`
- **Dependency:** KTL-24 must be merged first; the chip label lives in its picker.
- **Personal data, RLS, storage, roles.**
  - No new data is read or exposed. Results keep the minimal projection, and search terms and levels
    are still never logged (principle 1).
  - No RLS policy, grant, storage access or role definition changes. Search still fails closed
    before validation (principle 3).
- **Docs:**
  - `candidate-search` and `business-catalogs` specs (through deltas)
  - `docs/ktl-10/search.md` (level semantics)
  - `docs/ktl-25/release-notes.md` (the meaning change for saved presets and positions)
  - `README.md` only if it describes level matching
