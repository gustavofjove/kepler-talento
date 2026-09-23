Requirement references point to `specs/candidate-profile-pages/spec.md`:
R1 detail page is read-only · R2 detail page keeps viewing and status actions · R3 edit page owns
every change · R4 edit page states its save behaviours · R5 creation continues on the edit page ·
R6 localized and accessible. Paths are relative to `frontend/` unless stated.

## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-22` from an up-to-date `main`

## 1. Read-only mode in the section components (R1, R3)

- [x] 1.1 Replace the `canEdit` prop of `candidate-languages.tsx`, `candidate-programs.tsx`,
      `candidate-education.tsx`, `candidate-experience.tsx` and `candidate-skills.tsx` with
      `readOnly?: boolean`; compute `editable = !readOnly && usePermission('candidates.update')`
      with the hook hoisted to the top
- [x] 1.2 Add `readOnly?: boolean` to `candidate-tags.tsx` and `candidate-notes.tsx` with the same
      rule (no edit, retire, add or remove control when `readOnly`)
- [x] 1.3 Add `readOnly?: boolean` to `candidate-documents.tsx`: upload, primary and replace
      controls require `!readOnly && documents.upload`; list, availability polling and download
      (`documents.download`) stay in both modes
- [x] 1.4 Move every JSX and attribute string in `candidate-documents.tsx` to
      `candidate.profile.documents.*` keys in `src/assets/i18n/es.json` (values unchanged) and
      remove the file from `LEGACY_HARDCODED_COPY` in `eslint.config.js`

## 2. Read-only detail page (R1, R2)

- [x] 2.1 In `pages/candidate-detail-page.tsx`, pass `readOnly` to all eight sections and keep
      `usePermission('candidates.update')` only for the «Editar» link and activate/deactivate
- [x] 2.2 Confirm `CandidateCvPreview` and document download still render for `documents.download`

## 3. Edit page owns every change (R3, R4, R5, R6)

- [x] 3.1 Remove `CandidateTags` from `components/candidate-form.tsx`; move all its copy to
      `candidate.form.*` keys and remove the file from `LEGACY_HARDCODED_COPY`
- [x] 3.2 In `pages/candidate-edit-page.tsx`, for an existing candidate, render the heading, the
      save hint, a «Ver candidato» link, the form in a «Datos principales» panel, then the eight
      sections (editable) in a `.grid.two`, notes with `.span-all`
- [x] 3.3 For `/candidates/new`, render only the form and the `candidate.edit.newHint` line
- [x] 3.4 Change the save flow: an update stays on the page and shows `candidate.edit.saved` via
      `toastService`; a create navigates to `/app/candidates/:id/edit` when `candidates.update` is
      held (hoisted `usePermission`), otherwise to `/app/candidates/:id`
- [x] 3.5 Move all copy of `candidate-edit-page.tsx` to `candidate.edit.*` keys (replacing the
      stale RLS subtitle with the save hint) and remove the file from `LEGACY_HARDCODED_COPY`
- [x] 3.6 Review `placeholder`, `aria-label` and `title` attributes in the three migrated files by
      hand, since lint does not catch them; add `en.json` values where cheap

## 4. Update existing unit tests

- [x] 4.1 Review and update `tests/unit/candidate-profile-sections.spec.tsx` for the `readOnly`
      prop: read-only renders data and no controls with every permission granted; editable still
      follows `candidates.update`
- [x] 4.2 Update `candidate-tags.spec.tsx`, `candidate-notes.spec.tsx` and
      `candidate-documents.spec.tsx` with the same read-only / editable cases (documents: download
      kept, upload hidden)
- [x] 4.3 Update `candidate-form.spec.tsx` (no tags section) and any spec that mounts the detail
      page or asserts the removed Spanish literals (e.g. `catalog-loading-states.spec.tsx`)
- [x] 4.4 Add `tests/unit/candidate-edit-page.spec.tsx`: all sections for an existing candidate;
      form and hint only for new; update stays on the page and shows the toast; create navigates
      to `/edit` with `candidates.update` and to the detail page without it; a section write
      followed by a core save sends the fresh version (no 409)
- [x] 4.5 Check `tests/unit/i18n.spec.ts` covers the new keys and reports none missing or orphaned

## 5. Run unit and integration suites

- [x] 5.1 Run `npm test` from `frontend/` and inspect the output; fix failures
- [x] 5.2 Run `npm run build:all` and confirm it passes with warnings as errors
- [x] 5.3 Run `npm run test:backend` (Docker running) to confirm the untouched API suites,
      including the candidate, note and document authorization tests, still pass; confirm no
      PostgreSQL schema or storage state changed (no migration added, `git status` shows no
      backend diff)

## 6. End-to-end

- [x] 6.1 Grep `tests/e2e` for the section `data-testid`s and `/app/candidates/` navigations, and
      list every spec that edits from the detail page
- [x] 6.2 Update `candidate-profile.spec.ts`, `candidate-documents.spec.ts`,
      `candidate-tags-notes.spec.ts`, `candidate-api-cutover.spec.ts` and `secure-access.spec.ts` (also `candidate-crud`, `candidate-list-operations`, `catalogs-crud` and `security-ops`, which expected creation to land on the detail page)
      to perform their edits on `/edit` (selectors by `data-testid`, role or `name`; no Spanish
      text)
- [x] 6.3 Add e2e assertions: as `rrhh_admin`, the detail page has no section form, remove button
      or upload input; after creating a candidate the URL ends in `/edit`; a core save keeps the URL
- [x] 6.4 Start `docker compose up` and run the candidate-profile, candidate-documents and
      candidate-tags-notes specs; then run the full `npm run e2e` and inspect the report
- [x] 6.5 Check the detail and edit pages at 390 px in `navigation-responsive.spec.ts` or the profile
      spec: no horizontal page scroll
- [x] 6.6 Restore seed data afterwards (retire candidates, tags and notes the specs created, as the
      existing specs do)

## 7. Security evidence (fail closed)

- [x] 7.1 Extend `tests/e2e/secure-access.spec.ts`: a `readonly` user is refused
      `/app/candidates/:id/edit` and sees no editing control on the detail page; a
      `manager_reader` sees download and preview but no upload _(the e2e harness has no `manager_reader` sign-in; covered instead by the `candidate-documents` unit spec: read-only keeps download and hides upload)_
- [x] 7.2 Confirm by test (existing or added in `tests/security/` or the API integration suite)
      that direct relation, note and document writes still answer 401 unauthenticated and 403
      unauthorized
- [x] 7.3 Run `npm run security:rls` and `npm run security:storage` and inspect the output

## 8. Lint and format

- [x] 8.1 Run `npm run lint` and confirm the three files are off `LEGACY_HARDCODED_COPY` with no
      hardcoded-copy errors
- [x] 8.2 Run `npm run format:check` and fix any formatting

## 9. Documentation

- [x] 9.1 Write `docs/ktl-22/release-notes.md`: what moved from the detail page to the edit page,
      the two save behaviours, the post-create landing, and that document upload in practice needs
      both `documents.upload` and `candidates.update`
- [x] 9.2 Note the upload/update pairing in the role administration docs (under `docs/`, where
      roles and permissions are described)
- [x] 9.3 Update `README.md` (Spanish) wherever it describes editing candidates from the detail page
- [x] 9.4 Run `openspec validate ktl-22-edit-page-owns-editing --strict` and fix any finding
