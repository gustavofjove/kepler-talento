## [original]

Link Positions and candidates

The candidates can be now added/related/linked/associated (choose the correct term) to positions. Currently the only relationship is through "candidatos que encajan/candidates that meet the criteria" but that's not an actual (persistent) link. Therefore, each position will have a new list of candidates that will be related to the position through a status (TBD but start with something like "Nuevo", "En proceso", "Descartado", "Referido", etc.).

The list of candidates added to a position will appear above the list of "candidates that meet the criteria", and the latter will have a button "Add to position" that will link it to the former and will disable the "Add to position" button.

The candidate details page will have a new read-only panel with a table/list of present and past positions and their status. Also as actions "remove from position" and "View". Ideally clicking their status could display a dropdown menu that allows users to change it.

Question: what's the best approach to add a candidate to a position where they don't meet the position criteria? For example a candidate that applies directly may need to be added.

--

## [enhanced]

> **Superseded in detail** by `openspec/changes/ktl-30-position-candidates/`. During implementation, the match button became «Añadir», the position's candidate list gained the search contact columns, rows became clickable instead of offering «Ver»/«Detalle», and the Posiciones list gained a candidate count. Where they differ, the change artifacts are authoritative.

# KTL-30 — Add candidates to positions and track their stage

## User story

As an **HR user**, I want to **add candidates to a position** and record the stage each one has reached, so that a position keeps a durable list of the people actually being considered. Today the position only shows a live search of who happens to meet its requirements.

## Terminology

| Term                                 | Meaning                                                                                                                                                                                    |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Position candidate** (link)        | The persistent relationship between one candidate and one position. UI verb: **Añadir a la posición** / **Quitar de la posición**.                                                         |
| **Stage** (`stage`)                  | The status of a candidate within one position. It is named `stage` in the API and code so it does not collide with the position status (`open`/`closed`) or the candidate's global status. |
| **Matches** (Candidatos que encajan) | The existing live search of the position's requirements (KTL-15). It stays live and unpersisted. Adding a match creates a link; it never snapshots the search.                             |

## Confirmed product decisions

- **Stage vocabulary**: a closed set in code, with a database check constraint and in this order:

  | `stage`       | UI (es)         |
  | ------------- | --------------- |
  | `new`         | Nuevo           |
  | `shortlisted` | Preseleccionado |
  | `interview`   | Entrevista      |
  | `hired`       | Contratado      |
  | `rejected`    | Descartado      |

  A new link starts as `new`. Any stage can change to any other. None is terminal.

- **The stage is independent of the candidate's global status** (`CandidateStatuses`: new/available/in_process/hired/rejected). Changing a stage never changes the candidate record, and changing the candidate never changes a stage.
- **Requirements are not enforced when linking.** A candidate who does not meet a position's requirements can still be added, for example someone who applied directly. There are two entry points:
  1. the position page: **Añadir candidato** opens a picker that searches candidates by name or e-mail;
  2. the candidate page: **Añadir a posición** opens a picker of open positions.
- **Removing a link is a physical delete.** It exists to correct a mistaken addition. Rejecting a candidate is a different thing: that is the `rejected` stage, and the link stays. The removal is audited, so the audit trail keeps a record of it. This follows the existing precedents for association rows (`CND_CandidateTags`, `ADM_SearchPresets`). As of this ticket, AGENTS.md no longer carries a blanket "no physical deletes" rule. Candidates and catalog items are still retired logically (`openspec/config.yaml` domain rules).
- **"Past positions"** on the candidate page are links whose position is now `closed`. Removed links no longer exist, so they are not shown.

## Functional requirements

### 1. Position page: position candidates

- Add a **Candidatos de la posición** panel in `position-detail-page.tsx`, **above** the existing **Candidatos que encajan** panel.
- Columns: candidate name (links to `/app/candidates/:id`), stage, date added, actions **Ver** and **Quitar de la posición**. A logically removed candidate stays listed with an **Eliminado** badge.
- Order the rows by stage (vocabulary order), then by date added descending, then by candidate id.
- The stage is a native `<select>`, labelled per row (e.g. `aria-label` "Estado de {nombre}"). A change saves immediately with the link's `version`. On a concurrency conflict (409), show a toast and reload the list.
- **Quitar de la posición** asks for confirmation through `confirmDialogService` (`danger: true`) and then deletes the link.
- **Añadir candidato** opens a modal picker. It queries the existing `POST /api/candidates/search` with the `text` filter, shows one page of the minimal projection, and marks candidates who are already linked as added (disabled).
- Show a localized empty state (**Todavía no se ha añadido ningún candidato.**) and loading and error states.

