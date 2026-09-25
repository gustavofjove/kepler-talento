## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-30` from `main`, named after the ticket file `openspec/KTL-30.md`. (Proposal: traceable KTL-30 delivery.)

## 1. Domain and persistence

- [x] 1.1 Add `Domain/Positions/PositionCandidate.cs`:
  - the `PositionCandidateStages` closed vocabulary, in order `new`, `shortlisted`, `interview`, `hired`, `rejected`, with `All`, `IsKnown` and the order;
  - the entity with `ChangeStage`;
  - `PositionAuditEvents.CandidateAdded`, `CandidateStageChanged` and `CandidateRemoved`, registered in `AuditEventTypes.All`.

  (Design D1, D6. Spec: stage vocabulary; link changes are audited.)

- [x] 1.2 Add `PositionCandidateConfiguration.cs`:
  - quoted `"OPS_PositionCandidates"`;
  - `RESTRICT` FKs to positions and candidates;
  - stage and timestamp checks;
  - unique `(PositionId, CandidateId)` and `(CandidateId)` indexes;
  - the `xmin` version.

  Register the `DbSet` in `ApplicationDbContext`. (Design D7. Spec: position workflow persistence uses least privilege.)

- [x] 1.3 Generate the `AddPositionCandidates` migration with `dotnet ef migrations add`. Add the `GRANT SELECT, INSERT, UPDATE, DELETE` and `REVOKE TRUNCATE` for `ktl_runtime` on the link table only, and a `Down` that drops it. Inspect the generated SQL. (Design D7.)
- [x] 1.4 Extend `IPositionRepository` and `PositionRepository` (design D3):
  - `ListCandidatesAsync` and `ListForCandidateAsync`, as projections with SQL ordering and without requirements or description;
  - `FindLinkAsync` and `CountLinksAsync`;
  - `AddLink` and `RemoveLink`;
  - `SaveLinkAsync`, which writes the audit event with the composite subject and maps a unique violation to `AlreadyLinked`.

## 2. Application slices

- [x] 2.1 Add the read slices, and the response records in `PositionContract.cs`:
  - add `PositionGuards.RequireReadCandidates` and `RequireManageCandidates`;
  - add `PositionErrors` codes (design D2);
  - add `ListPositionCandidates.cs`, which returns 404 for an unknown position;
  - add `ListCandidatePositions.cs`, which returns 404 for an unknown candidate.

  (Spec: position candidate API; authorization fails closed.)

- [x] 2.2 Add `AddPositionCandidate.cs` with its validator. It guards, then checks in order:
  - position exists and is open;
  - candidate exists and is active;
  - not already linked;
  - count is below 500.

  It then inserts at `new` and saves with `position.candidate_added`. (Spec: link identity; write preconditions. Design D4.)

- [x] 2.3 Add `ChangePositionCandidateStage.cs`, with a validator for the stage vocabulary and a version above 0. It guards, then checks the position is open and the link exists, then applies `ExpectVersion` and `ChangeStage`, and saves with `position.candidate_stage_changed`. (Spec: stage vocabulary; stale stage change.)
- [x] 2.4 Add `RemovePositionCandidate.cs`. It guards, then checks the position is open and the link exists, then physically removes the link and saves with `position.candidate_removed`. (Spec: link removal.)

## 3. Web endpoints

- [x] 3.1 In `Program.cs`, register the `PositionCandidatesRead` (`positions.read` + `candidates.read`) and `PositionCandidatesManage` (`positions.manage` + `candidates.read`) policies next to the existing permission policies. (Design D2.)
- [x] 3.2 Map the four position-scoped routes in a new `PositionCandidateEndpoints.cs` (so `PositionEndpoints.cs` keeps no delete route), and the candidate-scoped read with `MapGet("/api/candidates/{candidateId:guid}/positions")`. Each needs a nested request record, `.WithName()` and explicit `.Produces*`/`.ProducesProblem` metadata including 401, 403, 404 and 409. (Spec: position candidate API.)

## 4. Backend tests

- [x] 4.1 Unit tests (`backend/Tests/UnitTests`), using hand-written doubles:
  - the vocabulary and `ChangeStage`;
  - every handler refusing when unauthenticated, and for each missing permission, before validation;
  - the preconditions: closed, inactive, duplicate, cap;
  - the audit subject format, with no stage value;
  - the `AuditEventTypes` catalogue including the three new types.
