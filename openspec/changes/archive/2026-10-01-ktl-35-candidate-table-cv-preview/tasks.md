## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-35` from `main`, named after the ticket file `openspec/KTL-35.md`. (Proposal: traceable KTL-35 delivery.)

## 1. Backend: previewability flag

- [x] 1.1 Add the static `CandidateDocument.IsPreviewable` expression and its compiled predicate to `Domain/Documents/CandidateDocument.cs`. The rule is: clean scan, `application/pdf`, and not a legacy record without a binary. (Design D1. Spec: candidate-search minimal projection.)
- [x] 1.2 In `Application/Features/Search/SearchContract.cs`, replace `Guid? PrimaryCvDocumentId` with `bool PrimaryCvPreviewable` on `CandidateSearchItem`, and update its remarks. In `Infrastructure/Persistence/CandidateSearchQuery.cs` `Project`, compute the flag with the primary-document subquery filtered by `IsPreviewable`, and drop the id subquery. (Design D1, D3.)
- [x] 1.3 Add `PrimaryCvPreviewable` to `PositionCandidateItem` (`IPositionRepository.cs`), `PositionCandidateResponse` and `ToResponse` (`PositionContract.cs`). Compute it in `PositionRepository.cs`. (Design D1. Spec: position candidate API.)
- [x] 1.4 Mask the flag to `false` when the actor lacks `Permissions.DocumentsDownload`, after the existing guards, in `SearchCandidatesHandler` and `ListPositionCandidatesHandler`. (Design D2. Spec: actor cannot download documents.)

## 2. Backend tests

- [x] 2.1 Review and update existing backend tests that reference `PrimaryCvDocumentId` or build `CandidateSearchItem` / `PositionCandidateItem`: `SearchApiTests`, `SearchHandlerTests`, `PositionCandidateHandlerTests`, `PositionCandidateApiTests`, `EncryptedSearchParityTests`, and any fixture builders.
- [x] 2.2 Add unit tests for `CandidateDocument.IsPreviewable`, covering clean PDF, `.docx`, each non-clean scan state and legacy-without-binary. Add a test pinning it to `CandidateDocumentResponse.From` («Available» and PDF ⇔ previewable). (Design D1.)
- [x] 2.3 Add handler unit tests: with `documents.download` the repository's flag passes through; without it every item's flag is `false`. The existing guards still run first for unauthenticated and unauthorized actors. (Design D2.)
- [x] 2.4 Add integration tests (Testcontainers PostgreSQL) for search and position candidates. They cover:
  - the flag matrix (clean PDF primary true; `.docx`, pending, refused, scan-failed, legacy-without-binary and no-primary false);
  - masking for an actor without `documents.download`;
  - `primaryCvDocumentId` absent from the JSON;
  - unauthenticated and unauthorized callers still refused (security evidence).
- [x] 2.5 Check `SearchQueryPlanTests` and `EncryptedSearchPerformanceTests` against the changed projection, and update plan expectations only if the plan legitimately changed. (Design D1 query cost.)
- [x] 2.6 Run `npm run test:backend` from `frontend/` with Docker running. Inspect the output until it is green, and fix failures at their cause.

## 3. Frontend: models, API and shared preview

- [x] 3.1 In the models, add `primaryCvPreviewable: boolean` to `CandidateListItem` (`candidate.models.ts`), `SearchResult` (`search.models.ts`) and `PositionCandidate` (`position.models.ts`), and remove `primaryCvDocumentId`. Fix every compile error and test double that follows. (Design D3.)
- [x] 3.2 Generalise `CandidateCvPreview` (design D7):
  - props `candidateId`, `initialDocuments?`, `header?`, `contentCache?` and `emptyMessage?`;
  - keep the internal cache when no `contentCache` is passed;
  - key the reset effect on `candidateId`.

  Update the call site in `candidate-detail-page.tsx` (the single candidate page since KTL-29), keeping its test ids and names.

