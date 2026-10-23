## Why

Recruiters lose their place on a position's candidate list because a stage change re-sorts the
list and moves the row they just changed. On the candidate page, Educación and Experiencia rows
do not show when each entry happened, and a mistake in an entry can only be fixed by removing it
and typing it again. Brief: `openspec/KTL-43.md`.

## What Changes

- On the position page, a candidate whose stage changes keeps its row; the API order (stage, then
  newest added, then id) is unchanged and returns on the next load.
- Educación and Experiencia rows lead with a years column: `2019–2024`, a single year, or
  `2020–actualidad` for a current role. Education shows its end year, the only year it stores.
- Educación and Experiencia entries can be edited: «Editar» loads an entry into the panel's form,
  which updates it in place, staged with the panel's other changes until «Guardar».
- The experience form shows «Años de experiencia» beside «Sector», with both dates together on the
  last row.

Front end only: no API, schema, grant or permission change.

### Actors

- Recruiters with `candidates.update` (entry editing) and `positions.manage` (stage changes).
- Every candidate reader sees the years column.

### Key entities

- Position candidate link (stage, added time, version).
- Candidate education entry (type, degree, specialty, institution, status, end year, notes).
- Candidate experience entry (company, position, sector, dates, current flag, years, functions,
  notes).

### Assumptions

- The relation writes already update a stored entry whose id is sent and keep its other columns,
  so an edit needs only the client to send the edited entry with its id.
- The API resolves inactive catalog values on write, so an entry that holds a retired value can be
  saved unchanged.

### Edge cases

- An entry holding a retired education type, education status or sector is edited.
- An edit that changes nothing, or that only moves a field between blank and missing.
- The entry being edited is removed, or the panel is cancelled or saved mid-edit.
- A current role without a start date; dates in the same year; no dates at all.
- A stale stage write (409/404) still reloads the list in API order.

### Personal data, permissions and storage

Education and experience are personal data. The change adds no new data path, endpoint, field,
permission, grant or storage access: edits travel through the existing permission-checked relation
writes, which keep failing closed (principle 3), and the screens expose no field they did not
already show (principle 1). No RLS policy or role definition is touched.

### Success criteria

- A stage change on the position page moves no row (unit-tested).
- Every Educación and Experiencia row with a known year shows it in the leading column.
- An entry can be corrected and saved without removing it, keeping its id, its position and its
  hidden fields; a no-op edit leaves the panel without unsaved changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `position-candidates`: the position page keeps a restaged row in place until the next load.
- `candidate-profile-pages`: Educación and Experiencia gain the years column and entry editing,
  and the experience form's field order changes.

## Impact

- `frontend/src/app/features/positions/`: `position-candidates.logic.ts`,
  `use-position-candidates.ts`.
- `frontend/src/app/features/candidates/components/`: `candidate-education.tsx`,
  `candidate-experience.tsx`, new `candidate-period.logic.ts` and `candidate-entry-form.logic.ts`,
  `candidate-panel.css`; `frontend/src/styles.css`.
- `frontend/src/assets/i18n/es.json` and `en.json`: new keys.
- Unit specs under `frontend/tests/unit/`. No new runtime dependency.
