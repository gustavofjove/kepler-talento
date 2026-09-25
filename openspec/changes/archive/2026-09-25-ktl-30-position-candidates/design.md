## Context

See `proposal.md` for motivation and the delta specs for normative behavior. The source brief is
`openspec/KTL-30.md`.

KTL-15 deliberately shipped positions without any candidate association. The pieces this change
builds on:

- **Position aggregate and repository.** `Domain/Positions/Position.cs` owns the aggregate.
  `IPositionRepository` stages writes and records one audit event per save through
  `SaveAsync(eventType)`, mapping PostgreSQL unique, check and FK violations to
  `PositionSaveOutcome` values.
- **Guards.** Handlers call `PositionGuards.RequireRead/RequireManage`, and endpoints use
  `RequireAuthorization(Permissions.Positions*)`.
- **Detail page.** `position-detail-page.tsx` runs the live match search through
  `CandidateSearchService` and renders `SearchResults`.
- **`SearchResults`.** It is shared with the advanced search page and is one of two files still
  listed in `LEGACY_HARDCODED_COPY`.
- **Candidate page.** Since KTL-29 it is a single page of panels with one-at-a-time edit mode,
  coordinated by `candidate-detail-page.tsx`.
- **Audit rows.** They carry a single `SubjectId` (max 100 characters) and a type from
  `AuditEventTypes.All`. The Auditoría screen labels types through
  `admin.audit.eventType.<type>` keys.
- **Physical deletes.** AGENTS.md no longer forbids physical deletes. `openspec/config.yaml`
  still requires candidates to be removed logically, and `"OPS_Positions"`, `"CND_Candidates"`
  and `"CAT_CatalogItems"` keep their revoked `DELETE`.

## Goals / Non-Goals

**Goals:**

- One new association entity with a small, guarded write surface. Positions and candidates keep
  their existing contracts.
- Complete, bounded link lists, so that the «Añadido» state and both panels never reason from a
  partial page.
- A physical link delete that is still traceable through the audit trail.
- Reuse the existing search endpoint for the candidate picker, and the position list endpoint
  for the position picker. No new search dialect.
- Retire `search-results.tsx` from the hardcoded-copy exemption as a side effect of touching it.

**Non-Goals:**

- Stage history, per-stage timestamps or notes, and kanban views.
- Syncing a stage to the candidate status.
- Changing the audit schema (for example, a second subject column).
- A generic picker component shared with other features. The two pickers are small, and each
  owns its own query.
- Server-side annotation of search results with link membership.

## Decisions

### D1. `PositionCandidate` is a separate entity keyed by its own id, not part of the `Position` aggregate

`Domain/Positions/PositionCandidate.cs` has these members:

- `Id`: from `Guid.CreateVersion7()`;
- `PositionId` and `CandidateId`;
- `Stage`: from `PositionCandidateStages`, a closed vocabulary like `PositionStatuses`, with
  `All`, `IsKnown` and the display order;
- `AddedAtUtc` and `UpdatedAtUtc`;
- `Version`: mapped to `xmin`.

It exposes `ChangeStage(stage, now)`.

Keeping it out of the `Position` aggregate means a stage change never bumps the position's
`xmin`. Otherwise every restage would make an open position editor stale, and every position
edit would invalidate stage selectors.

The closed-position and inactive-candidate preconditions live in the handlers. They read the
position and the candidate in the same request, rather than the entity holding references.

_Alternative considered:_ a composite key `(PositionId, CandidateId)` with no surrogate id. It
was rejected to keep EF concurrency and audit mapping uniform with the other entities. The pair
still carries a unique index, which is the real duplicate guard (D4).

### D2. Endpoints and slices

All five routes are mapped from `Web/Features/Positions/PositionCandidateEndpoints.cs`
(`MapPositionCandidateEndpoints`, registered in `Program.cs`). The four position-scoped routes
use the group `/api/positions/{id:guid}/candidates`. The candidate-scoped read is mapped in the
same file with `MapGet("/api/candidates/{candidateId:guid}/positions")`, because the Positions
slice owns the data and its guards.

They live in their own file rather than in `PositionEndpoints.cs` so that a position itself
still has no delete route. The KTL-15 security spec keeps asserting exactly that, and the KTL-30
spec asserts the one scoped link delete.