- [x] 3.2a Add the `requirePreviewablePrimary` gate (design D12). With it, the preview renders nothing and requests no content unless the primary document in the live list is previewable, and it appears once a pending primary settles clean. Set it on the candidate page. (Spec: candidate-profile-pages CV preview beside the candidate sections.)

- [x] 3.3 Add `features/candidates/components/row-cv-preview/row-cv-preview.logic.ts` (design D4, D5, D8). It holds:
  - `CV_SPLIT_MIN_WIDTH = 1360`, with a comment pairing it with the `@container page-split` rule in `styles.css`;
  - `placementFor`;
  - `openKey`;
  - `isOpenRowGone`.
- [x] 3.4 Add `row-cv-preview.tsx` (design D4, D6, D8, D9):
  - `RowCvPreviewProvider`: open state, a `ResizeObserver` placement on the `.page-split` ref, the single-entry content cache, `reportVisible`, and the focus and scroll handoff;
  - `useRowCvPreview(tableId)`;
  - `RowCvButton`: «Ver»/«Ocultar», `aria-expanded`, `aria-controls` and the named `aria-label`;
  - `RowCvInlineRow`: a `<tr>` with `colSpan`, not a row link;
  - `RowCvSidePanel`.

  Both of the last two share the header «CV de {{name}}», «Abrir ficha» and «Ocultar», and render `CandidateCvPreview` with `emptyMessage` and the provider's cache.

- [x] 3.5 Add `row-cv-preview.css`, using Kepler tokens only and no inline styles:
  - the `.is-cv-open` row highlight, distinct from the hover style;
  - the `.cv-row` expansion content pinned to the `.table-wrap` visible width (inline-size container, `100cqi`, sticky left);
  - the side panel slide-in and row open transition under `prefers-reduced-motion: no-preference` only.
- [x] 3.6 Update `assets/i18n/es.json`, and `en.json` where possible (design D11):
  - add `cvPanel.show`, `cvPanel.hide`, `cvPanel.showLabel`, `cvPanel.hideLabel`, `cvPanel.title` and `cvPanel.openProfile`;
  - remove `cvIndicator.*`, `search.results.openCv`, `search.results.cvUnavailable`, `search.results.cvDownloadFailed` and `search.results.cvNotAllowed`.

## 4. Frontend: tables and pages

- [x] 4.1 In `candidate-table.tsx`:
  - remove `CvIndicator`, and add the last «CV» column when `usePermission('documents.download')`;
  - render `RowCvButton` for `primaryCvPreviewable` rows, the `.is-cv-open` highlight, and `RowCvInlineRow` after the open row in inline placement;
  - `reportVisible` the rendered ids, and derive the empty-state `colSpan`.

  (Spec: data-tables CV preview from a table row.)

- [x] 4.2 In `search-results.tsx`, make the same changes with a `tableId` prop. Also remove `openCv`, `canOpenCv`, `showOpenCv` and the `cvNotAllowed` paragraph, and render the actions column only when `renderRowAction` is given. (Design D10. Spec: position-management live matching.)
- [x] 4.3 Make the same changes in `position-candidates-panel.tsx`, with the «CV» column after the actions column. (Spec: position page lists its candidates.)
- [x] 4.4 In `candidate-list-page.tsx`, wrap the table region in `RowCvPreviewProvider` and `.page-split` / `__main` / `__aside`, keeping the filters and bulk actions above the split. In `advanced-search-page.tsx`, do the same for the results panel. In `position-detail-page.tsx`, wrap both candidate panels in one provider and split, passing distinct table ids, and drop `showOpenCv={false}`. (Design D4. Spec: primary-navigation shell width.)
- [x] 4.5 Delete `shared/components/cv-indicator.tsx` and `cv-indicator.css`, and `tests/unit/cv-indicator.spec.tsx`. Confirm that no import or key references remain.

## 5. Frontend unit tests

