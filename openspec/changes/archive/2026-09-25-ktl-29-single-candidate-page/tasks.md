## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-29` from `main`, named after the ticket file `openspec/KTL-29.md`. (Proposal: traceable KTL-29 delivery.)

## 1. Services and validation

- [x] 1.1 In `candidate-relations.service.ts`, add pure per-entry validators for languages, programs, skills, tags, education and experience, keeping today's rules and `TranslatableError` keys. (Spec: duplicate entry is refused in the draft; validation reported in the panel.)
- [x] 1.2 Add whole-list methods: `saveLanguages`, `savePrograms`, `saveSkills`, `saveTags`, `saveEducation` and `saveExperience`. Each validates the list, normalises it (`endDate` cleared when `isCurrent`) and calls `CandidateService.set<Family>`. Remove the per-item `add*`, `update*` and `remove*` methods once no caller remains. (Design D3. Spec: form panels save only their own changes.)
- [x] 1.3 Update `tests/unit/candidate-relations.service.spec.ts`:
  - whole-list validation;
  - per-entry validators;
  - one `set<Family>` call per save;
  - `TranslatableError` keys.

## 2. Panel shell and page state

- [x] 2.1 Create `components/candidate-panel.tsx` and `candidate-panel.logic.ts` (design D1):
  - the `h2`;
  - «Editar», with a panel-specific accessible name;
  - the `form` mode footer (Guardar/Cancelar) and the `actions` mode footer (Hecho);
  - focus into the panel on open, and back to «Editar» on close;
  - test ids `candidate-panel-<id>-edit/save/cancel/done`.
- [x] 2.2 In `candidate-detail-page.tsx`, add the `editing` and `dirty` state:
  - one panel at a time, with the discard confirmation through `confirmDialogService`;
  - a `useBlocker(dirty)` confirmation;
  - a `beforeunload` listener while dirty.

  (Design D6. Spec: one panel is edited at a time and unsaved changes are protected.)

- [x] 2.3 Add per-panel permissions in the page: hoisted `usePermission('candidates.update')` and `usePermission('documents.upload')`. Add the removed-candidate rule and header hint, and remove the «Editar» link to `/edit`. (Design D7. Spec: panel editing follows API permissions; detail page keeps viewing and status actions.)

## 3. Form panels

- [x] 3.1 Datos principales:
  - The read view stays the current `dl.prop-list`.
  - The edit view is `CandidateForm`, with a `formId` submitted by the shell's «Guardar».
  - Add dirty reporting.
  - Saving calls `candidateService.update`, then shows the success toast and closes the panel.
  - A conflict keeps the draft.

  (Spec: core record is saved; core record without a name; panel saves handle concurrent edits.)

- [x] 3.2 `candidate-education.tsx` and `candidate-experience.tsx`:
  - Controlled draft lists: «Añadir» validates through the service validator and appends; remove drops from the draft.
  - «Guardar» calls `saveEducation` or `saveExperience`, and «Cancelar» restores.
  - Keep the markup, `name=` attributes and test ids.

  (Spec: staged changes are saved together; cancel discards the draft.)

- [x] 3.3 `candidate-relation-section.tsx`:
  - Replace `RelationWrite` and `run()` with a controlled `items` / `onItemsChange` over the family draft.
  - Add at the lowest active level, and allow the level and detail to change in place.

  (Design D2. Spec: entry is added with the lowest level; level is changed in place; no active level.)

- [x] 3.4 `candidate-competencies.tsx` and `.logic.ts`:
  - Add the four-family draft and `changedFamilies`.
  - Save the changed families sequentially.
  - Record a per-family error and keep failed families pending for retry.
  - Show the catalog notice once in edit mode, and disable «Guardar» while the catalogs are unavailable.

  (Design D4. Spec: only changed families are written; one family is refused; catalogs are unavailable on the edit page.)

## 4. Action panels

- [x] 4.1 `candidate-notes.tsx`: drive `readOnly` from edit mode and report dirtiness (non-empty new-note text, or a note being edited). Actions stay immediate. (Design D5. Spec: note added in edit mode.)
- [x] 4.2 `candidate-documents.tsx`: drive `readOnly` from edit mode without remounting, so polling continues, and report dirtiness when a file is selected. Download stays in both modes. (Design D5. Spec: document uploaded in edit mode; download while read-only.)

## 5. Routes, create page and copy

- [x] 5.1 In `app.tsx`:
  - Add a `CandidateEditRedirect` for `candidates/:id/edit` under the `candidates.read` guard.
  - Remove the `candidates.update` route group.
  - Point `candidates/new` at the create page.

  (Design D8. Spec: former edit address.)

- [x] 5.2 Rename `candidate-edit-page.tsx` to `candidate-create-page.tsx`. Keep only the create flow, always navigate to `/app/candidates/:id` after saving, and reword the post-save note. (Spec: candidate creation continues on the candidate page.)
- [x] 5.3 Update `src/assets/i18n/es.json` with the design D9 keys, as whole Spanish sentences with correct accents. Remove keys that no longer render. Leave `LEGACY_HARDCODED_COPY` in `eslint.config.js` untouched, except to shrink it for any touched file. Optionally add `en.json` values. (Spec: candidate pages are localized and accessible.)

