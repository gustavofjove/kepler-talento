## Why

A position only shows a live search of the candidates who currently match its requirements
(KTL-15). HR cannot record who is actually being considered for a position, add a candidate
who applied directly but does not match, or track how far each person has got. This change
adds a persistent link between candidates and positions, with a stage per link. Source brief:
`openspec/KTL-30.md`.

## What Changes

- **New persistent link** between a candidate and a position (a "position candidate"), at most
  one per pair, carrying a **stage** from a closed vocabulary: `new` (Nuevo), `shortlisted`
  (Preseleccionado), `interview` (Entrevista), `hired` (Contratado), `rejected` (Descartado).
  New links start at `new`. The stage is independent of the candidate's global status.
- **Position page:**
  - a new «Candidatos de la posición» panel above «Candidatos que encajan». It has the matches'
    columns (name with e-mail, phone, CV), except that «Estado en la posición» replaces the
    status and «Añadido» the update date. It also has a stage selector, «Quitar de la posición»
    and «Añadir candidato», which opens a candidate picker;
  - each match row gets «Añadir», which turns into a disabled «Añadido» once the candidate is
    linked; match rows on this page no longer offer «Abrir CV».
- **Candidate page:** a new «Posiciones» panel listing current (open) and past (closed)
  positions, with a stage selector, «Quitar de la posición» and «Añadir a posición», which opens
  an open-position picker.
- **Rows open their record.** In both position tables and in the advanced search results, a
  click on the row, outside its links and controls, opens the candidate or position.
  - Ctrl-click and middle-click open a new tab.
  - The name or title stays a real link, for keyboard users.
  - «Ver»/«Detalle» buttons are removed, and phones in these tables are plain text.
- **The Posiciones list** gains a «Candidatos» column with each position's link count.
- **Requirements are never enforced when linking.** A non-matching candidate can be added from
  either picker.
- **Removing a link is a physical, audited delete**, meant to correct a mistaken addition.
  Rejecting a candidate is the `rejected` stage and keeps the link.
- Closed positions are read-only for links. Logically removed candidates cannot be newly added.
  A position holds at most 500 links.
- Five new endpoints under the Positions slice, one of them `GET /api/candidates/{id}/positions`.
  No new permission: link reads need `positions.read` + `candidates.read`, and link writes need
  `positions.manage` + `candidates.read`.
- New table `"OPS_PositionCandidates"` with `SELECT/INSERT/UPDATE/DELETE` for `ktl_runtime` on
  that table only.
- **BREAKING (spec):** the `position-management` statement that the system never persists
  candidate-position membership is replaced. Live matches themselves stay unpersisted.
- `search-results.tsx` gains an optional per-row action and can hide «Abrir CV». Its hardcoded
  copy moves to `es.json`, and the file leaves `LEGACY_HARDCODED_COPY`. The search page keeps its
  text, apart from the removed «Detalle».

## Capabilities

### New Capabilities

- `position-candidates`: the persistent candidate–position link:
  - stage vocabulary and transitions;
  - add, stage change and removal rules (closed positions, removed candidates, duplicates,
    concurrency, the per-position cap);
  - the five-endpoint API contract and its fail-closed permission matrix;
  - audit events;
  - the position-page panel, the match action and the candidate picker.

### Modified Capabilities

- `position-management`: live matching no longer forbids persisted membership. Match rows offer
  «Añadir» to managers, and matches themselves remain live and unpersisted. The list projection
  adds a candidate count, and match rows on the position page drop «Abrir CV».
- `candidate-profile-pages`: the candidate page gains the «Posiciones» panel.
- `postgresql-persistence`: position workflow persistence now includes the link table, whose
  runtime role may delete link rows while position rows stay undeletable.

## Impact

- **Personal data (principle 1).** The link reveals which people a position is considering and
  how each is progressing. A position's link list shows the same candidate fields as candidate
  search (name, e-mail, phone, whether a primary CV exists), behind the same `candidates.read`.
  It never includes documents, notes or other candidate fields. A candidate's list of positions
  carries no candidate contact data. The position list's candidate count names no one, so it
  needs only `positions.read`.
  Audit events carry ids and event types only, no stage value. Nothing personal goes into
  URLs, beyond ids, or into logs.
- **Authorization and least privilege (principle 3).**
  - Every endpoint checks authentication and both required permissions before validation or
    lookup, and the handlers repeat the guards.
  - No role definitions or seeded permissions change.
  - `DELETE` is granted on `"OPS_PositionCandidates"` only. `"OPS_Positions"` and
    `"CND_Candidates"` keep their revoked `DELETE`.
  - No RLS policy or document storage changes.
- **Actors:** HR managers (`positions.manage` + `candidates.read`) add, restage and remove.
  Readers (`positions.read` + `candidates.read`) see the links.
- **Key entities:** `PositionCandidate` (position id, candidate id, stage, added/updated
  timestamps, version).
- **Edge cases:**
  - duplicate add, including a concurrent race;
  - add to a closed position, or of a removed candidate;
  - a stale stage change;
  - the cap reached;
  - a reader without `candidates.read`;
  - a removed candidate still linked.
- **Success criteria:** the 16 acceptance criteria in `openspec/KTL-30.md` pass through unit,
  integration, security and e2e evidence. Link list queries use the new indexes and never read
  position requirements or description.
- **Code:**
  - `backend/Domain/Positions`, `backend/Application/Features/Positions`, `backend/Infrastructure/Persistence` (configuration, repository, migration `AddPositionCandidates`), `backend/Web/Features/Positions`;
  - `frontend/src/app/features/positions`, `frontend/src/app/features/candidates` (the new
    panel), `frontend/src/app/features/search/components/search-results.tsx`,
    `frontend/eslint.config.js`, `frontend/src/assets/i18n/es.json`;
  - `scripts/e2e-cleanup.sql`.
- **Docs:** `docs/ktl-30/`, `docs/ktl-5/database-conventions.md`, and `README.md` if its
  feature overview lists position capabilities.
- **Dependencies:** none new.
