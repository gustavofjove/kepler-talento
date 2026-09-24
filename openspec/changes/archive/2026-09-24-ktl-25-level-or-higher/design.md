## Context

See `proposal.md` for the user problem. Search currently resolves each non-empty criterion level to
the identifiers whose normalized name exactly matches, then uses correlated `Exists` predicates
against candidate relations. Catalog order already lives in `SortOrder`, and inactive values remain
queryable so existing candidate relations can be resolved. KTL-24 supplies the shared search picker
where the minimum-level label will appear.

The change crosses persistence, saved-filter consumers, frontend presentation, catalog management,
and reference/query-plan tests. It changes interpretation but not the filter JSON shape, endpoint,
database schema, permissions, or result projection.

## Goals / Non-Goals

**Goals:** preserve the existing query shape while resolving a named level into the bounded set of
levels at or above its rank; make that meaning visible wherever search criteria or relevant catalog
ordering are presented; keep ad-hoc search, presets, and position matching semantically identical.

**Non-goals:** adding exact/range/or-lower modes, collapsing repeated criteria, decoupling rank from
display order, changing candidate editing, or introducing a new schema version, table, dependency,
permission, or runtime grant.

## Decisions

### Widen catalog resolution once per search

`ResolveCatalogAsync` will collect the value families and level families used by the request, then
load the named values plus every item in each referenced level family, including inactive items and
their `SortOrder`, in one bounded catalog query. Resolution finds the named level by normalized name
within its expected family and expands `LevelIds` to identifiers whose `SortOrder` is greater than
or equal to that level's order. If the named level is absent, the identifier set stays empty and the
existing predicate fails closed.

This keeps `SkillPredicate`, `LanguagePredicate`, and `ProgramPredicate` as correlated existence
tests using `LevelIds.Contains(relation.LevelId)`. The alternative of joining catalog items into
each outer search predicate would couple ranking to the main query, increase plan complexity, and
risk duplicate outer rows. A second catalog query was also considered; widening the existing small
lookup avoids another round trip and the families contain only a handful of rows.

### Catalog order is the only rank

`SortOrder` is authoritative, including for inactive levels. No rank column or migration is added.
This makes administrative reordering immediately semantic, consistent with the ticket decision and
existing seeded order. A separate rank would reduce accidental semantic changes but would add a
second ordering concept, migration, validation, UI, and audit surface without a current use case.
Because a reorder changes this search rank, the catalog repository forces an optimistic-concurrency
write for every loaded value in the family, including values whose position stays the same. Two
requests based on one family snapshot therefore cannot commit disjoint swaps into a mixed order.

### Preserve criteria and schema version 1

Normalization and persistence keep every criterion as written, including two criteria for one value
at different levels. `FilterSchemaVersion` remains `1` because the serialized representation and
validation rules do not change; all stored documents deliberately adopt the new interpretation.
Collapsing redundant `ANY` criteria was rejected because it would make presets fail to round-trip
and would require mode-dependent rewriting. A version bump was rejected because there is no old
interpretation to preserve after rollout and no document migration to perform.

### Present minimum semantics only in search contexts

Search chips and read-only summaries format a non-empty level through the localized
`search.criteria.level.atLeast` string (for example, `≥ B2`). The picker labels its field with
`search.criteria.level.minimum`. Candidate editing continues to display the factual level without
the suffix. Catalog management shows the localized ordering hint only for the three level families.
Pure summary formatting remains in the sibling logic module, and components continue using their
subscribing catalog hooks.

### Security and data boundaries stay unchanged

The endpoint and handler continue checking a real actor and `candidates.read` before validation,
catalog resolution, or query dispatch. No search term or level is logged. The response keeps its
minimal personal-data projection. There are no RLS, storage, role, grant, or database schema
changes, and no new runtime dependency.

### Test the semantic boundary and query shape

PostgreSQL integration tests will cover all three relation families, highest and inactive levels,
reordering, unknown levels, `ANY`/`ALL`, deduplication, presets, and position matching. The reference
evaluator and parity fixture will use explicit ordered levels. Query-plan evidence will verify that
the candidate query remains an index-backed correlated existence test with a bounded level ID set.
Frontend unit tests cover labels and family-specific hints; Playwright covers a lower minimum
matching a higher seeded level. Existing 401/403 tests provide fail-closed security evidence and
must demonstrate that resolution is not reached.

## Risks / Trade-offs

- **Saved presets and positions can return more candidates without their JSON changing** → Publish
  a KTL-25 release note and display “≥” wherever a search level is summarized.
- **A display reorder changes search meaning immediately** → Show the level-family warning in
  catalog management and retain existing reorder auditing.
- **Reference evaluation can drift from the database query** → Update both implementations and
  retain randomized parity coverage with explicitly ordered fixtures.
- **Widening resolution could degrade the main query** → Materialize only small identifier arrays
  before building predicates and re-capture the PostgreSQL plan.
- **KTL-24 UI files may not yet be present on the implementation branch** → Land KTL-25 after KTL-24
  and treat the dependency as a prerequisite rather than recreating the picker.

## Migration Plan

1. Land after KTL-24 and deploy backend and frontend together so behavior and labels agree.
2. Deploy application code only; no database migration, seed, grant, RLS, or stored-filter rewrite
   runs.
3. Exercise an ad-hoc search, saved preset, and position match against ordered seeded levels, then
   confirm authorization and query-plan evidence.
4. Announce that stored filters with levels can return more candidates.

Rollback is an application rollback. Stored filters remain compatible because their shape never
changes; rolling back restores exact-level interpretation without a data rollback.