- [x] 5.1 Review and update existing affected unit specs for the removed tick, the removed «Abrir CV» and `primaryCvDocumentId`, and the new column:
  - `candidate-list-page.spec.tsx`, `search-results.spec.tsx`, `advanced-search-page.spec.tsx`, `position-pages.spec.tsx`;
  - `candidate-cv-preview.spec.tsx` and `candidate-cv-preview.logic.spec.ts`;
  - the candidate page spec (`candidate-detail-page.spec.tsx`): a clean PDF primary shows the preview; a clean `.docx` primary, a refused primary and non-primary-only documents show none and request no content; a pending primary shows it once it settles clean.
- [x] 5.2 Add `tests/unit/row-cv-preview.logic.spec.ts` for placement at 1359/1360, open keys and the row-gone detection.
- [x] 5.3 Add `tests/unit/row-cv-preview.spec.tsx`. It stubs `ResizeObserver` and covers:
  - «Ver»/«Ocultar» toggling and `aria-expanded`, and the named labels;
  - one open CV across two tables, and the same candidate in two tables;
  - highlight on the open row only;
  - the inline row after the open row, and the side panel in the aside;
  - the switch between placements without a second `openPreview` call;
  - close on row gone, with focus back on the button;
  - no column without `documents.download`;
  - the stale flag showing `emptyMessage`, not an empty area.
- [x] 5.4 Run `npm test` from `frontend/` and inspect the output until it is green.

## 5b. Row download and icon buttons (added during implementation)

- [x] 5b.1 Backend: `CandidateDocument.IsDownloadable` and `primaryCvDownloadable` in the search and position candidate projections, masked without `documents.download`; unit, handler and integration tests extended (`DocumentPreviewabilityTests`, `SearchHandlerTests`, `PositionCandidateHandlerTests`, `SearchApiTests`, `PositionCandidateApiTests`). (Design D13.)
- [x] 5b.2 Frontend: `primaryCvDownloadable` in the models and doubles; `RowCvDownloadButton` (disk) and `RowCvActions` in the three tables; icon-only eye toggle with the placement arrow; `cvPanel.download*` copy; specs for the arrows, the download and the stale-download warning. (Design D13, D14. Spec: data-tables CV preview from a table row.)

## 6. End-to-end

- [x] 6.1 Review `tests/e2e/advanced-search.spec.ts`, `position-candidates.spec.ts` and `candidate-list-operations.spec.ts` for the removed tick and «Abrir CV». Reviewed: none asserts the tick, «Abrir CV» or column positions, so no edit was needed; the 6.3 run confirms it.
- [x] 6.2 Add `tests/e2e/candidate-cv-row-preview.spec.ts`. It uses a candidate with a clean PDF primary CV and one with a `.txt` primary (a non-PDF the e2e suite can build without a zip writer), both named with a `Date.now()` marker. It covers:
  - at 1920×1080: open beside the table, switch, hide (shell back to ≤1440px), no URL change, row click still navigates;
  - at 1366×768: an inline row under the candidate;
  - at 390px: no horizontal page scroll, and the viewer within the viewport width;
  - resize 1920 → 1280 → 1920 with the CV kept open and a single content request;
  - the position page with both tables;
  - no «Ver» but a download on the `.txt` row, which downloads the file;
  - the candidate page shows no preview for the `.txt` candidate.
- [x] 6.3 Run `docker compose up --build` from the repository root, then run the new and updated Playwright specs from `frontend/` (`npx playwright test tests/e2e/candidate-cv-row-preview.spec.ts` and the three updated specs). Inspect the results and traces, and confirm the global teardown purged the marked records.
- [x] 6.4 Run `npx playwright test tests/e2e/candidate-profile.spec.ts tests/e2e/navigation-responsive.spec.ts` to confirm the KTL-28 candidate page layout and the shell are unchanged.

## 7. Documentation and gates

- [x] 7.1 Check `README.md` and `docs/` for mentions of «Abrir CV», the CV tick or `primaryCvDocumentId`, and update them in their language (README in Spanish).
- [x] 7.2 Run `npm run lint`, `npm run format:check` and `npm run build:all` from `frontend/`, and inspect the output until everything is clean.
- [x] 7.3 Run `openspec validate ktl-35-candidate-table-cv-preview --strict` and fix any finding.
