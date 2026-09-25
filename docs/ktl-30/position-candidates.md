# KTL-30 position candidates

A position candidate is the persistent link between one candidate and one position. It records
that HR is considering the candidate for the position and which **stage** they have reached. It
is separate from the live **matches** («Candidatos que encajan»), which are still evaluated from
the position's requirements on every visit and are never stored.

## Stages

| `stage`       | UI              |
| ------------- | --------------- |
| `new`         | Nuevo           |
| `shortlisted` | Preseleccionado |
| `interview`   | Entrevista      |
| `hired`       | Contratado      |
| `rejected`    | Descartado      |

- The vocabulary is closed. It is enforced by `PositionCandidateStages` and by the check
  constraint `CK_OPS_PositionCandidates_Stage`, and lists are ordered in the table's order.
- A new link starts at `new`, and any stage can move to any other.
- A stage is independent of the candidate's own status (`CandidateStatuses`). Neither updates
  the other.

## Rules

- **Requirements are not enforced.** Any active candidate can be added, including one who
  applied directly and matches nothing.
- **One link per pair.** A second add of the same candidate is refused
  (`position.candidate.already_linked`). This holds under concurrency, because the unique index
  `UX_OPS_PositionCandidates_PositionId_CandidateId` is the authority.
- **Closed positions are read-only.** Adding, restaging and removing are refused
  (`position.candidate.position_closed`) until the position is reopened. Their links stay
  listed, and on the candidate page they are the muted "past" positions.
- **Removed candidates.** A logically removed candidate cannot be added
  (`position.candidate.candidate_inactive`). Existing links stay, are marked «Eliminado», and
  can still be restaged or removed.
- **Limit.** A position holds at most 500 links (`position.candidate.limit_reached`), which keeps
  its list response bounded. The check is not serializable, so two concurrent adds at 499 can
  both succeed.
- **Removal is a physical delete.** It corrects a mistaken addition, and the candidate can be
  added again later. Rejecting a candidate is the `rejected` stage, which keeps the link.

## API

| Method and route                                         | Body                 | Success | Permissions                            |
| -------------------------------------------------------- | -------------------- | ------- | -------------------------------------- |
| `GET /api/positions/{id}/candidates`                     | —                    | `200`   | `positions.read` + `candidates.read`   |
| `POST /api/positions/{id}/candidates`                    | `{ candidateId }`    | `201`   | `positions.manage` + `candidates.read` |
| `PUT /api/positions/{id}/candidates/{candidateId}/stage` | `{ stage, version }` | `200`   | `positions.manage` + `candidates.read` |
| `DELETE /api/positions/{id}/candidates/{candidateId}`    | —                    | `204`   | `positions.manage` + `candidates.read` |
| `GET /api/candidates/{candidateId}/positions`            | —                    | `200`   | `positions.read` + `candidates.read`   |

**Authorization.** The two combined policies (`positions.candidates.read` and
`positions.candidates.manage`) run before binding. An unauthenticated or unauthorized caller gets
the same refusal for valid, malformed, existing and missing input. Every handler repeats the
guard, and no new permission exists.

**Responses.** Both lists are complete and unpaged.

- A position's list is ordered by stage, then newest first, then candidate id. Each item holds
  `candidateId`, `firstName`, `lastName`, `candidateIsActive`, `stage`, `addedAtUtc`,
  `updatedAtUtc` and `version`.
- A candidate's list puts open positions first, then newest first. Each item holds
  `positionId`, `title`, `positionStatus`, `stage`, `addedAtUtc`, `updatedAtUtc` and `version`.
- Neither list carries contact data, documents, notes, description or requirements.

**Stable problem codes.**

| Code                                      | HTTP |
| ----------------------------------------- | ---- |
| `position.candidate.already_linked`       | 409  |
| `position.candidate.position_closed`      | 409  |
| `position.candidate.candidate_inactive`   | 409  |
| `position.candidate.limit_reached`        | 409  |
| `position.candidate.concurrency_conflict` | 409  |
| `position.candidate.stage_invalid`        | 400  |
| `position.candidate.candidate_required`   | 400  |
| `position.candidate.not_found`            | 404  |
| `position.not_found`                      | 404  |
| `candidate.not_found`                     | 404  |

## Position list

Each item of `GET /api/positions` now carries `candidateCount`, the number of linked candidates.
It names no one, so it needs only `positions.read`. The Posiciones table shows it in a
«Candidatos» column.

## Audit

Adding, restaging and removing a link each record one event: `position.candidate_added`,
`position.candidate_stage_changed` or `position.candidate_removed`. The event is written in the
same transaction as the change.

The subject is `{positionId:N}:{candidateId:N}`, because the link itself may later be deleted.
The stage is never recorded.

The Auditoría subject filter matches exactly, so searching for a bare candidate or position id
does not find these events. Filter by event type and date instead.

## Persistence

`"OPS_PositionCandidates"` (migration `AddPositionCandidates`) has these columns: `Id`,
`PositionId`, `CandidateId`, `Stage`, `AddedAtUtc`, `UpdatedAtUtc` and `xmin`.

- **References.** Both references are `ON DELETE RESTRICT`.
- **Indexes.** The unique pair index serves the position list, and
  `IX_OPS_PositionCandidates_CandidateId` serves the candidate list.
- **Runtime grants.** `ktl_runtime` holds `SELECT`, `INSERT`, `UPDATE` and `DELETE` on this table
  only, and `TRUNCATE` is revoked. `"OPS_Positions"` and `"CND_Candidates"` keep `DELETE`
  revoked.

## User interface

- **Position page.** «Candidatos de la posición» appears above «Candidatos que encajan», with the
  same columns (name and e-mail, phone, CV) except «Estado en la posición» and «Añadido».
  - In both tables, and on the advanced search page, clicking a row opens the candidate. Links and
    controls in the row keep their own behaviour, Ctrl or middle click opens a new tab, and the
    name is the row's keyboard link. Phones are plain text.
  - Managers get a stage selector per row, «Quitar de la posición» with a confirmation, and
    «Añadir candidato». The last opens a picker over the existing candidate search.
  - Each match offers «Añadir», or a disabled «Añadido» when the candidate is
    already linked.
  - Without `candidates.read` the panel is not shown and nothing is requested.
- **Candidate page.** The «Posiciones» panel sits between Experiencia and Notas, only with
  `positions.read`.
  - Rows show the title, position status, stage, date added and, for managers on open
    positions, the stage selector and removal. Clicking a row opens the position, as in the
    candidate tables.
  - «Añadir a posición» opens a picker of open positions. It is hidden for removed candidates.
  - Actions apply immediately, so the panel takes no part in per-panel edit mode.