| Route                                                    | Slice file                        | Request                                                                         |
| -------------------------------------------------------- | --------------------------------- | ------------------------------------------------------------------------------- |
| `GET /api/positions/{id}/candidates`                     | `ListPositionCandidates.cs`       | `ListPositionCandidatesQuery(PositionId)`                                       |
| `POST /api/positions/{id}/candidates`                    | `AddPositionCandidate.cs`         | `AddPositionCandidateCommand(PositionId, CandidateId?)`                         |
| `PUT /api/positions/{id}/candidates/{candidateId}/stage` | `ChangePositionCandidateStage.cs` | `ChangePositionCandidateStageCommand(PositionId, CandidateId, Stage?, Version)` |
| `DELETE /api/positions/{id}/candidates/{candidateId}`    | `RemovePositionCandidate.cs`      | `RemovePositionCandidateCommand(PositionId, CandidateId)`                       |
| `GET /api/candidates/{candidateId}/positions`            | `ListCandidatePositions.cs`       | `ListCandidatePositionsQuery(CandidateId)`                                      |

**Authorization.** Each endpoint has to require two permissions, and `RequireAuthorization`
takes a single policy name. Add two policies:

- `PositionCandidateEndpoints.ReadPolicy` (`positions.candidates.read`): `positions.read` +
  `candidates.read`;
- `PositionCandidateEndpoints.ManagePolicy` (`positions.candidates.manage`): `positions.manage` +
  `candidates.read`.

These are policy names, not permissions: no new value enters the permission vocabulary.

Register them in the same place the existing permission policies are registered, so the
authorization middleware refuses before binding and validation, as KTL-15 does. Handlers repeat
the check through `PositionGuards.RequireReadCandidates(actor)` and
`PositionGuards.RequireManageCandidates(actor)`.

**Contracts.** Responses are `PositionCandidateResponse` and `CandidatePositionResponse`
records in `PositionContract.cs`. New stable codes, in the existing `PositionErrors` namespace:

| Code                                      | HTTP | Meaning                            |
| ----------------------------------------- | ---- | ---------------------------------- |
| `position.candidate.already_linked`       | 409  | The pair already exists            |
| `position.candidate.position_closed`      | 409  | The position is `closed`           |
| `position.candidate.candidate_inactive`   | 409  | The candidate is logically removed |
| `position.candidate.limit_reached`        | 409  | The position holds 500 links       |
| `position.candidate.stage_invalid`        | 400  | Stage outside the vocabulary       |
| `position.candidate.candidate_required`   | 400  | No candidate id in an add          |
| `position.candidate.not_found`            | 404  | Link does not exist                |
| `position.candidate.concurrency_conflict` | 409  | Stale version on a stage change    |
| `candidate.not_found`                     | 404  | Reused from the candidate slice    |
| `position.not_found`                      | 404  | Reused                             |

Server messages stay Spanish, like the existing ones. The SPA reports a failed link write with
`useErrorToast()`, which shows the API's Spanish problem message and falls back to an `es.json`
key. This is the same pattern the position form uses for its title and concurrency conflicts, so
no per-code client mapping is added.

### D3. Repository: extend `IPositionRepository` rather than add a second one

Add these members to `IPositionRepository` and `PositionRepository`:

- `ListCandidatesAsync(positionId)` and `ListForCandidateAsync(candidateId)`: projections
  straight to the response records, joining `CND_Candidates` for names and the active flag, or
  `OPS_Positions` for title and status. They never select `Requirements` or `Description`;
- `FindLinkAsync(positionId, candidateId)`;
- `CountLinksAsync(positionId)`;
- `AddLink(link)` and `RemoveLink(link)`;
- `SaveLinkAsync(eventType, positionId, candidateId)`: returns the existing `PositionSaveOutcome`
  extended with `AlreadyLinked`.

One repository keeps a single `DbContext` unit of work and one place that translates PostgreSQL
errors. The position's open state and the candidate's existence and active flag are read by
`IsPositionOpenAsync` and `IsCandidateActiveAsync`: no-tracking scalar projections, so a link
write never loads or tracks a whole position or candidate. `ICandidateRepository.FindCoreAsync`
was considered and rejected because it tracks the candidate entity in the shared unit of work.
`FindCandidateItemAsync` returns the single-row projection that add and stage change respond
with.

Ordering is applied in SQL:

- position list: stage order through a `CASE` over the vocabulary, then `AddedAtUtc DESC`, then
  `CandidateId`;
- candidate list: `open` first, then `AddedAtUtc DESC`, then `PositionId`.

### D4. Duplicates and the cap

**Duplicates.** The unique index `UX_OPS_PositionCandidates_PositionId_CandidateId` is the
authority. The handler also checks `FindLinkAsync` first for a clean error in the common case.
A concurrent race surfaces as a unique violation, which `SaveLinkAsync` maps to
`AlreadyLinked`, and the handler turns that into 409.

**The 500 cap.** It is checked with `CountLinksAsync` before insert. Two concurrent adds at 499
can therefore both succeed, reaching 501. This is accepted: the cap exists to bound the list
response, not as a business invariant, and one overshoot does not threaten that. A serializable
transaction or an advisory lock would add contention for no user-visible benefit.