### 2. Position page: matches

- Each row in **Candidatos que encajan** gets an **Añadir a la posición** button. After a successful add, the button becomes a disabled **Añadido** and the row appears in the panel above without a full page reload.
- Candidates who are already linked show the disabled **Añadido** state from the first render. The state comes from the complete link list (see _Bounded lists_ in the API contract), never from a partial page.
- `SearchResults` is shared with the search page, so the action is an optional prop (for example `renderRowAction?: (result) => ReactNode`). The search page keeps rendering exactly what it renders today.
- `search-results.tsx` is in `LEGACY_HARDCODED_COPY`. Touching it means moving all of its copy to `es.json` keys and removing it from the list in the same change.

### 3. Closed positions and removed candidates

- A `closed` position is read-only for links. Its lists still render, but add, stage change and remove return `409 position_closed`, and the UI hides those controls. Reopening the position makes it editable again.
- A logically removed candidate cannot be **added** (`409 candidate_inactive`). Existing links to that candidate stay, and can still change stage or be removed.

### 4. Candidate page: positions panel

- Add a **Posiciones** panel to `candidate-detail-page.tsx` after **Experiencia** and before **Notas**. It is not part of the one-panel-in-edit-mode mechanism from KTL-29: it has no **Editar** mode, and every action applies immediately.
- Columns: position title (links to `/app/positions/:id`), position status badge (**Abierta**/**Cerrada**), stage (a `<select>` when the link is editable, a badge otherwise), date added, actions **Ver** and **Quitar de la posición**.
- Links to open positions come first, then links to closed positions (shown muted), each group ordered by date added descending.
- **Añadir a posición** opens a picker of **open** positions from the existing `GET /api/positions?status=open&text=…`, with positions the candidate is already on shown as disabled. The button is hidden when the candidate is logically removed.
- The panel renders only when the actor holds `positions.read`. The page already requires `candidates.read`.

### 5. Authorization

No new permission. The link reveals which people a position is considering, so every link operation also requires `candidates.read`.

| Operation                    | Required permissions                         |
| ---------------------------- | -------------------------------------------- |
| Read a position's candidates | `positions.read` **and** `candidates.read`   |
| Read a candidate's positions | `positions.read` **and** `candidates.read`   |
| Add, change stage, remove    | `positions.manage` **and** `candidates.read` |

- Every endpoint checks authentication and both permissions **before** validating input or looking up the position, the candidate or the link. Unauthenticated and unauthorized callers get the same refusal for valid, invalid, existing and missing ids. The handlers repeat the guards (for example `PositionGuards.RequireManageCandidates(actor)`).
- In the SPA, use `usePermission()` at the top level of each component. An actor with `positions.read` but without `candidates.read` keeps today's behavior: the position shows the localized explanation, and no candidate request is sent.

## API contract

Check the existing routes first. None of these exist today. Implement them in the Positions slice (`backend/Web/Features/Positions/PositionEndpoints.cs`) and map the candidate-scoped read from the same file:

| Method and route                                         | Body                 | Success    | Stable problems                                                                                                                |
| -------------------------------------------------------- | -------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------ |
| `GET /api/positions/{id}/candidates`                     | —                    | `200` list | 404 position                                                                                                                   |
| `POST /api/positions/{id}/candidates`                    | `{ candidateId }`    | `201` link | 400 invalid; 404 position/candidate; 409 `already_linked`, `position_closed`, `candidate_inactive`, `position_candidate_limit` |
| `PUT /api/positions/{id}/candidates/{candidateId}/stage` | `{ stage, version }` | `200` link | 400 unknown stage; 404; 409 concurrency, `position_closed`                                                                     |
| `DELETE /api/positions/{id}/candidates/{candidateId}`    | —                    | `204`      | 404 link; 409 `position_closed`                                                                                                |
| `GET /api/candidates/{candidateId}/positions`            | —                    | `200` list | 404 candidate                                                                                                                  |

**Bounded lists.** A position can hold at most **500** links, enforced on add (`409 position_candidate_limit`). Because of that cap, `GET /api/positions/{id}/candidates` returns the complete list unpaged, which the disabled **Añadido** state needs. The candidate-scoped list is bounded by the number of positions a candidate is on. The design may lower the cap, but may not drop it.

**Response shapes.** Minimal projections, with no contact data:

```json
// GET /api/positions/{id}/candidates → items
{
  "candidateId": "uuid",
  "firstName": "Ana",
  "lastName": "García",
  "candidateIsActive": true,
  "stage": "shortlisted",
  "addedAtUtc": "2026-09-25T10:00:00Z",
  "updatedAtUtc": "2026-09-25T11:00:00Z",
  "version": 3
}

// GET /api/candidates/{candidateId}/positions → items
{
  "positionId": "uuid",
  "title": "Desarrollador/a .NET",
  "positionStatus": "open",
  "stage": "interview",
  "addedAtUtc": "2026-09-25T10:00:00Z",
  "updatedAtUtc": "2026-09-25T11:00:00Z",
  "version": 3
}
```

Errors go through the existing `GlobalExceptionHandler` and `Application/Common/Errors` types. New ids use `Guid.CreateVersion7()`. Timestamps are UTC `DateTimeOffset`.

## Persistence and backend design

- **Domain**: `backend/Domain/Positions/PositionCandidate.cs`, plus `PositionCandidateStages` (a closed vocabulary with `All` and `IsKnown`, like `PositionStatuses`). Add `CandidateAdded`, `CandidateStageChanged` and `CandidateRemoved` to `PositionAuditEvents` (`position.candidate_added`, `position.candidate_stage_changed`, `position.candidate_removed`).
- **Application** (`backend/Application/Features/Positions/`), one file per use case: `ListPositionCandidates.cs`, `ListCandidatePositions.cs`, `AddPositionCandidate.cs`, `ChangePositionCandidateStage.cs`, `RemovePositionCandidate.cs`, each a `sealed record` request with a validator and a `sealed` handler. Extend `IPositionRepository`, or add `IPositionCandidateRepository`, under `Application/Abstractions/Persistence/`.
- **Table** `"OPS_PositionCandidates"` (the `OPS_` prefix already owns recruitment workflow state since KTL-15):
  - `Id` uuid PK, `PositionId` FK → `"OPS_Positions"` (`ON DELETE RESTRICT`), `CandidateId` FK → `"CND_Candidates"` (`ON DELETE RESTRICT`), `Stage` text, `AddedAtUtc`, `UpdatedAtUtc`, `xmin` concurrency token;
  - check constraint on `Stage` ∈ vocabulary; `UpdatedAtUtc >= AddedAtUtc`;
  - unique index `(PositionId, CandidateId)`, where a race on the unique key maps to `409 already_linked`; index `(CandidateId)` for the candidate panel;
  - EF Core configuration in `Infrastructure/Persistence/Configurations/PositionCandidateConfiguration.cs`, and a migration `AddPositionCandidates` generated with `dotnet ef migrations add`.
- **Grants**: the migration grants `SELECT, INSERT, UPDATE, DELETE` on `"OPS_PositionCandidates"` to `ktl_runtime` (and `REVOKE TRUNCATE`). `DELETE` is limited to this table. `OPS_Positions` and `CND_Candidates` keep their revoked `DELETE`.
- **Audit**: every add, stage change and removal writes an `AUD_Events` row with the actor, the position id, the candidate id, the event type and the correlation id. It carries **no stage value or name**, following the KTL-19 convention that the type is the whole description.
- **Logging**: no candidate name or stage in logs. The `PersonalDataRedactionEnricher` stays in place.
- **Concurrency**: the stage change requires `version`, and a mismatch returns 409. Add and remove are protected by the unique key and by the existence check.
- **E2E cleanup**: `scripts/e2e-cleanup.sql` deletes from `"OPS_PositionCandidates"` rows whose position or candidate is marked, **before** it deletes `"OPS_Positions"` and the candidates.

## Frontend impact

- `src/app/features/positions/position.models.ts`: `PositionCandidate`, `CandidatePosition` and a `PositionCandidateStage` union matching the backend vocabulary.
- `src/app/features/positions/position.service.ts`: `listCandidates`, `addCandidate`, `changeStage`, `removeCandidate` and `listForCandidate`, all through `ApiTransport`. No `localStorage`.
- New components under `src/app/features/positions/components/`: `position-candidates-panel.tsx`, `position-candidate-picker.tsx`, `position-stage-select.tsx` (shared by both pages). Pure helpers such as stage order and sorting go in a sibling `*.logic.ts`.
- `src/app/features/candidates/components/candidate-positions-panel.tsx` and a `position-picker` modal, both built on the existing `shared/components/modal.css` patterns.
- `search-results.tsx`: the optional row-action prop, all copy moved to keys, and the file removed from `LEGACY_HARDCODED_COPY` in `eslint.config.js`.
- Keep the `name=` and `data-testid` attributes (`position-candidates`, `position-candidate-row`, `position-candidate-stage`, `add-to-position`, `remove-from-position`, `candidate-positions`).
- Report errors with `useErrorToast()`. New validation uses `TranslatableError`.

### New i18n keys (`es.json`, flat)

| Key                                  | Value                                                             |
| ------------------------------------ | ----------------------------------------------------------------- |
| `positions.candidates.title`         | Candidatos de la posición                                         |
| `positions.candidates.empty`         | Todavía no se ha añadido ningún candidato.                        |
| `positions.candidates.add`           | Añadir candidato                                                  |
| `positions.candidates.remove`        | Quitar de la posición                                             |
| `positions.candidates.removeConfirm` | ¿Quitar a {{name}} de esta posición?                              |
| `positions.candidates.view`          | Ver                                                               |
| `positions.candidates.addedAt`       | Añadido                                                           |
| `positions.candidates.stageLabel`    | Estado de {{name}}                                                |
| `positions.candidates.inactive`      | Eliminado                                                         |
| `positions.candidates.closedHint`    | La posición está cerrada; reábrela para modificar sus candidatos. |
| `positions.matches.addToPosition`    | Añadir a la posición                                              |
| `positions.matches.added`            | Añadido                                                           |
| `positions.stage.new` … `.rejected`  | Nuevo · Preseleccionado · Entrevista · Contratado · Descartado    |
| `candidate.profile.positions.title`  | Posiciones                                                        |
| `candidate.profile.positions.add`    | Añadir a posición                                                 |
| `candidate.profile.positions.empty`  | Este candidato no está en ninguna posición.                       |
| `candidate.profile.positions.stage`  | Estado en la posición                                             |

Plus the keys for the copy extracted from `search-results.tsx`. The final key names are the implementer's choice, as long as they follow `feature.section.element`.

## Acceptance criteria

1. **Add from matches**: given a manager with `candidates.read` viewing an open position, when they click **Añadir a la posición** on a match, then a `new` link is persisted, the candidate appears in **Candidatos de la posición**, and the match row shows a disabled **Añadido**.
2. **Add without meeting requirements**: given a candidate who does not match the requirements, when a manager adds them through **Añadir candidato** on the position page, or through **Añadir a posición** on the candidate page, then the link is created and requirements are not checked.
3. **No duplicates**: given an existing link, when the same candidate is added again, including by two concurrent requests, then exactly one link exists and the other request receives `409 already_linked`.
4. **Stage change**: given a link at `new`, when a manager selects **Entrevista** on either page, then the stage is stored with a new version, both pages show it, and the candidate's global status is unchanged.
5. **Stale stage change**: given two managers holding the same link version, when both change the stage, then the second receives the concurrency problem and the first value stays.
6. **Remove**: given a mistaken link, when a manager confirms **Quitar de la posición** on either page, then the row is physically deleted, it disappears from both lists, the candidate can be added again, and a `position.candidate_removed` audit event exists without a stage value.
7. **Reject is not remove**: given a link, when the stage is set to **Descartado**, then the link remains listed on both pages.
8. **Closed position**: given a closed position, when any add, stage change or remove is attempted through the UI or API, then it is refused with `position_closed`, the lists stay visible read-only, and the candidate page lists the link as a past (muted) position.
9. **Removed candidate**: given a logically removed candidate, when someone tries to add them, then `candidate_inactive` is returned. Their existing links stay visible with **Eliminado** and can still change stage or be removed.
10. **Candidate panel**: given a candidate linked to one open and one closed position, when an actor with `positions.read` opens the candidate page, then both rows show the title, position status, stage, date added, **Ver** and (for managers on open positions) **Quitar de la posición**, with the open position first.
11. **Fail closed**: given an unauthenticated caller, or one lacking `positions.read`/`positions.manage` or `candidates.read`, when they call any link endpoint with valid, invalid, existing or missing ids, then they receive the same refusal before validation or lookup, and nothing changes.
12. **Privacy boundary**: given an actor with `positions.read` but without `candidates.read`, when they open a position, then no link or candidate request is sent and no candidate name or count is shown. Given an actor without `positions.read`, the candidate page shows no **Posiciones** panel and sends no request.
13. **Least privilege**: given the migrated database, `ktl_runtime` can `SELECT/INSERT/UPDATE/DELETE` on `"OPS_PositionCandidates"`, cannot `TRUNCATE` it, and still cannot `DELETE` from `"OPS_Positions"` or `"CND_Candidates"`.
14. **Limit**: given a position with 500 links, when one more is added, then `409 position_candidate_limit` is returned.
15. **Search page unchanged**: given the advanced search page, when results render, then no add-to-position action appears, and the copy it renders today is unchanged (now served from `es.json`).
16. **Accessibility and responsiveness**: every stage `<select>` and picker has a programmatic label, the dialogs trap and restore focus, everything is operable by keyboard, and at 390 px wide the page does not scroll horizontally (tables scroll inside their own container).

## Test coverage and evidence

- **Backend unit** (`backend/Tests/UnitTests`): the stage vocabulary and domain invariants; every handler's guard failing for missing auth and for each missing permission, before validation; the closed-position and inactive-candidate rules; audit payloads without stage or personal values.
- **Backend integration** (`backend/Tests/IntegrationTests`, Testcontainers): all five endpoints; a concurrent duplicate add on the unique key; the stale version; the physical delete plus its audit row; the 500-link cap; ordering; check constraint and FK behavior; runtime grants (DELETE on the link table only).
- **Frontend unit** (`frontend/tests/unit`): service request contracts; both panels (states, sorting, permission-gated controls, closed/inactive handling); the stage select with conflict reload; both pickers; `SearchResults` with and without the row action; the candidate page rendering the panel only with `positions.read`.
- **Security** (`frontend/tests/security`): the unauthenticated/unauthorized matrix for the new routes; `positions.read` without `candidates.read`; `positions.manage` without `candidates.read`; no contact data in link responses.
- **E2E** (`frontend/tests/e2e/position-candidates.spec.ts`): create a marked position and candidate, add from matches, add a non-matching candidate from both pages, change the stage on each page, reject, remove, close the position and verify read-only mode. Select elements by role, label or `data-testid`, never by Spanish text. The cleanup SQL removes the links.
- Gates, run and inspected: `npm test`, `npm run test:backend`, the targeted `npm run e2e`, `npm run security:rls`, `npm run security:storage`, `npm run lint`, `npm run format:check`, `npm run build:all`.

## Documentation and specification impact

- `openspec/specs/position-management/spec.md`: the requirement "The system SHALL NOT persist candidate-position membership" is **replaced**. The live matches stay unpersisted, but links are now a persisted, separate concept. Add requirements for links, stages, the permission matrix and closed-position behavior.
- `openspec/specs/candidate-profile-pages/spec.md`: the new **Posiciones** panel.
- `openspec/specs/audit-trail/spec.md`: the three new event types.
- `openspec/specs/postgresql-persistence/spec.md` and `docs/ktl-5/database-conventions.md`: `"OPS_PositionCandidates"`, its grants, and a record of the scoped `DELETE`.
- `docs/ktl-30/position-candidates.md` (API, stages, rules, permission matrix) and `docs/ktl-30/release-notes.md`.
- `README.md` (Spanish): mention that candidates can be added to positions, if the feature overview lists position capabilities.

## Non-functional requirements

- **Personal data**: link responses carry only name, active flag, stage and dates. No e-mail, phone, CV or notes. No candidate data or stage in URLs (ids only), logs or audit payloads.
- **Performance**: the position list query uses `(PositionId, CandidateId)`, the candidate panel uses `(CandidateId)`, and neither query reads position requirements JSON or description HTML. The matches **Añadido** check is a set lookup on the client over the (capped) link list.
- **Consistency**: after any successful write, both the panel and the matches state update from the server response, not from optimistic guesses.

## Out of scope

- Admin-configurable stages, stage history or timelines, per-stage notes, interview scheduling, notifications.
- Syncing a stage with the candidate's global status.
- Bulk add, drag-and-drop pipelines (kanban) and exporting a position's candidates.
- Snapshotting or persisting the matches search.
- Linking candidates to closed positions without reopening them.
