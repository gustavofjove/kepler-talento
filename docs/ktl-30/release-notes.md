# KTL-30: Candidates added to positions

HR can now record which candidates are being considered for each position and how far each one
has got.

**Add candidates to a position.** Each position page has a new «Candidatos de la posición» panel
above «Candidatos que encajan». There are three ways to add a candidate:

- the «Añadir» button on a matching candidate;
- «Añadir candidato» on the position page, which searches any active candidate by name or
  e-mail, even one who does not meet the requirements (for example a direct applicant);
- «Añadir a posición» on the candidate page, which lists open positions.

**Track the stage.** Every added candidate has a stage: Nuevo, Preseleccionado, Entrevista,
Contratado or Descartado. It changes from a selector on either page. It is independent of the
candidate's own status, which does not change.

**Remove a mistake.** «Quitar de la posición» deletes the link after a confirmation. It exists
to undo a mistaken addition. To turn a candidate down, set the stage to Descartado, which keeps
the record.

**Positions on the candidate page.** A new «Posiciones» panel lists the candidate's positions,
open ones first and closed (past) ones muted.

**Candidate count.** The Posiciones table has a new «Candidatos» column with the number of
candidates added to each position.

**Closed positions.** A closed position keeps its candidates but becomes read-only for them until
it is reopened.

**Permissions.** No new permission is added.

- Seeing a position's candidates needs `positions.read` and `candidates.read`.
- Adding, restaging and removing needs `positions.manage` and `candidates.read`.

**Deployment.** Run the `AddPositionCandidates` migration before deploying the API. It creates
`"OPS_PositionCandidates"` and grants the runtime role `DELETE` on that table only. Rolling the
migration back drops the table and every link.

Details: [position-candidates.md](position-candidates.md).