_Alternative considered:_ a trigger enforcing the cap. It was rejected because business rules in
triggers are invisible to the application tests and to the Problem Details mapping.

### D5. The link list is complete and unpaged, and the client derives «Añadido» from it

`GET /api/positions/{id}/candidates` returns every link, at most 500. The position page loads it
once, keeps it in page state, and builds a `Set<candidateId>` that the match rows consult. After
an add, a stage change or a removal, the page updates the list from the server response:

- **add:** the `201` body is inserted;
- **stage change:** the `200` body replaces the row;
- **removal:** after the `204`, the row is dropped;
- **409 on a stage change:** the whole list is reloaded.

_Alternatives considered:_

- A paged link list plus `GET …/candidates/ids`: two endpoints for one small bounded set.
- A `linked` flag on search results: this would couple the candidate-search contract to
  positions and change a shared, security-reviewed projection.

### D6. Audit subject is the composite `{positionId:N}:{candidateId:N}`

`AuditEvent` has one `SubjectId`. The link id alone is useless after a physical delete. The
position id alone loses the candidate, and the candidate id alone loses the position.

The events therefore use `"{positionId:N}:{candidateId:N}"`, 65 characters, which fits the 100
limit. The format carries only opaque ids, so it complies with "identifiers and codes only".

Register the three types in `PositionAuditEvents` and `AuditEventTypes.All`, and add
`admin.audit.eventType.position.candidate_*` labels. The stage value is never recorded.

_Trade-off:_ the Auditoría subject filter matches exactly, so searching by a bare candidate or
position id will not find link events. Filtering by event type and time still works. Changing
the audit schema, for example by adding a secondary subject, is out of scope. It is noted as a
follow-up if users need per-candidate audit timelines.

### D7. Schema and grants

Generate the migration `AddPositionCandidates` with `dotnet ef migrations add`. It creates the
table and adds the grants through `migrationBuilder.Sql`, following the KTL-15 migration.

**Table `"OPS_PositionCandidates"`:**

- `Id` uuid PK;
- `PositionId` uuid, FK → `"OPS_Positions"("Id")` `ON DELETE RESTRICT`;
- `CandidateId` uuid, FK → `"CND_Candidates"("Id")` `ON DELETE RESTRICT`;
- `Stage` varchar(32) with `CK_OPS_PositionCandidates_Stage` restricting it to the vocabulary;
- `AddedAtUtc` and `UpdatedAtUtc` timestamptz, with `CK_…_Timestamps` requiring
  `UpdatedAtUtc >= AddedAtUtc`;
- `xmin`.

**Indexes:** unique `(PositionId, CandidateId)`, which also serves the position list, and
`(CandidateId)`.

**Grants:**

- `GRANT SELECT, INSERT, UPDATE, DELETE ON "OPS_PositionCandidates" TO ktl_runtime`;
- `REVOKE TRUNCATE ON "OPS_PositionCandidates" FROM ktl_runtime`.

`Down` drops the table. No seeded role or permission changes.

**Why `DELETE` is granted here.** The link is correction-grade association data with no
retention requirement of its own. Its removal is audited (D6), and it follows the
`CND_CandidateTags` and `ADM_SearchPresets` precedent. The simpler alternative, a soft-remove
flag, was considered and rejected by the product owner: a mistaken add should leave no trace
beyond the audit trail. Mitigation: `DELETE` is scoped to this table, the FKs are `RESTRICT`,
and a security test asserts `DELETE` stays revoked on positions and candidates.

### D8. Frontend structure

**Models and service.** `position.models.ts` gains `PositionCandidateStage`, `POSITION_CANDIDATE_STAGES`
(the ordered list), `PositionCandidate` and `CandidatePosition`. `PositionService` gains
`listCandidates`, `addCandidate`, `changeStage`, `removeCandidate` and `listForCandidate`
through `ApiTransport`. No signal is needed: both panels own page-local state, and neither list
is read by other components.

**New components** under `features/positions/components/`:

- `position-candidates-panel.tsx`: the list, the stage selector, removal and «Añadir candidato»;
- `position-stage-select.tsx`: a labelled native `<select>`, shared by both pages;
- `position-candidate-picker.tsx`: a modal that uses `CandidateSearchService.search({ ...EMPTY_SEARCH_FILTERS, text }, { page: 1 })`,
  debounced and aborting stale requests like the detail page does;
- `position-candidates.logic.ts`: stage order, sorting and the linked-id set.

