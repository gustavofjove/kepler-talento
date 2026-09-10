# KTL-10 candidate search and saved searches

Filtering, counting, ordering and paging are owned by PostgreSQL. The browser no longer holds
a candidate collection, and no longer loads a candidate's aggregate in order to decide whether
that candidate matches.

## Endpoints

| Method | Route                          | Capability        | Result                               |
| ------ | ------------------------------ | ----------------- | ------------------------------------ |
| POST   | `/api/candidates/search`       | `candidates.read` | one page of matches plus the total   |
| GET    | `/api/search-presets`          | `candidates.read` | the current actor's saved searches   |
| POST   | `/api/search-presets`          | `candidates.read` | `201` with the stored saved search   |
| PUT    | `/api/search-presets/{id}`     | `candidates.read` | renamed and re-filtered saved search |
| DELETE | `/api/search-presets/{id}`     | `candidates.read` | `204`                                |
| POST   | `/api/search-presets/{id}/use` | `candidates.read` | its filters, and records the use     |

Every route authorizes before validating or touching data, so an unauthorized caller cannot
tell a well-formed request from a malformed one, nor an existing saved search from a missing
one, by the shape of the refusal.

Search is a POST despite reading nothing but data. Nested criteria pairs travel badly in a
query string, and — the deciding reason — search terms are personal data that a GET would put
into every proxy and access log that records a URL. The handler writes nothing.

## Request

```jsonc
{
  "filters": {
    "text": "", // case-insensitive literal substring
    "statusValues": ["available"], // empty or all five = unrestricted
    "skillCriteria": [{ "value": "Java", "level": "Avanzado" }],
    "skillMode": "ANY", // or "ALL"
    "languageCriteria": [],
    "languageMode": "ANY",
    "programCriteria": [],
    "programMode": "ANY",
    "hasCv": "", // "", "yes" or "no"
  },
  "page": 1,
  "pageSize": 25,
}
```

### Normalization and validation

Search and saved searches share one definition of "a valid filter value", so a saved search
can never store filters the search endpoint would refuse.

- Blank text and blank criterion values are **dropped**, not refused: the filter panel
  produces a blank line every time a user adds one before choosing its value.
- Criteria are deduplicated on their normalized `(value, level)` pair. Repeating a criterion
  cannot change `ANY`, and under `ALL` it would add a condition already stated.
- An empty criterion level matches **any** level for that value; a non-empty one requires
  both value and level.
- An unknown criterion value matches nothing. It is not an error: refusing it would disclose
  which catalog values exist to a caller who may not read the catalog.
- Unsupported statuses, modes and CV selections are refused with stable codes
  (`search.status.invalid`, `search.mode.invalid`, `search.cv.invalid`). One refusal reports
  every problem in the request, not just the first.
- Text is capped at 200 characters, criterion values at 200, and each family at 25 criteria —
  each `ALL` criterion becomes its own existence condition, so an unbounded list is an
  unbounded query.

### Pagination

| Rule         | Value                                                    |
| ------------ | -------------------------------------------------------- |
| Default page | 1                                                        |
| Default size | 25                                                       |
| Maximum size | 100                                                      |
| Refused      | page < 1, size < 1, size > 100 (`400`, before any query) |
| Order        | `UpdatedAtUtc DESC, Id ASC`                              |

Absent is not the same as invalid: omitting the page size accepts the default, while asking
for a thousand rows is refused rather than quietly reinterpreted. The response reports the
values the server _applied_, so a caller that omitted them learns the defaults.

The identifier tie-breaker is not decoration. Without it, two candidates saved in the same
instant can appear on both of two adjacent pages, or on neither.

`totalCount` and `items` are two statements under read-committed isolation. For unchanged data
they agree exactly; while another user writes, the count may describe a marginally different
population than the page. This is documented rather than prevented — holding a repeatable-read
transaction across both costs more than the discrepancy is worth at this scale.

## Response projection

```jsonc
{
  "items": [
    {
      "candidateId": "…",
      "firstName": "…",
      "lastName": "…",
      "phone": "…",
      "email": "…",
      "status": "available",
      "hasPrimaryCv": true,
      "primaryCvDocumentId": "…",
      "updatedAt": "…",
    },
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 57,
}
```

That is the whole item. No relation collections, no notes, no consent or retention metadata,
no storage key, filename or scan state. `primaryCvDocumentId` is an opaque identifier that
lets the row offer "Abrir CV"; the document capability then applies its own permission and
scan-state checks. It confers no right to the bytes and reveals nothing about where they live.

`hasPrimaryCv` is true when the candidate has a non-removed primary document **in any scan
state** — pending, clean or refused. Downloadability remains KTL-9's decision; KTL-10 never
reads a document's content.

Logically deleted candidates never appear on a page and are never counted.

## Query shape

Filtering starts from `CND_Candidates` with the `IsActive` predicate and uses correlated
`EXISTS` expressions for skills, languages, programs and the primary document. No relation is
ever joined into the outer query.

- `ANY` is those existence conditions combined with `OR` in one `WHERE`.
- `ALL` is one existence condition **per distinct criterion**, so a candidate satisfying two
  criteria through two different relation rows matches — something a single-row comparison
  cannot express.

Duplicate rows are therefore impossible by construction rather than hidden by a `DISTINCT`.
This matters beyond tidiness: a `DISTINCT` covering a join would also make `ALL` and the total
count quietly wrong, which is far harder to notice than a duplicate row.

Criteria are resolved to catalog identifiers once per request against
`CatalogItem.NameNormalized`, so each criterion becomes an identifier comparison on an indexed
column rather than a correlated text join repeated per candidate. Inactive catalog entries are
included deliberately: a value an administrator retired stays meaningful for the candidates who
already hold it.

