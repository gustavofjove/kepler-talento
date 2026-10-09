## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-42` from an up-to-date `main`

## 1. Picker and copy

- [x] 1.1 Add `candidate.cvDraft.attach` and `candidate.cvDraft.attachFailed`, and update `candidate.cvDraft.hint`, in `frontend/src/assets/i18n/es.json` (design D5; `en.json` optional)
- [x] 1.2 Give `CvDraftPicker` an optional `attach` prop rendering a checkbox `name="attachCv"`, `data-testid="cv-draft-attach"`, labelled via `t('candidate.cvDraft.attach')`, disabled while busy; style in `cv-draft-picker.css` (no inline styles)

## 2. Create page

- [x] 2.1 In `CandidateCreatePage`, hold `heldCv` and `attachCv` per design D2: clear on a new pick, set only when the current `extract` resolves (fields or `no_text`), reset `attachCv` to `true` on each held CV (spec: Latest CV wins, Refused CV is not offered)
- [x] 2.2 Hoist `usePermission('documents.upload')` and pass `attach` to the picker only when a CV is held and the permission is present (spec: Actor cannot upload documents)
- [x] 2.3 Extend `save` per design D4: after a successful create, upload the held CV through `documentService.upload` as primary `CV` when chosen; on failure show the `warning` toast; navigate in both cases; on create failure upload nothing and keep the held CV (spec: CV is attached on save, Attach fails…, Candidate creation fails)
- [x] 2.4 Update the page's doc comment (KTL-32 → KTL-42 behaviour) and make sure nothing logs the file or its name

## 3. Unit tests

- [x] 3.1 Review existing `tests/unit/candidate-create-page.spec.tsx` cases (KTL-29/KTL-32) and update any that assume the CV is never attached
- [x] 3.2 Add create-page cases: checkbox absent before a read, after a refusal and without `documents.upload`; present and ticked after a draft and after `no_text`; save uploads the held file (`CV`, primary, new id); unticked → no upload; second pick replaces the first; upload failure → warning toast and navigation; create failure → no upload and choice kept
- [x] 3.3 Add `CvDraftPicker` cases: checkbox only with `attach`, reports changes, disabled while busy
- [x] 3.4 Run `npx vitest run tests/unit/candidate-create-page.spec.tsx` and the picker spec, then `npm test`, and inspect the output

## 4. Security evidence

- [x] 4.1 Confirm `backend/Tests/IntegrationTests/CandidateApiTests.cs` asserts `POST /api/candidates/{id}/documents` is refused (403, nothing stored) for unauthenticated and non-`documents.upload` actors; add the case if missing (spec: Upload endpoint still fails closed)
- [x] 4.2 Confirm `CandidateDraftApiTests` still prove the draft endpoint stores no document or object (unchanged KTL-32 requirement)
- [x] 4.3 Run `npm run test:backend` (Docker running) and `npm run security:rls && npm run security:storage`; inspect the output and confirm no new document rows or stored objects result from a draft alone

## 5. End to end

- [x] 5.1 Update `tests/e2e/candidate-cv-draft.spec.ts`: the existing journey now expects `cv-<marker>.pdf` in `candidate-documents`; add a case that unticks `cv-draft-attach` and expects no document (selectors by test id, not Spanish text; files carry the `Date.now()` marker)
- [x] 5.2 With `docker compose up` running, run `npx playwright test tests/e2e/candidate-cv-draft.spec.ts` and inspect the result
- [x] 5.3 Confirm the global teardown purged the marked candidates and their documents afterwards

## 6. Quality gates and documentation

- [x] 6.1 Run `npm run lint` and `npm run format:check` from `frontend/` and fix any finding
- [x] 6.2 Run `npm run build:all`
- [x] 6.3 Update `README.md` (Spanish) lines 125-129: the CV is attached on save when the box is ticked
- [x] 6.4 Add `docs/ktl-42/release-notes.md` (behaviour, no API/DB change, failure handling)
- [x] 6.5 Run `openspec validate ktl-42-attach-cv-on-create --strict` and fix any issue
