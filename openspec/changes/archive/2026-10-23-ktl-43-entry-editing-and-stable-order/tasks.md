## 0. Create Feature Branch

- [x] 0.1 Create the `feat/KTL-43` branch (the user creates branches and commits themselves)

## 1. Stable order on the position page

- [x] 1.1 Add `replacePositionCandidate` to `position-candidates.logic.ts` (in place, sorted insert fallback)
- [x] 1.2 Use it for `changeStage` in `use-position-candidates.ts`; keep `upsertPositionCandidate` for adds

## 2. Years column

- [x] 2.1 Add `candidate-period.logic.ts` with `educationPeriod` and `experiencePeriod`
- [x] 2.2 Add `candidate.profile.period.*` keys to `es.json` and `en.json`
- [x] 2.3 Render the leading `.item-period` column in both panels, shown only when a row has a year; drop «· Actual» when a current role has a period
- [x] 2.4 Add the `.item-period` style to `styles.css`

## 3. Entry editing

- [x] 3.1 Add `candidate-entry-form.logic.ts` with `optionsWithCurrent`, `applyEntryDraft` and `replaceEntry`
- [x] 3.2 Educación: «Editar» per row, edit mode of the form (title, focus, «Actualizar formación», «Cancelar edición»), in-place update, retired type/status kept
- [x] 3.3 Experiencia: the same flow, keeping `functions` and `notes`, retired sector kept
- [x] 3.4 End the edit on remove, panel «Cancelar» and successful «Guardar»
- [x] 3.5 Add `.item-actions` and `.item-row.is-editing` to `candidate-panel.css`, and the edit keys to `es.json` and `en.json`

## 4. Experience form layout

- [x] 4.1 Reorder the fields: Sector | Años de experiencia, «Puesto actual» on its own row, both dates on the last row

## 5. Tests

- [x] 5.1 Review the existing affected specs (`position-candidates.logic`, `position-pages`, `candidate-profile-sections`) and extend them
- [x] 5.2 New `candidate-period.logic.spec.ts` and `candidate-entry-form.logic.spec.ts`
- [x] 5.3 Run the full Vitest suite (`npm test` from `frontend/`) and inspect the result (95 files, 914 passed, 1 skipped)
- [ ] 5.4 Run the Playwright journeys `tests/e2e/position-candidates.spec.ts` and `tests/e2e/candidate-crud.spec.ts` against the running stack and inspect the result
- [x] 5.5 No API, grant, storage or permission change: no backend, PostgreSQL state or `tests/security` change needed (design, Security section)

## 6. Quality gates

- [x] 6.1 Run `npm run lint` from `frontend/`
- [x] 6.2 Run `npm run format:check` from `frontend/` over the whole repository
- [x] 6.3 Run `tsc --noEmit`; only the pre-existing `seed-demo-data.lib.spec.ts` error remains

## 7. Documentation

- [x] 7.1 Sync the `position-candidates` and `candidate-profile-pages` deltas into `openspec/specs/` (`/opsx:sync` or at archive)
- [x] 7.2 Add lines to the KTL-29 and KTL-30 sections of `README.md` (Spanish) about the years column, entry editing and the stable stage order