**Candidate page.** `features/candidates/components/candidate-positions-panel.tsx` holds the
panel, and `position-picker.tsx` the modal, which queries
`positionService.search({ status: 'open', text, pageSize: 10 })`. `search` is a new side-effect-free
variant of `list`: `list` publishes the page into the service signal that the positions list page
renders, and a picker must not overwrite it. The panel is a plain
`article.panel` and does not use `CandidatePanel`, so KTL-29's edit-mode coordinator is
untouched. It is placed between Experiencia and Notas. It renders only when
`usePermission('positions.read')` is true, and receives the candidate's active flag to hide
«Añadir a posición».

**`SearchResults`.** It gains `renderRowAction?: (result: SearchResult) => ReactNode`, rendered
first in the actions cell. All its literals move to `search.results.*`
keys in `es.json` with identical Spanish text, and the file is removed from
`LEGACY_HARDCODED_COPY`. The advanced search page passes no action. The position page passes a
button («Añadir») that reads the linked-id set and `canManage && position.status === 'open'`.
It also passes `showOpenCv={false}`, because the position page offers no «Abrir CV».

**Rows open their record.** `shared/components/row-link.ts` (`useRowLink`) makes a table row
navigate on click. It is used by `SearchResults`, the position's candidates table and the
candidate's positions table.

- It ignores clicks on links, buttons and form controls, and clicks made while text is selected.
- Ctrl/⌘-click and middle-click open a new tab.
- The name or title stays a real `Link`, styled plain with `.row-link`, so keyboard and
  screen-reader users reach the same destination. The row handler is a mouse convenience only.
- «Ver»/«Detalle» buttons are removed, and phones in these tables are plain text. The e-mail
  stays a `mailto` link.

_Alternatives considered:_ a name link plus a button (redundant, and the name link sat directly
above the e-mail link), or a button only (a smaller hit target).

**Dialogs.** Both pickers reuse the modal markup and `modal.css` already used by
`confirm-dialog.tsx`. They get focus trapping, focus return, Escape to close, a labelled search
input, and a result list of buttons with `aria-disabled` for linked items.

**Errors.** Errors go through `useErrorToast()`. The 409 codes from D2 map to specific
`positions.candidates.errors.*` keys, with the generic key as fallback.

### D9. Tests and e2e data

**Backend unit tests:**

- every handler's guard, in both permission combinations, before validation;
- the vocabulary;
- the preconditions (closed, inactive, cap, duplicate);
- the audit subject format.

**Backend integration tests** use Testcontainers:

- all five endpoints and the ordering;
- the concurrent-add race: two parallel `POST`s, and exactly one row;
- the stale stage change;
- delete plus audit row;
- the grant matrix, as `ktl_runtime`:
  - `DELETE` on links succeeds;
  - `TRUNCATE` on links fails;
  - `DELETE` on `"OPS_Positions"` and `"CND_Candidates"` fails;
- the FK `RESTRICT` behavior;
- the query plans of both lists, which use the indexes and never touch `Requirements`.

**Frontend security specs** extend the position matrix with the five routes.

**E2E.** `frontend/tests/e2e/position-candidates.spec.ts` creates a `Date.now()`-marked position
and candidates. `scripts/e2e-cleanup.sql` gains
`delete from "OPS_PositionCandidates" where "PositionId" in (…marked positions…) or "CandidateId" in (select "Id" from e2e_candidates);`
before the existing position and candidate deletes, since the FKs are `RESTRICT`.

## Risks / Trade-offs

- **[Cap overshoot under concurrency]** → The count check is not serializable, so two
  concurrent adds at the limit can both succeed. This is bounded by one row per concurrent
  request, and the list stays small (D4).
- **[Audit subject is composite]** → Exact subject filtering by a candidate id misses link
  events. Documented in `docs/ktl-30/position-candidates.md`, and a follow-up if needed (D6).
- **[Physical delete loses the stage history]** → That is intended. Only the removal event
  remains, and HR uses `rejected` to keep a record.
- **[`SearchResults` copy migration could change rendered text]** → The keys hold identical
  strings. A unit spec asserts the search page renders the same literals, and the existing
  search e2e specs still pass.
- **[Stale «Añadido» state when another user edits links]** → The state reflects the page load
  plus this user's own changes. A duplicate add from a stale page gets `already_linked`, which
  the UI reports before reloading the list.
- **[Candidate FK blocks privileged hard deletes]** → Candidates are never hard-deleted in normal
  operation. Operator scripts, including e2e cleanup, must delete links first.

## Migration Plan

1. Deploy the migration through the `migrator` container. It is additive: a new table and grants.
2. Deploy the API and the SPA together. The old SPA never calls the new routes, so the order
   within one release does not matter.
3. **Rollback:**
   - revert the API and SPA;
   - run the migration's `Down`, which drops `"OPS_PositionCandidates"` and with it every link
     created since the deploy.

   Take a backup first if links must be kept (`docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md`).
