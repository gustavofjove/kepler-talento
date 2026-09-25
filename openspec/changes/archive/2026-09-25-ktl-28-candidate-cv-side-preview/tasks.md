## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-28` from `main`, named after the ticket file `openspec/KTL-28.md`. (Proposal: traceable KTL-28 delivery.)

## 1. Shell width

- [x] 1.1 In `app-layout.css`, declare `--shell-max-width: 1440px` on `.shell`, use it for `.shell main`, and add the `.shell main:has(.page-split__aside .cv-preview)` override to 1920px (design D1). (Spec: shell content width; preview widens the content area.)

## 2. Split layout

- [x] 2.1 Add the `.page-split` layout to `styles.css`:
  - `.page-split` is the `page-split` inline-size container.
  - `.page-split__layout` is a one-column grid that becomes `minmax(0, 1fr) minmax(560px, 45%)` at `@container page-split (min-width: 1360px)`, and only when `:has(> .page-split__aside .cv-preview)`.
  - `.page-split__main` is the `page-split-main` container.
  - `.page-split__aside` is sticky and viewport-tall when split, and `display: none` when `:empty`.
  - The scoped `@container page-split-main (max-width: 620px)` rule collapses `.grid.two`.

  (Design D2–D5. Spec: preview beside the sections; field groups follow the column; no preview leaves one column.)

- [x] 2.2 In `candidate-cv-preview.tsx`, drop `span-all`. In its `.css`, make the panel a flex column whose viewer fills the height inside the aside, and keep the 600px and 70vh minimums when stacked (design D5). (Spec: preview stays in view.)
- [x] 2.3 Wrap the section grid of `candidate-detail-page.tsx` in `.page-split` > `.page-split__layout` > `.page-split__main`, and move `<CandidateCvPreview>` into `.page-split__aside`. (Spec: detail page layout; preview beside the sections.)
- [x] 2.4 Do the same in `candidate-edit-page.tsx` around the main-data panel and the section grid. Render `<CandidateCvPreview candidate={candidate} />` in the aside only when `candidate` exists (design D6). (Spec: preview on the edit page; new candidate page has no preview.)

## 3. Unit tests

- [x] 3.1 Review and update `candidate-detail-page.spec.tsx` and `candidate-cv-preview` specs for the new structure.
- [x] 3.2 In `candidate-edit-page.spec.tsx`, add cases for:
  - The preview in the aside with `documents.download`.
  - No preview, and no content request, without it.
  - No preview on `/new`.

  (Spec: preview on the edit page; no preview leaves one column; new candidate page has no preview.)

- [x] 3.3 Run `npm test` from `frontend/` and inspect the results.

## 4. Browser journeys

- [x] 4.1 Extend `tests/e2e/candidate-profile.spec.ts` with bounding-box checks at 1920×1080:
  - The preview is right of Datos principales on the detail and edit pages.
  - It stays in the viewport after scrolling to Documentos.
  - The two-column main-data form keeps two columns.
  - A picker popover opens inside the viewport.

  Add checks at 1366×768 (the preview is below Documentos) and at 390px (no horizontal scroll), and one for a page without a preview (`main` is at most 1440px at 1920px). Selectors use test ids and roles. (Spec: every added scenario.)

- [x] 4.2 With `docker compose up` running, run from `frontend/`: `npx playwright test tests/e2e/candidate-profile.spec.ts tests/e2e/navigation-responsive.spec.ts tests/e2e/candidate-api-cutover.spec.ts`. Inspect the results, fix failures, and restore any seed data the journeys changed.
- [x] 4.3 Look at the candidate list, search, positions and admin pages at 1920px on :4300 to confirm they read well at the wider shell width. (Design, Risks.)

## 5. Boundaries

- [x] 5.1 Confirm with `git diff --stat main` that no file under `backend/` or `supabase/` changed and no dependency was added. Confirm that the existing preview permission tests (`documents.download` fail closed) still pass in the `npm test` run. (Proposal: no data path or permission change.)

## 6. Document and finish

- [x] 6.1 Add `docs/ktl-28/release-notes.md`, covering the shell width, the side-by-side preview, the edit-page preview and the container-driven field grids. Update `README.md` (in Spanish) if it describes the candidate page layout. (Proposal: docs.)
- [x] 6.2 Run `npm run build:all`, `npm run lint` and `npm run format:check` from `frontend/`. Inspect the outputs, and check tasks only after their commands pass. (Repository delivery gate.)
