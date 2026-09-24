# KTL-25 — Level criteria match "this level or higher"

## [original]

Search level "or higher": a level criterion such as «Inglés · B2» should also find candidates
with a higher level (C1, C2), not only B2. Levels are always "or higher", and the order of the
level catalog decides which levels are higher.

_(Captured from the KTL-24 design conversation on 2026-09-23.)_

## [enhanced]

**Status:** Proposed
**Depends on:** KTL-24 (shared catalog value picker). The backend part does not depend on it, but
the search chip that shows «≥ B2» is the KTL-24 picker. Land this ticket after KTL-24.

### Summary

Today a level criterion matches **only that exact level** (`candidate-search` spec, «Multi-value
criteria semantics»; `CandidateSearchQuery.cs:196-212`, which compares `LevelIds.Contains`).
Searching «Inglés · B2» therefore **excludes** candidates with C1 or C2. Recruiters read a level as
a minimum, so the search quietly misses the strongest candidates.

This ticket changes the meaning of a non-empty level to **"this level or any higher level of the same
family"**, where "higher" means **later in the family's configured catalog order**. An empty level
still matches any level. The filter shape does not change.

### User stories

- **As a recruiter**, searching «Inglés · B2» returns candidates with B2, C1 or C2.
- **As a hiring manager**, a position requiring «Excel · Avanzado» lists candidates at Avanzado and
  Experto.
- **As a catalog administrator**, I know that reordering a level family changes what "higher"
  means in search.

### Decisions taken (2026-09-23)

1. **Always "or higher".** There is no per-criterion choice between "exacto" and "o superior". A
   non-empty level always means that level or higher.
2. **Rank = catalog order.** Within a level family (`language_level`, `program_level`,
   `skill_level`), the item with the lowest `SortOrder` is the lowest level. The seeded families are
   already in that order (`CatalogSeedData.cs:78-80`: A1…C2, Básico…Experto). No new column or
   migration.
3. **Saved presets and positions change meaning, deliberately.** Their stored filters stay byte for
   byte the same, but a stored level now also matches higher levels, so they may return more
   candidates. This is accepted and must be announced in the release note.
4. **Inactive levels keep their rank.** Search already resolves inactive catalog values so that
   candidates holding a retired value stay findable (`CandidateSearchQuery.cs:228-231`). A retired
   level therefore still sits in the order and still counts as higher or lower.

### Matching rule

For a criterion `{ value, level }` in a family with levels:

- `level` empty → any relation with `value` (unchanged).
- `level` names a level `L` of the family → any relation with `value` whose level has
  `SortOrder >= L.SortOrder` in the same level family, active or inactive.
- `level` names no known level → the criterion matches nothing (unchanged fail-closed behaviour).
- Tags carry no level (unchanged; a tag criterion with a level is still rejected).

`ANY`/`ALL`, the AND between families, deduplication, "no duplicate candidates" and the minimal
projection are unchanged.

### Backend changes (`backend/`)

- `Infrastructure/Persistence/CandidateSearchQuery.cs`
  - `ResolveCatalogAsync` also loads, for every level family a criterion names, that family's items
    with their `SortOrder` (one extra small query at most, or the same query widened; level families
    are a handful of rows).
  - `Resolve` expands `LevelIds` from "the ids named `L`" to "the ids of every level in the family
    ranked at or above `L`". The predicates (`SkillPredicate`, `LanguagePredicate`,
    `ProgramPredicate`) do not change: they already test `LevelIds.Contains(relation.LevelId)`, so
    the query stays a correlated existence test and cannot produce duplicates.
