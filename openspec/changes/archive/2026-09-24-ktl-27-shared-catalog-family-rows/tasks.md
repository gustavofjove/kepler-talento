## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-27` from `main`, named after the ticket file `openspec/KTL-27.md`. (Proposal: traceable KTL-27 delivery.)

## 1. Shared family rows

- [x] 1.1 Add `CATALOG_FAMILY_ORDER` in `features/catalogs/components/catalog-family-rows.logic.ts` and the `CatalogFamilyRows` component (`renderRow`, optional `notice`), with `catalog-family-rows.css` holding the row border, padding and 6.5rem label column moved verbatim from `search-filters.css` (design D1, D2). Add a unit spec covering order, one `data-family` row per kind and the notice slot. (Spec: families share one row layout.)
- [x] 1.2 Render the four `CriteriaGroup`s in `search-criteria-form.tsx` through `CatalogFamilyRows`. Keep the `.criteria-group[data-criteria]` wrapper and the single catalog notice, and delete the moved declarations from `search-filters.css`. (Spec: same rows in every host; design D2.)
- [x] 1.3 Change `search.criteria.skill.label` to `Habilidades` in `es.json` (and `en.json` if present). Review and update `search-criteria-summary.spec.tsx`, `search-criteria-form.spec.tsx`, `search-criteria-dialog.spec.tsx` and `i18n.spec.ts` for the label. (Spec: family labels in every host.)

## 2. Required level defaults to the lowest

- [x] 2.1 In `CatalogValuePicker`, commit a required-level choice at once with `levelOptions[0]` and refocus the input. Remove the `DRAFT_KEY` draft path and the unused `catalogPicker.levelRequired` copy (design D5). (Spec: required level defaults to the lowest.)
- [x] 2.2 Disable the add control of a required-level picker whose `levelOptions` is empty, keeping its chips visible and removable (design D6). (Spec: required level family has no active value.)
- [x] 2.3 Review and update `catalog-value-picker.spec.tsx` and `catalog-value-picker.logic.spec.ts`: replace the draft and abandon cases with an immediate add at the first level, keep the change-on-chip case, and add the no-level case. (Spec: optional and required levels; read-only and disabled pickers.)

## 3. Competencias panel and stacked candidate pages

- [x] 3.1 Turn `CandidateRelationSection` into a row. Keep its `data-testid` wrapper, drop the `<h3>`, `labelHidden` and its own notice, and keep `candidate.profile.<family>.title` as its visible label (design D3). (Spec: families are named by their row labels.)
- [x] 3.2 Add `CandidateCompetencies` (`article.panel[data-testid="candidate-competencies"]`, `<h2>` `Competencias`, one notice, `CatalogFamilyRows`) with `candidate` and `readOnly` props. Add the `candidate-competencies.logic.ts` no-level message helper with the new `candidate.profile.competencies.title` and `.noLevels` keys (design D4, D6). (Spec: Competencias panel and stacked sections.)
- [x] 3.3 Replace the `grid two` layouts in `candidate-edit-page.tsx` and `candidate-detail-page.tsx` with the stacked order from the spec, including Datos principales and Auditoría on the detail page, and drop the redundant `span-all` (design D4). (Spec: edit page layout; detail page layout.)
- [x] 3.4 Review and update `candidate-relation-section.spec.tsx`, `candidate-edit-page.spec.tsx`, `candidate-detail-page.spec.tsx` and `catalog-loading-states.spec.tsx`. Cover the panel heading, row order, labelled pickers, read-only empty text, the single notice, the add at the lowest level and the no-level notice. Add a unit spec for the competencies logic helper. (Spec: every candidate-profile-pages scenario touched.)

## 4. Browser journeys

- [x] 4.1 Update `tests/e2e/support/catalog-picker.ts` so `addValue` waits for the saved chip, then calls `setLevel` only when a different level is requested (design, Risks). Fix any caller that still expects the editor on add in `candidate-profile.spec.ts`, `candidate-api-cutover.spec.ts`, `catalogs-crud.spec.ts` and `candidate-tags-notes.spec.ts`. (Spec: entry is saved with the lowest level.)
- [x] 4.2 Add e2e coverage: on the candidate edit and detail pages, sections are stacked in spec order and Competencias rows are in family order; on search and a candidate page, rows match in order and label column width; no horizontal overflow at 390 px. Selectors use test ids and roles, not Spanish text. (Spec: same rows in every host; narrow viewport.)
- [x] 4.3 With `docker compose up` running, run from `frontend/`: `npx playwright test tests/e2e/candidate-profile.spec.ts tests/e2e/candidate-tags-notes.spec.ts tests/e2e/candidate-api-cutover.spec.ts tests/e2e/catalogs-crud.spec.ts tests/e2e/navigation-responsive.spec.ts tests/e2e/advanced-search.spec.ts tests/e2e/advanced-search-presets.spec.ts tests/e2e/positions.spec.ts`. Inspect the results, fix failures, and restore any seed data or catalog activation the journeys changed. (Spec: all scenarios.)

## 5. Verify suites and boundaries

- [x] 5.1 Run `npm test` from `frontend/` (unit, integration and security projects) and inspect the results. Confirm the existing relation-write fail-closed tests for unauthenticated and unauthorized callers still pass unchanged. (Proposal: unchanged authorization boundary; design D7.)
- [x] 5.2 Confirm with `git diff --stat main` that no file under `backend/`, `supabase/` or a migration changed, and that no dependency was added to `frontend/package.json`. Run `npm run security:rls` and `npm run security:storage` from `frontend/` and inspect the output. (Proposal: no API, grant, RLS or storage change.)

## 6. Document and finish

- [x] 6.1 Add `docs/ktl-27/release-notes.md`: the shared rows, the Competencias panel, the stacked pages, the default level, the `Habilidades` label, and the changed e2e helper. Update the KTL-24 section of `README.md` (in Spanish), which says a candidate entry is saved only once its level is chosen. (Proposal: docs.)
- [x] 6.2 Run `npm run build:all`, `npm run lint` and `npm run format:check` from `frontend/`; inspect the outputs and check tasks only after their commands pass. (Repository delivery gate.)
