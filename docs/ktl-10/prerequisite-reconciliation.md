# KTL-10 prerequisite reconciliation (KTL-6 → KTL-9)

Checked before writing any KTL-10 code, as the design requires: the search query is built on the
final mapped columns of the preceding slices, not on the shapes assumed while KTL-10 was planned.

## Candidate core (`CND_Candidates`, KTL-7/KTL-8)

| Design assumption          | Actual mapping                                                                      | Verdict                                                                     |
| -------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| Logical deletion predicate | `IsActive` boolean, with `CK_CND_Candidates_Deleted` pairing it to `DeletedAtUtc`   | Matches. The query filters on `IsActive`.                                   |
| Deterministic order        | `UpdatedAtUtc` (timestamptz) plus `Id` (uuid v7)                                    | Matches.                                                                    |
| Parity text fields         | `FirstName`, `LastName`, `Email`, `Phone`, `Notes` — all `IsRequired()`, never null | Matches the browser evaluator's field list exactly.                         |
| Status                     | `Status` text, `CK_CND_Candidates_Status` over the five `CandidateStatuses` values  | Matches.                                                                    |
| Existing index             | `IX_CND_Candidates_IsActive_UpdatedAtUtc`                                           | Already covers the base scan and ordering — KTL-10 adds no duplicate of it. |

## Relations (`CND_CandidateSkills` / `Languages` / `Programs`, KTL-6/KTL-7)

Deviation from the design's wording, resolved in KTL-10's favour without changing behaviour:

- The design speaks of comparing "relation values" case-insensitively. The relations do **not**
  store values; they store `SkillId`/`LanguageId`/`ProgramId` and `LevelId` as composite foreign
  keys onto `CAT_CatalogItems (Id, Family)`. The comparison therefore happens on the catalog row.
- `CatalogItem.NameNormalized` is a persisted column produced by `CatalogName.Normalize`
  (trim, accent fold, lowercase). It is what KTL-10 compares against, so the match is
  index-backed instead of a per-row `lower()` over `NameEs`.
- The browser evaluator compared with `trim().toLocaleLowerCase()` — no accent fold. The
  normalized column is therefore _slightly more lenient_ (`"ingles"` also matches `"Inglés"`).
  Criterion values reach the API from catalog-backed selects, so the two agree on every value the
  UI can actually produce; the fixture parity tests assert this rather than assume it.
- Each relation carries a fixed `*Family` column pinned by a check constraint, so a criterion can
  never resolve across families.

## Primary CV (`CND_Documents`, KTL-9)

- There is no "removed" flag on a document: detaching one deletes the row. "Non-removed primary
  metadata" therefore reads as "a `CND_Documents` row for the candidate with `IsPrimary`".
- `UX_CND_Documents_CandidateId_Primary` (unique, filtered on `IsPrimary`) guarantees at most one,
  so the existence test cannot multiply rows and the identifier lookup is single-valued.
- `ScanState` is the KTL-9 enum stored as text: `PendingScan`, `Clean`, `Infected`, `Rejected`,
  `ScanFailed`. The spec's _pending / available / refused_ map to `PendingScan` / `Clean` /
  {`Infected`, `Rejected`, `ScanFailed`}. `hasCv` ignores all of them, exactly as specified;
  downloadability stays KTL-9's business and KTL-10 never reads document bytes.

## Authorization (KTL-5 → KTL-9)

- The canonical permission is `Permissions.CandidatesRead` (`candidates.read`); the frontend's
  `view_candidates` maps onto it. Routes authorize before dispatching, as
  `CandidateEndpoints.Require` already does.
- `ICurrentActor.ExternalKey` is the only stable actor identifier available. It is `string?`, so
  preset ownership additionally requires a non-blank key — an authenticated actor without one is
  refused rather than given a shared empty-string owner.

## Incompatible assumptions found

None that block implementation. The two adjustments above (catalog-row comparison via
`NameNormalized`, and physical rather than logical document removal) are recorded here and
reflected in the code and its tests; neither changes an observable requirement.