- [x] 4.2 Integration tests (Testcontainers), covering endpoints and behavior:
  - all five endpoints with ordering and projections, asserting no contact fields;
  - a concurrent duplicate add leaving one row and giving one 409;
  - a stale stage change;
  - physical delete plus the audit row;
  - the 500 cap;
  - 404s for an unknown position, candidate or link.
- [x] 4.3 Integration tests covering the database:
  - the schema constraints and FK `RESTRICT`;
  - the grant matrix as `ktl_runtime`: `DELETE` on links allowed; `TRUNCATE` on links refused; `DELETE` on `"OPS_Positions"` and `"CND_Candidates"` refused;
  - `EXPLAIN` of both list queries showing index use and no `Requirements` read.
- [x] 4.4 Review and update existing backend tests affected by the change: the position endpoint and audit catalogue tests, and `ProjectDependencyTests`. Run `npm run test:backend` and inspect the output.

## 5. Frontend: service and shared pieces

- [x] 5.1 Extend `position.models.ts`: `PositionCandidateStage`, `POSITION_CANDIDATE_STAGES`, `PositionCandidate` and `CandidatePosition`. Extend `position.service.ts` with `listCandidates`, `addCandidate`, `changeStage`, `removeCandidate` and `listForCandidate` through `ApiTransport`. Add `position-candidates.logic.ts` for ordering and the linked-id set. (Design D8.)
- [x] 5.2 Add `position-stage-select.tsx`: a labelled native `<select>` with `name` and `data-testid`, whose options come from `positions.stage.*` keys. (Spec: stage vocabulary; accessibility.)
- [x] 5.3 `search-results.tsx`:
  - move every literal to `search.results.*` keys with identical Spanish text;
  - add the optional `renderRowAction` prop;
  - remove the file from `LEGACY_HARDCODED_COPY` in `eslint.config.js`.

  (Spec: advanced search results are unchanged. Design D8.)

## 6. Frontend: position page

- [x] 6.1 Add `position-candidates-panel.tsx`:
  - the list with name link, stage, date added, «Ver» and the **Eliminado** badge;
  - the stage selector and «Quitar de la posición», with confirmation, for managers on open positions;
  - the closed-position hint;
  - loading, empty and error states;
  - a 409 reload.

  (Spec: position page lists its candidates.)

- [x] 6.2 Add `position-candidate-picker.tsx`: a modal with a labelled text search through `CandidateSearchService` (debounced, aborting stale requests), linked candidates marked `aria-disabled`, focus trap and focus return, and Escape to close. (Spec: candidate is added through the picker.)
- [x] 6.3 Wire `position-detail-page.tsx`:
  - place the panel above «Candidatos que encajan», only with `candidates.read`;
  - load the link list once;
  - pass `renderRowAction` («Añadir a la posición» or disabled «Añadido») to `SearchResults` when `canManage` and the position is open;
  - update the list from server responses.

  (Spec: live candidate matching, modified. Design D5.)

## 7. Frontend: candidate page

- [x] 7.1 Add `candidate-positions-panel.tsx`:
  - open positions first, then muted closed rows;
  - title link, status badge, stage (selector or badge), date added, «Ver» and «Quitar de la posición».
  - «Añadir a posición» is hidden for removed candidates.

  (Spec: positions panel on the candidate page.)

- [x] 7.2 Add `position-picker.tsx`: a modal over `positionService.search({ status: 'open', text })` (a side-effect-free `list`), with already-linked positions not selectable and the same accessibility behavior as 6.2.
- [x] 7.3 Render the panel in `candidate-detail-page.tsx` between Experiencia and Notas, only when `usePermission('positions.read')` is true, and outside the KTL-29 edit-mode coordinator. (Spec: panel does not block editing; actor lacks position permission.)
- [x] 7.4 Add every new `es.json` key:
  - `positions.candidates.*`, including the error keys for each 409 code;
  - `positions.stage.*`;
  - `positions.matches.addToPosition` and `.added`;
  - `candidate.profile.positions.*`;
  - `admin.audit.eventType.position.candidate_*`.

  No hardcoded copy.