- `Application/Features/Search/SearchFilterContract.cs`: update the XML comments ("an empty level
  means any level; a level means that level or higher"). Normalization is unchanged. Two criteria
  for the same value at different levels still both exist; under `ANY` the lower one is redundant,
  under `ALL` the higher one is stricter. No collapsing rule is added (see design decisions).
- No endpoint, request/response shape, permission, table, migration or `ktl_runtime` grant change.
- `SearchFilterNormalization.FilterSchemaVersion` stays `1`: the stored shape does not change (see
  design decisions).

### UI changes (`frontend/`)

- The level shown on an added search chip (KTL-24 picker) and in
  `SearchCriteriaSummary` reads **«≥ B2»**.
- The level choice in a search chip's popover is labelled «Nivel mínimo». The candidate edit page is
  unaffected: a candidate's level is a fact, not a minimum.
- Catalog management page (`features/catalogs/pages/catalog-management-page.tsx`): for the three
  level families, a hint under the list: «El orden define qué niveles se consideran superiores en la
  búsqueda: el primero es el más bajo.»

**Copy** (`src/assets/i18n/es.json`; `en.json` optional):

| Key                                  | Spanish                                                                                         |
| ------------------------------------ | ----------------------------------------------------------------------------------------------- |
| `search.criteria.level.atLeast`      | ≥ {{level}}                                                                                     |
| `search.criteria.level.minimum`      | Nivel mínimo                                                                                    |
| `catalogs.management.levelOrderHint` | El orden define qué niveles se consideran superiores en la búsqueda: el primero es el más bajo. |

### Acceptance criteria

```gherkin
Scenario: A level matches higher levels
  Given candidates with Inglés at B1, B2 and C1
  When an authorized actor searches for Inglés at level B2
  Then the candidates with B2 and C1 are returned and the B1 candidate is not

Scenario: The highest level matches only itself
  Given candidates with Inglés at C1 and C2
  When an authorized actor searches for Inglés at level C2
  Then only the C2 candidate is returned

Scenario: Empty level is unchanged
  When a criterion names Inglés with an empty level
  Then every candidate holding Inglés at any level is returned

Scenario: Rank follows the configured order
  Given an administrator reorders skill_level so that "Experto" is before "Alto"
  When an actor searches for a skill at level "Alto"
  Then a candidate at "Experto" is no longer returned

Scenario: A retired level keeps its rank
  Given the level C1 is deactivated and a candidate still holds Inglés at C1
  When an actor searches for Inglés at level B2
  Then that candidate is returned

Scenario: ALL across levels
  Given a skill family in ALL mode with "Java · Medio" and "SQL · Alto"
  Then a candidate with Java Experto and SQL Alto matches once, and one with SQL Medio does not

Scenario: Unknown level still fails closed
  When a criterion names a level that does not exist in the family
  Then the criterion matches no candidate

Scenario: Saved presets and positions use the new meaning
  Given a preset and a position whose stored filters contain "Inglés · B2"
  When the preset is applied or the position's matches are listed
  Then candidates at B2 or higher are returned, with no change to the stored filters

Scenario: Authorization is unchanged
  When an unauthenticated caller or one without candidates.read searches
  Then the request is refused before any criterion is resolved

Scenario: Labels
  Then an added search chip and the criteria summary show "≥ B2"
  And the catalog page shows the order hint on the three level families only
```

### Test coverage

- **Backend integration** (`backend/Tests/IntegrationTests/`, Testcontainers PostgreSQL):
  - `SearchApiTests.cs`: or-higher across languages, skills and programs; highest level;
    reorder changes the result; retired level keeps its rank; unknown level; ANY and ALL; no
    duplicates when a candidate holds several matching levels (impossible per value today because
    of relation uniqueness, but asserted).
  - `ReferenceSearchEvaluator.cs` and `SearchParityFixture.cs`: the reference evaluator must
    implement the same rank rule, otherwise the parity tests will disagree with the query. The
    fixture levels (`Avanzado`, `Alto`) need a defined order.
  - `SearchQueryPlanTests.cs`: re-capture the plan and confirm the predicate is still an index-backed
    existence test (`LevelId IN (...)`) with no join into the outer query.
  - Position matching tests, if any assert exact-level behaviour.
- **Backend unit:** any unit test that documents exact matching.
- **Frontend unit:** `search-criteria-summary.spec.tsx` and the KTL-24 picker spec for the
  «≥ B2» chip and summary labels; catalog management page spec for the hint on level families only.
- **E2E:** `advanced-search.spec.ts`: a level criterion returns a candidate with a higher level
  (seed data A1…C2); restore seed data afterwards. `data-testid` or role selectors only.
- **Security:** existing search authorization tests (401/403 before resolution) must still pass;
  no new endpoint.

### Non-functional requirements

- **Security and personal data:** no new data exposed; search terms and levels are still never
  logged. Authorization still runs before validation and resolution.
- **Performance:** at most one extra lookup of a few catalog rows per request; `LevelIds` grows from
  one id to at most the family size (≤ ~10). The existing `(LevelId, LevelFamily)` relation indexes
  still apply.
- **Accessibility / localization:** new copy in `es.json` with correct accents; no hardcoded JSX.

### Documentation

- `openspec/specs/candidate-search/spec.md` — «Multi-value criteria semantics»: replace "a
  non-empty level SHALL require the same value and level" with the or-higher rule and the rank
  definition; add scenarios for higher level, reorder and retired level.
- `openspec/specs/business-catalogs/spec.md` — «Catalog family reordering»: the order of a level
  family defines its rank for search.
- `docs/ktl-10/search.md` — update the level semantics in the contract description.
- `docs/ktl-25/` — release note stating that saved presets and positions with levels may now
  return more candidates.
- `README.md` (Spanish) — only if it describes level matching.

### Out of scope

- A per-criterion choice between "exacto" and "o superior" (decision 1).
- A separate rank column decoupled from display order (decision 2).
- "Or lower" or level ranges.
- Changing how candidate levels are stored or edited.

### Decisions to make in the design

1. **Criterion collapsing.** With or-higher, `Inglés · B2` makes `Inglés · C1` redundant under `ANY`.
   Recommended: do not collapse; keep stored filters exactly as written so presets round-trip.
2. **`FilterSchemaVersion`.** Recommended: keep `1`, since the stored shape is unchanged and the
   meaning change applies to every document equally; record the reasoning.
3. **Resolution query.** Widen the existing catalog lookup to fetch the named level families
   whole, or run a second query only when a criterion has a level.

### Risks

- **Silent result changes for saved work:** mitigated by the release note and the «≥ B2» chip
  and summary labels.
- **Order is now semantic:** an administrator reordering a level family for display changes search
  results. Mitigated by the catalog page hint; reorders are already audited.
- **Parity evaluator drift:** if the reference evaluator is not updated, the parity tests fail or,
  worse, are adjusted to pass without checking the rule.
