## 0. Create Feature Branch

- [x] 0.1 Work on branch `feat/KTL-44` (the user creates the branch and commits; the agent does not)

## 1. Candidate origin module

- [x] 1.1 Create `frontend/src/app/features/candidates/candidate-origin.ts` with the `CandidateOrigin` type, `CandidateOriginContext`, `useCandidateLinkState()` and `readCandidateOrigin(state)` (design D2)
- [x] 1.2 Add `frontend/tests/unit/candidate-origin.spec.ts`: a valid origin is accepted; `null`, non-objects, a wrong `kind`, missing or empty id or title are rejected

## 2. Carry the origin from the position page

- [x] 2.1 `frontend/src/app/shared/components/row-link.ts`: `rowLink(to, state?)` passes `{ state }` to `navigate`; the new-tab branch stays stateless (design D3)
- [x] 2.2 `position-detail-page.tsx`: provide `CandidateOriginContext` with the loaded position's id and title around its content
- [x] 2.3 `position-candidates-panel.tsx`: row `rowLink` and name `Link` carry `useCandidateLinkState()`
- [x] 2.4 `search-results.tsx`: row `rowLink` and name `Link` carry `useCandidateLinkState()` (no state outside a provider)
- [x] 2.5 `row-cv-preview.tsx`: «Ver ficha» (`row-cv-open-profile`) carries `useCandidateLinkState()`

## 3. Candidate page trail

- [x] 3.1 `candidate-detail-page.tsx`: build `Posiciones › title` parents from `readCandidateOrigin(useLocation().state)`, gated by `usePermission('positions.read')`, with testIds `breadcrumb-positions` / `breadcrumb-position`; keep the `Candidatos` parent otherwise (design D4)
- [x] 3.2 Confirm the candidate page's links to positions (`candidate-positions-panel.tsx`) pass no state, so the trail cannot grow (design D5)

## 4. Unit tests

- [x] 4.1 Review and update existing unit specs touching the breadcrumb or candidate links (`candidate-detail-page.spec.tsx`, `position-pages.spec.tsx`, `breadcrumb.spec.tsx`, search and candidate list specs), replacing any assertion of the old "does not depend on the origin" scenario
- [x] 4.2 `candidate-detail-page.spec.tsx`: default trail; position trail with both links; loading and failure keep `Posiciones › title`; malformed state falls back; without `positions.read` both position segments are plain text
- [x] 4.3 `position-pages.spec.tsx`: activating a linked candidate's name and row, a match's name, and «Ver ficha» reaches the candidate route with the origin in location state (the CV panel's «Ver ficha» and the no-origin case are covered in `search-results.spec.tsx`)
- [x] 4.4 Run `npx vitest run tests/unit/candidate-origin.spec.ts tests/unit/candidate-detail-page.spec.tsx tests/unit/position-pages.spec.tsx` from `frontend/` and inspect the output
- [x] 4.5 Run the full `npm test` (unit + integration + security) from `frontend/` and inspect the output; no backend, PostgreSQL or storage state changes in this slice, so confirm none was touched

## 5. End to end

- [x] 5.1 Add to `frontend/tests/e2e/position-candidates.spec.ts` (it already sets up linked candidates): create a `Date.now()`-marked position and candidate, link them, open the candidate from the position, assert `breadcrumb-position` shows the title, activate it and land on the position
- [x] 5.2 Same spec: from that candidate page, follow the title and go Back, and assert the position trail remains (a reload signs the user out, so it is not asserted; see design D1)
- [x] 5.3 With `docker compose up` running, run `npx playwright test tests/e2e/position-candidates.spec.ts tests/e2e/positions.spec.ts` and `npx playwright test tests/e2e/candidate-crud.spec.ts tests/e2e/advanced-search-presets.spec.ts`, and inspect the results
- [x] 5.4 Confirm the global teardown purged the marked test records

## 6. Security evidence

- [x] 6.1 Confirm no endpoint, permission, grant, storage or data path changed (diff review); the route guard still refuses `/app/positions/:id` for a profile without `positions.read` even when a crafted origin renders the title as plain text (covered by 4.2)
- [x] 6.2 Confirm the navigation state holds no candidate data and nothing is added to the URL, document title or logs

## 7. Quality gates and documentation

- [x] 7.1 Run `npm run lint` and `npm run format:check` from `frontend/` and fix any findings
- [x] 7.2 Run `npm run build:all` from `frontend/` and inspect the output (`tsc` fails only on two pre-existing TS4111 errors in `tests/unit/seed-demo-data.lib.spec.ts:216`, from commit 9284d77 and outside this change; `vite build` and `build:backend` pass with 0 warnings)
- [x] 7.3 Update the «Ruta de navegación» section of `README.md` (in Spanish) with the position-origin trail
- [x] 7.4 Add `docs/ktl-44/release-notes.md` describing the new trail, its fallbacks and the three-segment limit