## 8. Frontend tests

- [x] 8.1 Unit specs (`frontend/tests/unit`):
  - the service request contracts;
  - the logic helpers;
  - the stage select;
  - both panels: states, ordering, permission- and closed-gated controls, the removal confirmation and its cancel, the 409 reload;
  - both pickers.
- [x] 8.2 Unit specs for integration points:
  - `SearchResults` with and without `renderRowAction`, and the search page still rendering the same literals;
  - the position detail page not requesting links without `candidates.read`;
  - the candidate page with and without `positions.read`, and restaging while another panel is in edit mode.
- [x] 8.3 Review and update existing unit specs affected by the change: the position detail page, the candidate detail page, the search results and the audit event labels. Run `npm test` and inspect the output.

## 9. Security evidence

- [x] 9.1 Extend `frontend/tests/security`:
  - the five routes refuse unauthenticated callers, with valid and malformed input;
  - they refuse `positions.read`/`positions.manage` without `candidates.read`;
  - writes refuse a reader without `positions.manage`;
  - existing and missing ids get identical refusals;
  - link responses carry no contact data.
- [x] 9.2 Run `npm run security:rls` and `npm run security:storage`. Confirm that `DELETE` on `"OPS_Positions"` and `"CND_Candidates"` is still refused for `ktl_runtime`. Inspect the output.

## 10. End to end

- [x] 10.1 Add the `"OPS_PositionCandidates"` delete, for marked positions or e2e candidates, to `scripts/e2e-cleanup.sql` before the existing position and candidate deletes.
- [x] 10.2 Write `frontend/tests/e2e/position-candidates.spec.ts`. Use a `Date.now()`-marked position and candidates, and select by role, label or `data-testid` only. The journey:
  - add a match;
  - add a non-matching candidate from the position picker and from the candidate page;
  - restage on both pages;
  - reject;
  - remove;
  - close the position and verify read-only mode.
- [x] 10.3 Run `docker compose up --build` so the stack serves the new API and migration, then run `npx playwright test tests/e2e/position-candidates.spec.ts` and inspect the result.
- [x] 10.4 Run the existing `tests/e2e/positions.spec.ts` and the search e2e specs to confirm matches and the search page are unchanged. Check that the global teardown left no marked links, positions or candidates in the development database.

## 11. Documentation

- [x] 11.1 Write `docs/ktl-30/position-candidates.md`:
  - routes, payloads and error codes;
  - stages and their rules;
  - the permission matrix;
  - the audit subject format and its filtering trade-off.

  Also write `docs/ktl-30/release-notes.md`.

- [x] 11.2 Update `docs/ktl-5/database-conventions.md` with the `"OPS_PositionCandidates"` table and its scoped `DELETE` grant. Update `README.md` (in Spanish) if its feature overview lists position capabilities.

## 12. Quality gates

- [x] 12.1 From `frontend/`, run `npm run lint` and `npm run format:check`, and fix all findings.
- [x] 12.2 Run `npm run build:all` (warnings are errors) and confirm the build succeeds.

## 13. Post-review UX changes

Requested after the first implementation. Groups 6 and 7 describe the original UI, and these tasks
supersede it where they differ.

- [x] 13.1 Rename the match action to «Añadir» and hide «Abrir CV» on position-page matches (`showOpenCv={false}`). (Spec: live candidate matching.)
- [x] 13.2 Add `candidateCount` to the position list projection and a «Candidatos» column to the Posiciones table. Cover it in unit and integration tests. (Spec: bounded position listing.)
- [x] 13.3 Vertically centre the link tables and size the stage select to match `.button.small`, scoped to `.position-links-table` and `.position-stage-select`.
- [x] 13.4 Give the position's candidate list the search columns (e-mail, phone, CV), with «Estado en la posición» and «Añadido». Extend `PositionCandidateResponse`, and update the projection, security and schema tests. (Spec: position candidate API.)
- [x] 13.5 Add `useRowLink` and use it for row navigation in `SearchResults` and both position tables. Remove «Ver»/«Detalle», keep the name or title as the keyboard link, and show phones as plain text. Cover it in unit specs and run the affected e2e specs. (Spec: position page lists its candidates; positions panel; live candidate matching.)