## 6. Unit tests

- [x] 6.1 Review and update the existing unit specs affected by the change:
  - `candidate-detail-page.spec.tsx`;
  - `candidate-edit-page.spec.tsx`, which becomes `candidate-create-page.spec.tsx`;
  - `candidate-form.spec.tsx`, `candidate-profile-sections.spec.tsx`, `candidate-relation-section.spec.tsx`;
  - `candidate-notes.spec.tsx`, `candidate-documents.spec.tsx`;
  - `candidate-competencies.logic.spec.ts`, `candidate-cv-preview.spec.tsx`;
  - breadcrumb specs that assert the `Editar` trail.

  Remove the chip-retry cases replaced by per-family errors.

- [x] 6.2 Add page cases:
  - The «Editar» matrix: reader; update without upload; upload without update; removed candidate.
  - One panel at a time, with and without unsaved changes.
  - The leave guard: blocks when dirty, passes when clean.
  - Focus moves into the panel and back.
  - The `/edit` redirect.
  - Creation navigates to `/app/candidates/:id`.
- [x] 6.3 Add panel cases:
  - Staged add and remove send nothing before «Guardar», and one call per changed family after it.
  - «Cancelar» restores the saved data.
  - A Competencias partial failure retries only the failed family.
  - A 409 keeps the draft.
- [x] 6.4 Run `npm test` from `frontend/`, inspect the unit, integration and security projects, and fix failures.

## 7. Backend regression and data state

- [x] 7.1 With Docker running, run `npm run test:backend` from `frontend/` and inspect the results. No backend file changes, so this is a regression check of the candidate, relation, note and document endpoints.
- [x] 7.2 After the e2e runs in section 8, check with `GET /api/candidates/{id}` (dev stack) that a candidate edited through each panel has the expected collections in PostgreSQL. Confirm that no document binary was left outside quarantine or clean storage by the upload journey.

## 8. End-to-end

- [x] 8.1 Update the e2e specs that assert or open `/edit` so they use the candidate page and the `candidate-panel-*` test ids:
  - `candidate-crud`, `candidate-profile`, `candidate-api-cutover`;
  - `candidate-documents`, `candidate-tags-notes`, `catalogs-crud`;
  - `candidate-list-operations`, `security-ops`, `navigation-responsive`.

  Every created record keeps its `Date.now()` marker, and selectors do not hardcode Spanish text.

- [x] 8.2 Add a panel-editing journey to `tests/e2e/candidate-profile.spec.ts`:
  - Edit and save each form panel.
  - Cancel one panel.
  - Hit the switch confirmation and the leave confirmation.
  - Check the CV preview stays beside an open panel at 1920×1080.
  - Check there is no horizontal scroll at 390px with a panel open.
- [x] 8.3 With `docker compose up` running, run the touched specs from `frontend/`: `npx playwright test tests/e2e/candidate-profile.spec.ts tests/e2e/candidate-crud.spec.ts tests/e2e/candidate-documents.spec.ts tests/e2e/candidate-tags-notes.spec.ts tests/e2e/secure-access.spec.ts`. Inspect the results and fix failures.
- [x] 8.4 Run the full `npm run e2e` from `frontend/`, inspect the report, and confirm that the global teardown purged the marked test data. Restore any seeded fixture the journeys changed.

## 9. Security checks

- [x] 9.1 In `tests/e2e/secure-access.spec.ts`, assert that the `readonly` user:
  - sees no `candidate-panel-*-edit`, no form and no `document-file`;
  - is redirected from `/edit` to the read-only candidate page instead of `/app`.

  Keep the direct-write refusals (401 unauthenticated, 403 unauthorized) for candidate, relation, note and document writes.

- [x] 9.2 Run `npm run security:rls` and `npm run security:storage` from `frontend/`, and inspect the results. Confirm with `git diff --stat main` that nothing under `backend/`, `supabase/` or the grants changed, and that no dependency was added. (Proposal: no data path or permission change.)

## 10. Documentation

- [x] 10.1 Add `docs/ktl-29/release-notes.md`, covering:
  - the single page and the `/edit` redirect;
  - per-panel Guardar/Cancelar and Hecho;
  - one panel at a time and the leave guards;
  - the `documents.upload` nuance;
  - the removed-candidate rule;
  - the Competencias partial-save behaviour.
- [x] 10.2 Update `README.md` (in Spanish) wherever it describes the candidate view and edit pages.

## 11. Gates

- [x] 11.1 Run `npm run build:all`, `npm run lint` and `npm run format:check` from `frontend/`. Inspect the outputs, fix any issues, and check tasks only after their commands pass.
- [x] 11.2 Run `openspec validate ktl-29-single-candidate-page --strict` and confirm that it is valid.
