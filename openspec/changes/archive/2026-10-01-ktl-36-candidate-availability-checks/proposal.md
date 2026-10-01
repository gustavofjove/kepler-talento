## Why

A candidate carries a global status (`new`, `available`, `in_process`, `hired`, `rejected`) that
overlaps with the per-position stage introduced by KTL-30 (`new`, `shortlisted`, `interview`,
`hired`, `rejected`). The two are deliberately independent, so a candidate can be «Descartado»
globally while at «Entrevista» for a position, and nothing tells HR which is true. The only part of
the global status that does not describe a hiring process, "available", has no date, so nobody can
tell how stale it is. A free-text «Disponibilidad» field competes with it. Source brief:
`openspec/KTL-36.md`.

## What Changes

- **BREAKING: the global candidate status is removed** from the domain, the database, the API, the
  CSV import, search, sorting, presets, position requirements, the export and every screen. The
  position stage becomes the only pipeline state.
- **BREAKING: the free-text `availability` field is removed** everywhere it appears.
- **New availability check** on each candidate:
  - a value: `unknown` (default), `available` or `unavailable`;
  - an optional «hasta» date, only for `unavailable`;
  - the date it was checked (a calendar date, defaulting to today, at most one day after the
    current UTC date, and allowed to be earlier than the stored one so a mistake can be amended);
  - who checked it, assigned by the server.

  Only the latest check is kept.

- **New endpoint** `PUT /api/candidates/{id}/availability` (`candidates.update`, candidate version).
  Every availability write goes through it. The general candidate update no longer touches
  availability, so editing other fields can never make an old check look fresh.
- **Candidate page:**
  - an availability block under the contact line, with «Sigue igual» (same value, today's date),
    «Cambiar…» (inline form) and an inline «Deshacer» after each check;
  - a check whose «hasta» date has passed is marked «(vencido)» and cannot be reconfirmed;
  - «En proceso» and «Contratado» badges beside the name, derived from the candidate's position
    links and shown only to actors with `positions.read`;
  - «Datos principales» loses «Estado» and «Disponibilidad».
- **Tables, search and list:**
  - the «Estado» column becomes «Disponibilidad» (value and time elapsed);
  - the `status` sort becomes `availabilityCheckedOn`, with unchecked candidates last;
  - the status filter family becomes an availability family: `availabilityValues` (ANY) plus
    `availabilityCheckedFrom`.
- **BREAKING: stored filter documents move to schema version 2.** A migration drops `statusValues`
  from saved presets and position requirements, which broadens any restricted selection. This is
  accepted because all current data is test data.
- **BREAKING: CSV import (KTL-17)** no longer accepts the `status` and `availability` columns; a file
  that still has them is refused as having unknown columns. Imported candidates start `unknown`.
- **Access migration (KTL-7):** the legacy `Status` and `Availability` columns are still exported
  and validated, but they are loaded as text appended to the candidate's notes. Every migrated
  candidate starts `unknown`.
- **Audit:** a new `candidate.availability_checked` event that carries no value or date.
  `candidate.status_changed` is no longer written but stays readable.
- **CSV export:** `estado` is replaced by `disponibilidad` and `comprobado_el`.

## Capabilities

### New Capabilities

None. The availability check belongs to the existing candidate capabilities.

### Modified Capabilities

- `candidate-management`: "Constrained candidate status" is replaced by the availability check: its
  values, date rules, the dedicated write, removed-candidate refusal and audit.
- `candidate-persistence`: the status and free-text availability are no longer persisted; the
  availability columns and their database constraints are.
- `candidate-search`: the status family becomes the availability family, the projection carries
  availability instead of status, and the sort set replaces `status` with `availabilityCheckedOn`.
- `saved-search-presets`: presets store filter schema version 2, and the editor presents
  availability and «Comprobado desde» instead of candidate status.
- `position-management`: requirements use filter schema version 2, and "unsupported candidate
  status" becomes "unsupported availability".
- `position-candidates`: the sentences that relate stages to the candidate's own status are removed.
- `candidate-profile-pages`: the availability block and the derived pipeline badges, and the
  reduced «Datos principales».
- `data-tables`: the candidate tables show «Disponibilidad» instead of a candidate status.
- `candidate-import`: the column set loses `status` and `availability`.
- `legacy-data-migration`: the legacy status and availability load as note text, and every
  candidate loads as `unknown`.
- `personal-data-encryption`: "availability" leaves the list of encrypted free-text values.

`audit-trail` needs no delta: its event catalogue is defined in code. The new event type and the
read-only retention of `candidate.status_changed` are specified under `candidate-management`.

## Impact

- **Personal data (principle 1).** Availability is personal data about a person's working
  situation. Only three values and two dates are stored, plus the checker's user id. The API
  exposes the checker as a display name, only to actors who can already read the candidate, as
  note authors are. Removing the free-text field removes an unbounded personal-data field. Audit
  events carry the candidate id only. Nothing new is logged. The export carries only the value and
  the check date.
- **Authorization and least privilege (principle 3).**
  - The new endpoint checks authentication and `candidates.update` before validation or lookup,
    and the handler repeats the guard.
  - No permissions or role definitions change, and there is no new table.
  - `ktl_runtime` keeps its existing `SELECT, INSERT, UPDATE` on `CND_Candidates`.
  - No RLS policy or document storage changes.
- **Actors:**
  - HR users with `candidates.update` record checks;
  - readers with `candidates.read` see availability;
  - readers with `positions.read` also see the badges.
- **Key entities:** `Candidate` availability: state, checked-on date, until date and the checker's
  user id. The pipeline badges are derived from `PositionCandidate` and never stored.
- **Edge cases:**
  - a check date in the future, or one earlier than the stored check (allowed);
  - a «hasta» date with the wrong value or before the check date;
  - a lapsed «hasta» date;
  - resetting to `unknown`;
  - undo after a reconfirm;
  - a stale version;
  - a removed candidate;
  - an old URL sorting by `status`;
  - a v1 filter document;
  - a CSV with the old columns;
  - a legacy row with both values blank.
- **Success criteria:** the 22 acceptance criteria in `openspec/KTL-36.md` pass with unit,
  integration, security and e2e evidence. The availability filter and sort run in SQL on clear
  columns within the KTL-33 search budget; the new index serves the date filter.
- **Code:**
  - `backend/Domain/Candidates`, `backend/Domain/Auditing`;
  - `backend/Application/Features/Candidates`, `backend/Application/Features/Search`,
    `backend/Application/Features/Positions`, `backend/Application/Import`;
  - `backend/Infrastructure/Persistence`: configuration, the search query, the repository, the
    cipher column set and the migration `ReplaceCandidateStatusWithAvailability`;
  - `backend/Web/Features/Candidates`;
  - `backend/Tools/DataMigration`;
  - `frontend/src/app/features/candidates`, `features/search`, `features/dashboard` and
    `features/admin/audit`, plus `core/i18n/format.ts` and `assets/i18n/es.json`.
- **Docs:**
  - `docs/ktl-36/`;
  - `docs/ktl-17`, `docs/ktl-7`, `docs/ktl-8`, `docs/ktl-10`, `docs/ktl-18` and `docs/ktl-19`;
  - `README.md` if it mentions candidate states.
- **Dependencies:** none new. Elapsed time uses `Intl.RelativeTimeFormat`.
