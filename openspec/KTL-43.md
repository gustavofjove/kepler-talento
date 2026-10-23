## [original]

When updating the status of the candidates in a position, could we keep their order?

Also, for education and experience in candidates, could we show on the left of the item the years range e.g. (2019-2024) or just 2024 as long as there's a value? Finally, how hard would be to be able to edit education and experience entries? Currently you can only add and delete.

Also move "Años de experiencia" to the RHS of Sector leaving start and end date on the last row both together.

## [enhanced]

### User story

**As** a recruiter working on a position's candidates and on candidate profiles,
**I want** rows that stay where they are when I change a candidate's stage, entry years I can scan
at a glance, and education and experience entries I can correct in place,
**so that** I do not lose my place in the list, can read a career at a glance, and do not have to
remove and re-type an entry to fix one field.

### Decisions taken while refining

| Topic                     | Decision                                                                                                                                                                                                                                                                                         |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Scope of the stable order | Client only. A stage change on the position page replaces the row where it stands. The API order (stage vocabulary order, then newest added, then id) is unchanged and applies again on the next load. Adding a candidate still inserts in API order. Chosen over dropping stage from the order. |
| Candidate page links      | Unchanged: a candidate's own position list is ordered by position status, not by stage, so a stage change never moved its rows.                                                                                                                                                                  |
| Experience period         | Years from the stored dates: `2019–2024`; one year when only one date is known or both fall in the same year; `2020–actualidad` for a current role with a start date. A current role without a start date keeps the «Actual» marker and has no period.                                           |
| Education period          | Education stores only an end year, so the period is that year. A start year (and so a range) needs a new field, migration and import mapping: out of scope.                                                                                                                                      |
| Period column             | Leads each row, muted, tabular figures, fixed minimum width so rows align. Shown on every row once any row in the panel has a period; absent otherwise.                                                                                                                                          |
| Entry editing             | Front end only. The relation writes already update an entry whose id is sent (`SetCandidateRelations.cs`), so no API, schema or grant change.                                                                                                                                                    |
| Edit flow                 | «Editar» on a row loads it into the panel's existing form, staged like add and remove, and written with the panel's «Guardar». No inline row editor and no dialog.                                                                                                                               |
| Retired catalog values    | An entry being edited keeps its type, status or sector selectable even when that catalog value is no longer active; the API resolves inactive values.                                                                                                                                            |

### Functional description

#### Position page — «Candidatos de la posición»

1. Changing a candidate's stage keeps every row in its place. Reloading the page restores the API
   order. A refused stale write still reloads the list, as today.

#### Candidate page — Educación and Experiencia

2. **Period column.** Each row starts with its years in a muted column (see the decisions above).
   For a current role with a period, the trailing «· Actual» marker is dropped.
3. **«Editar».** In edit mode each row offers «Editar» before «Quitar». Its accessible name names
   the entry: «Editar la formación {degree}», «Editar la experiencia {position} en {company}».
4. Activating it fills the form with the entry, renames the form «Editar formación» / «Editar
   experiencia», focuses the form's first field and highlights the row. The submit button becomes
   «Actualizar formación» / «Actualizar experiencia», and «Cancelar edición» appears.
5. «Actualizar …» validates with the same rules as an add, replaces the entry where it stands, and
   returns the form to «Nueva …». Fields the form does not show (`notes`, `functions`) are kept.
   An update that changes nothing leaves the panel clean.
6. «Cancelar edición» returns the form to «Nueva …» without changing the entry. Removing the entry
   being edited, the panel's «Cancelar», or a successful «Guardar» also leave the edit.
7. **Experience form layout.** Empresa | Puesto; Sector | Años de experiencia; «Puesto actual» on
   its own row (it governs the end date); Fecha inicio | Fecha fin together on the last row.

### API and data contract

No change. `PUT /api/candidates/{id}/education` and `/experience` already replace the collection
as a set against the candidate version and update entries whose `id` matches a stored row.

### Files to change (`frontend/`)