Free text is an `ILIKE` over first name, last name, email, phone and notes, with `%`, `_` and
the escape character escaped first — so a search for `100%` matches that text rather than
behaving as wildcard syntax.

## Indexes

KTL-10 **adds no index**. Every predicate is already served:

| Predicate                | Index                                       | Owner |
| ------------------------ | ------------------------------------------- | ----- |
| Active candidates, order | `IX_CND_Candidates_IsActive_UpdatedAtUtc`   | KTL-7 |
| Criterion resolution     | `UX_CAT_CatalogItems_Family_NameNormalized` | KTL-6 |
| Relation existence       | the composite catalog foreign keys' indexes | KTL-6 |
| Primary-CV existence     | `UX_CND_Documents_CandidateId_Primary`      | KTL-9 |

Wider `(catalog, candidate, level)` relation indexes were written, measured against the
KTL-7-scale dataset, and removed again: the planner never chose one, so they would have cost
every write and bought nothing. See [`query-plans.md`](./query-plans.md) for the retained
`EXPLAIN (ANALYZE, BUFFERS)` evidence, regenerated by the integration suite.

There is deliberately no `pg_trgm` and no `tsvector`. Free text is a leading-wildcard literal
substring, which no B-tree can serve; at 3 000 candidates the parameterized scan measures
around 8 ms, far cheaper than a new extension's operational weight and write amplification. A
`tsvector` would not be acceptable here in any case: token semantics differ from literal
substring matching, so it would change results rather than accelerate them. **If the retained
evidence stops supporting this, amend the design before adding an index.**

## Saved searches

`ADM_SearchPresets` — `ADM_`, not `CND_`, because this is user-owned configuration, not
candidate data.

| Column                | Shape                  | Note                                        |
| --------------------- | ---------------------- | ------------------------------------------- |
| `Id`                  | uuid, primary key      | opaque API identity                         |
| `OwnerId`             | varchar(200), required | from `ICurrentActor`; never client-supplied |
| `Name`                | varchar(120), required | trimmed display name                        |
| `NormalizedName`      | varchar(120), required | case-folded uniqueness key                  |
| `Filters`             | jsonb, required        | the complete filter value                   |
| `FilterSchemaVersion` | integer, required      | currently 1                                 |
| `CreatedAtUtc`        | timestamptz, required  | server-assigned                             |
| `UpdatedAtUtc`        | timestamptz, required  | server-assigned                             |
| `LastUsedAtUtc`       | timestamptz, nullable  | last successful apply                       |

Constraints: non-blank owner and name, `jsonb_typeof(Filters) = 'object'`, schema version ≥ 1,
and `UpdatedAtUtc >= CreatedAtUtc` with `LastUsedAtUtc <= UpdatedAtUtc`.

`UX_ADM_SearchPresets_Owner_NormalizedName` is unique and is _also_ the listing index — owner
then normalized name is exactly the order the list reads, so a second index would only cost
writes. The unique index is the authority on name conflicts: two concurrent creations can both
find a name free, and only one can store it; the loser gets `409`.

`OwnerId` has no foreign key. The identity schema does not exist yet, and inventing a parallel
user table here would couple saved searches to an identity design nobody has approved. An
authenticated actor without a stable key is refused rather than defaulted to the empty string,
which would pool every such actor's saved searches into one shared owner.

Every repository predicate includes the owner. A cross-owner identifier returns the **same**
`404` as an absent one — code and detail identical — or the API answers questions about other
people's saved searches to anyone willing to guess identifiers.

Name comparison folds case but **not** accents, unlike catalog names. A catalog name is a
shared vocabulary where `Ingles` and `Inglés` must be one entry; a saved search's name is one
person's private label, and refusing "Búsqueda" because they already have "Busqueda" would be
a surprise rather than a safeguard.

## Authorization and visibility

The capability is `candidates.read`, which the frontend's `view_candidates` maps onto. There is
no separate search permission.

`view_all_candidates` is **inert** in KTL-10, by decision rather than omission. The domain
models no recruiter ownership, team or office assignment, so narrowing an actor's results
without that permission would be arbitrary rather than restrictive. Two actors differing only
by it receive identical results, and an integration test asserts that. The permission is
neither removed nor granted to new roles; a future change may activate it only alongside an
explicit domain scope and backfilled data.

## Privacy in diagnostics

Search terms, filter payloads and saved-search names never reach a log. The Serilog enricher
masks `text`, `filters`, `skillCriteria`, `languageCriteria`, `programCriteria`, `name`,
`normalizedName` and `presetName` at every depth, alongside the candidate fields it already
covered. Filter families are masked whole rather than by their inner `value`/`level` members,
which redacts every criterion without masking the word "value" everywhere else.

A search term is at least as personal as the field it matched — "Marta Ruiz" in a log is the
same disclosure whichever direction it arrived from — and a saved search is routinely named
after a person. What still reaches diagnostics: correlation id, actor and preset identifiers,
operation and outcome. Search is read-only and creates no per-result audit rows.

## Database privileges

The runtime role receives `SELECT`, `INSERT`, `UPDATE`, `DELETE` on `ADM_SearchPresets` and
nothing else — no `TRUNCATE`, no DDL, no schema-wide privileges, no role management. Search
itself needs no new privilege: it reads tables the role already holds `SELECT` on. Applying the
migration remains the separate migration role's job.

## Rollback

1. Restore the previous frontend and API binaries. Leave `ADM_SearchPresets` in place: it is
   additive, contains only API-created saved searches, and is harmless to the old build.
2. Only if a database rollback is genuinely required: export and retain the rows under
   restricted operational access, then run the down migration, which revokes the runtime
   grants before dropping the table.
3. Do **not** restore browser preset persistence, and do not broaden database grants as a
   shortcut.
