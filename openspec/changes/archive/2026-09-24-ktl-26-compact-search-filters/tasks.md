## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-26` from the ticket filename, preserving the existing working-tree changes before implementation. (Proposal: traceable KTL-26 delivery.)

## 1. Implement the shared basic filters

- [x] 1.1 Review and update existing `search-criteria-form` unit tests and the affected search, preset and position editor tests for the new controls and accessible names. (Spec: same editor in every host; default choices.)
- [x] 1.2 Add pure CV and status selection/summary helpers and focused tests for `''`/`yes`/`no`, legacy empty status arrays, checkbox selection, select all and non-empty selection. (Spec: CV mapping and status selection scenarios.)
- [x] 1.3 Replace the CV select and always visible status fieldset in the shared criteria form with two controlled CV checkboxes and an initially closed status disclosure; add localized labels and actions. (Spec: default, selection, disclosure and keyboard scenarios.)
- [x] 1.4 Update the form CSS so text, CV and closed status share a row when space permits, wrap when narrow, and show two, three or seven expanded status options without overlap or horizontal overflow. (Spec: variable option count and viewport.)
- [x] 1.5 Close the status options on outside pointer interaction and Escape, preserving the selected values; add focused component and browser coverage. (Spec: dismiss status options.)
- [x] 1.6 Remove repeated per-status `Solo` buttons, keep checkbox selection and the restore-all action, and update focused tests and documentation. (Spec: select one status and restore all.)
- [x] 1.7 Show statuses in an anchored vertical popover and add a radio-shaped button to select only one status; update focused tests, browser coverage and documentation. (Spec: quick single-status selection and compact layout.)

## 2. Verify host behavior and browser journeys

- [x] 2.1 Update existing unit tests for advanced search, preset editing and position editing; verify a disclosure-only toggle submits nothing, the outer form collapse still works, and existing saved filter values render and save with the same meaning. (Spec: search behavior, saved criteria and shared editor.)
- [x] 2.2 Update Playwright selectors that target `select[name="hasCv"]` in `advanced-search.spec.ts` and `advanced-search-presets.spec.ts`, plus affected position selectors; add browser coverage for CV toggles, checkbox selection, select all and narrow-width wrapping. (Spec: CV/status interaction and viewport.)
- [x] 2.3 Run the affected frontend unit specs and `npm run test:integration` from `frontend/`; inspect results and fix failures. (Spec: shared editor and existing search semantics.)
- [x] 2.4 Run targeted Playwright journeys from `frontend/`: `npx playwright test tests/e2e/advanced-search.spec.ts tests/e2e/advanced-search-presets.spec.ts tests/e2e/positions.spec.ts`; inspect the wide and narrow layout results and restore any test seed data changed by the journeys. (Spec: shared editor and responsive layout.)

## 3. Preserve security and data boundaries

- [x] 3.1 Run the existing backend `SearchApiTests` against isolated PostgreSQL and inspect the unauthenticated and unauthorized failures, primary-CV and status cases; verify no backend schema, grant or private-storage implementation changed. Browser position tests leave only their documented closed test positions in the development database. (Proposal: unchanged API and personal-data boundary.)
- [x] 3.2 Run `npm run test:security`, `npm run security:rls` and `npm run security:storage` from `frontend/`; inspect the fail-closed authorization and least-privilege/private-storage evidence and document any environment limits. (Proposal: unchanged permission, RLS and storage boundaries.)

## 4. Document and finish

- [x] 4.1 Update `docs/ktl-26/` with the delivered interaction and release note, and reconcile any user-visible shared-editor documentation with the final UI. (Spec: shared editor; proposal: user value.)
- [x] 4.2 Run `npm run build`, `npm run lint` and `npm run format:check` from `frontend/`; inspect outputs and leave all KTL-26 tasks checked only after their commands actually pass. (Spec: accessible, localized shared editor; repository delivery gate.)
