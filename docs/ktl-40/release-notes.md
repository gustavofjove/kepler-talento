# KTL-40 release notes: «Inicio», the home page

The home page has been rebuilt and renamed from «Dashboard» to «Inicio», in the navigation and on
the page. The route stays `/app`. Every figure and row on it now leads to the screen where the user
acts on it:

- **Header actions**, each shown only with its permission: «Alta de candidato»
  (`candidates.create`), «Nueva posición» (`positions.manage`), «Importar candidatos»
  (`candidates.import`). «Alta de candidato» used to be shown to everyone.
- **Candidatos**: the active total, an availability bar, and «disponibles · no disponibles · sin
  comprobar», each linking to the candidate list filtered by it. `candidates.delete` holders also
  see «inactivos», as text.
- **Posiciones abiertas** and **Sin CV principal** tiles, linking to the position list and to the
  candidates without a primary CV.
- **Posiciones abiertas** panel: the five most recently updated open positions, with how many
  candidates sit at each stage and in total.
- **Últimos disponibles**: the candidates most recently _confirmed_ available. Only the latest check
  is stored, so «Sigue igual» moves a candidate to the top.
- **Últimos añadidos**: the most recently created candidates, flagging those without a CV. After a
  bulk import it shows imported records.
- **Búsquedas guardadas**: the shared presets most recently used; each opens the advanced search
  with that preset applied.

«Con CV principal» and «Centro operativo» are gone. Each panel loads, fails and shows its empty
state on its own, and a panel the user may not see sends no request: a user holding only
`positions.read` sees the position panels, one holding only `candidates.read` the candidate
panels. Candidate rows show names only.

## API

- `GET /api/positions`: each item gains
  `stageCounts: { new, shortlisted, interview, hired, rejected }`. Like `candidateCount` they name
  no one and need only `positions.read`; links to removed candidates are counted, so the five add up
  to `candidateCount`. See [`docs/ktl-30/position-candidates.md`](../ktl-30/position-candidates.md).
- `POST /api/candidates/search`: `createdAt` joins the sort fields, with the identifier as the final
  tie-breaker, and orders the same with or without a text filter. See
  [`docs/ktl-18/list-contract.md`](../ktl-18/list-contract.md).
- No new endpoint, permission, table or grant. Nothing new is logged or audited.

## SPA

- `/app/search?preset=<id>` applies that preset on load, records the use, selects it in the picker
  and removes the parameter from the address. An invalid or deleted id shows «No se pudo cargar el
  preset.» and runs the default search. Only the id enters the address, never the filters.
- `/app/candidates?sort=createdAt` lists the newest candidates first (descending by default).

## Deployment

One migration, `CandidateCreatedAtSortIndex`, adds the partial index
`IX_CND_Candidates_IsActive_CreatedAtUtc` (`CreatedAtUtc` desc, `Id`, where `"IsActive"`). It runs
through the `migrator` as usual; the runtime role already reads the table. Release the API and the
SPA together: a new SPA against an old API would show every stage count as 0.

Rollback: redeploy the previous images. The index is harmless to keep; to drop it, run the
migrator back to the previous migration.

## Development

`npm run seed:demo` (from `frontend/`) loads a fabricated demo dataset into a local stack so every
panel shows content. See [`demo-data.md`](demo-data.md).