| File                                                                           | Change                                                             |
| ------------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| `src/app/features/positions/position-candidates.logic.ts`                      | `replacePositionCandidate` (in place; falls back to sorted insert) |
| `src/app/features/positions/use-position-candidates.ts`                        | `changeStage` uses it                                              |
| `src/app/features/candidates/components/candidate-period.logic.ts` (new)       | `educationPeriod`, `experiencePeriod`                              |
| `src/app/features/candidates/components/candidate-entry-form.logic.ts` (new)   | `optionsWithCurrent`, `applyEntryDraft`, `replaceEntry`            |
| `src/app/features/candidates/components/candidate-education.tsx`               | Period column, edit flow                                           |
| `src/app/features/candidates/components/candidate-experience.tsx`              | Period column, edit flow, field order                              |
| `src/app/features/candidates/components/candidate-panel.css`, `src/styles.css` | `.item-period`, `.item-actions`, `.item-row.is-editing`            |
| `src/assets/i18n/es.json` (+ `en.json`)                                        | Keys below                                                         |

New test ids: `education-period`, `experience-period`, `education-edit`, `experience-edit`. The
forms keep `education-add-form` / `experience-add-form` and every `name=`.

**i18n keys (Spanish)**

```json
"candidate.profile.period.range": "{{start}}–{{end}}",
"candidate.profile.period.current": "{{start}}–actualidad",
"candidate.profile.action.edit": "Editar",
"candidate.profile.action.cancelEdit": "Cancelar edición",
"candidate.profile.education.editTitle": "Editar formación",
"candidate.profile.education.editLabel": "Editar la formación {{degree}}",
"candidate.profile.education.update": "Actualizar formación",
"candidate.profile.experience.editTitle": "Editar experiencia",
"candidate.profile.experience.editLabel": "Editar la experiencia {{position}} en {{company}}",
"candidate.profile.experience.update": "Actualizar experiencia"
```

### Acceptance criteria

1. **Stable stage change.** Given two «Nuevo» candidates, when the first is moved to «Descartado»,
   it stays the first row until the page is reloaded.
2. **Experience period.** An entry from 2019-03-01 to 2024-06-30 shows `2019–2024`; one with only a
   start date in 2019 shows `2019`; a current role since 2020 shows `2020–actualidad` and no
   «· Actual».
3. **Education period.** An entry with end year 2020 shows `2020`; without one, an empty cell when
   other rows have a year, and no column when none do.
4. **Edit in place.** Editing an education entry's centre and saving writes the list with the same
   entry id and the new centre, in the same position.
5. **Hidden fields kept.** Editing an experience keeps its `functions` and `notes`.
6. **Retired value.** An entry whose status is no longer an active catalog value shows that status
   selected in the edit form and saves it unchanged.
7. **Clean no-op.** «Actualizar» without changes leaves the panel without unsaved changes.
8. **Validation.** Blanking a required field and pressing «Actualizar» shows the Spanish message
   and leaves the entry unchanged.
9. **Layout.** The experience form's fields are ordered company, position, sector, years, current,
   start date, end date.

### Test coverage

| Suite                                                          | Coverage                                                                                                       |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `frontend/tests/unit/position-candidates.logic.spec.ts`        | `replacePositionCandidate` in place and fallback insert                                                        |
| `frontend/tests/unit/position-pages.spec.tsx`                  | A restaged candidate keeps its row                                                                             |
| `frontend/tests/unit/candidate-period.logic.spec.ts` (new)     | Ranges, single years, current role, education end year                                                         |
| `frontend/tests/unit/candidate-entry-form.logic.spec.ts` (new) | Retired option, changed-fields merge, blank clearing, in-place replace                                         |
| `frontend/tests/unit/candidate-profile-sections.spec.tsx`      | Period column, edit in place, retired status, no-op clean, cancel edit, validation, hidden fields, field order |

### Documentation

- `openspec/specs/position-candidates/spec.md`: the position page keeps a restaged row in place.
- `openspec/specs/candidate-profile-pages/spec.md`: period column, entry editing, experience form
  order.

### Non-functional requirements

- **Security:** no new endpoint, permission, grant or data path. Edits go through the existing
  permission-checked relation writes.
- **Accessibility:** «Editar» buttons are named per entry; the form's accessible name follows its
  mode; focus moves to the form's first field when an edit starts.
- **Responsive:** the period column and row actions fit at 390px without horizontal scrolling.

### Out of scope

- A start year for education, and so education ranges.
- Changing the API order of a position's candidates.
- Editing entries inline in the row or in a dialog.
