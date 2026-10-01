# KTL-35 — View a candidate's CV from the candidate tables

## [original]

Remove the boolean CV column from the candidates tables (candidates index, search results, position
candidadates/suggestions) and keep just a button "Ver" when the candidate actually has a CV. The
column would be the last one. When clicking this button the user should be able to see the CV of
the user without going away from the page. Ideally:

- On wide screens, the CV would appear with a transition on the RHS as in the candidate detail page
  but I wonder if the content on these pages is as stretch as in the candidates detail page to fit
  together.
- On less wide screens the CV would appear below the candidate as in an accordion rather than at
  the bottom of the page as on the candidates detail page

I wonder if keeping both behaviours is actuallly possible and what would happen if I resize my
windows beyond the breakpoint.

Viewing and expanding a CV would always collapse any other CV open and the same button that says
"Ver" would switch to "Ocultar" once clicked so users could go back to the previous step.

Is this feasible and good UX?

## [enhanced]

**Status:** Implemented on `feat/KTL-35`, pending review
**Depends on:** KTL-28 (split layout and CV preview), KTL-30 (position candidate tables), KTL-34
(CV tick column, which this ticket replaces)

### Summary

Four candidate tables show a «CV» column with a tick when the candidate has a primary CV (KTL-34).
The tick does not help anyone read the CV. To read it, users must open the candidate page, where
the CV preview sits beside or below the sections (KTL-28), and then come back to the list. On the
advanced search page, «Abrir CV» downloads the file instead.

This ticket removes the tick. The «CV» column moves to the end of each table and holds a «Ver»
button, shown only when the candidate's primary CV can be previewed. «Ver» shows the CV on the same
page:

- **Wide screens:** in a sticky panel on the right, beside the table, as on the candidate page.
- **Narrower screens:** in an expansion row directly under the candidate, like an accordion.

Only one CV is open per page. The button switches to «Ocultar» and the row is highlighted while
its CV is open.

### Changes agreed during implementation

These were agreed with the product owner while building the change. Where they contradict a line
further down this brief, they win. The change's specs and design
(`openspec/changes/ktl-35-candidate-table-cv-preview/`, design D12–D16) are the detailed record.

- **Candidate page uses the same rule.** Its CV preview appears only when the primary CV can be
  previewed (a clean PDF with its file). A pending primary appears once its scan reports it
  clean.
- **Download from the row.** The «CV» cell also holds a download button, left of «Ver», whenever
  the primary CV can be downloaded: clean, with its file, in any format. A `.docx` CV gets the
  download alone, so the earlier line "downloaded from the candidate page's «Documentos» panel" no
  longer applies. The API adds `primaryCvDownloadable` beside `primaryCvPreviewable`, with the same
  masking.
- **Icon buttons.**
  - The download is a disk.
  - «Ver»/«Ocultar» is an eye, crossed out when open, with an arrow towards where the CV opens:
    → / ← beside the table, ↓ / ↑ under the row.
  - Both keep their accessible names («Descargar el CV de …», «Ver el CV de …», «Ocultar el CV de
    …») as tooltips.
- **Motion.** Opening glides the panel in, or unfolds the row, in about 260 ms. Hiding plays the
  reverse in about 220 ms. Switching CVs and rows leaving the table stay instant. There is no
  motion for users who prefer reduced motion.
- **Click safety.** The «CV» cell and the actions cell ignore row clicks, so a click beside a
  button does nothing.
- **End-to-end test file.** The non-PDF case uses a `.txt` CV, because the e2e suite cannot build
  a valid `.docx` without a zip writer.

### User story

As an HR user screening a list of candidates, I want to read a candidate's CV without leaving the
list, so that I can compare candidates quickly and keep my place (page, sort order, filters and
scroll position).

### Tables in scope

| Table                                                | Component                                                     | Page                                              |
| ---------------------------------------------------- | ------------------------------------------------------------- | ------------------------------------------------- |
| Candidate list                                       | `features/candidates/components/candidate-table.tsx`          | `candidate-list-page.tsx` (`/app/candidates`)     |
| Advanced search results                              | `features/search/components/search-results.tsx`               | `advanced-search-page.tsx` (`/app/search`)        |
| «Candidatos de la posición»                          | `features/positions/components/position-candidates-panel.tsx` | `position-detail-page.tsx` (`/app/positions/:id`) |
| «Candidatos que encajan» (the matches of a position) | `features/search/components/search-results.tsx`               | `position-detail-page.tsx`                        |

### Decisions

| Topic                      | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CV column                  | The tick goes. The «CV» column becomes the **last** column of every table in scope, after the actions column where there is one, and holds only the button. The button is in the same place in every table, so switching candidates always happens at the right edge. The search page's empty actions column is dropped.                                                                                                                                                                                                                                                         |
| When «Ver» appears         | Only when the row's primary CV **can be previewed** (new API flag, below) and the actor holds `documents.download`. Otherwise the cell is empty: no disabled button. Without `documents.download` the whole column is not rendered.                                                                                                                                                                                                                                                                                                                                              |
| «Can be previewed» flag    | New boolean `primaryCvPreviewable` in the search projection and the position candidate projection. It is true when the candidate has a primary document whose scan state is `Clean`, whose content type is `application/pdf`, and whose binary exists (not a legacy record without a binary). This is the same rule as `CandidateDocumentResponse.From` («Available») plus the SPA's `isPreviewable`, expressed once and reused by both queries.                                                                                                                                 |
| Flag and permission        | The API sets the flag to `false` for an actor without `documents.download`: they cannot use it, so it is not exposed to them. The SPA still hides the button by permission, but that is not the control. The download endpoint keeps its own permission and scan checks.                                                                                                                                                                                                                                                                                                         |
| `primaryCvDocumentId`      | Removed from the search projection and the SPA models. Its only consumer, «Abrir CV», goes away, and the preview reads the candidate's document list itself.                                                                                                                                                                                                                                                                                                                                                                                                                     |
| «Abrir CV»                 | Removed from the search results, together with the `showOpenCv` prop and the «Tu rol no permite abrir CVs desde resultados.» notice. A primary CV that cannot be previewed (for example a `.docx`) is downloaded from the candidate page's «Documentos» panel.                                                                                                                                                                                                                                                                                                                   |
| Button                     | «Ver» when closed, «Ocultar» when open. It is a disclosure button with `aria-expanded` and `aria-controls`. Its accessible names are «Ver el CV de {{name}}» and «Ocultar el CV de {{name}}», and each contains the visible text.                                                                                                                                                                                                                                                                                                                                                |
| One open CV per page       | Opening a CV closes any other on the page. The state is page-wide, so on the position page it spans both tables. A candidate can be in both position tables, so the open CV is keyed by table + candidate: only the clicked row toggles and is highlighted.                                                                                                                                                                                                                                                                                                                      |
| Row highlight              | The row whose CV is open gets a highlighted state (`is-cv-open`) in Kepler tokens. It is visibly different from the hover state and applies in both placements. The button label and `aria-expanded` carry the state, not colour alone.                                                                                                                                                                                                                                                                                                                                          |
| Placement rule             | Side panel when the page's `.page-split` container is at least **1360px** wide (the KTL-28 threshold, roughly a 1400px window). Below that, an expansion row. The code measures the container, not the window.                                                                                                                                                                                                                                                                                                                                                                   |
| Wide placement             | Reuses the KTL-28 layout. The table region goes in `.page-split__main` and the panel in the sticky `.page-split__aside`. The existing `:has()` rule widens the shell to 1920px while the panel is shown. The panel **pushes** the table, never overlays it, so the «Ver» column stays visible and clickable. Filters, search criteria and the position description stay full width above the split. On the position page the split wraps both candidate tables.                                                                                                                  |
| Narrow placement           | A `<tr>` inserted directly after the candidate's row, with one cell spanning every column. It is not a clickable row. The table scrolls sideways inside `.table-wrap`, so the row's content is pinned to the wrapper's visible width, not the table's full width. The viewer keeps the preview's heights: 600px, or 70vh below 767px.                                                                                                                                                                                                                                            |
| Why the placement is in JS | On the candidate page the preview has one place in the DOM, and CSS alone moves it. Here the two places are structurally different (a row inside `<tbody>` and an aside next to the table), and CSS cannot move an element out of a table. So the placement is chosen in code from a `ResizeObserver` on the container. This is a deliberate exception to the "one DOM tree, CSS only" rule (AGENTS.md, KTL-28), to be recorded in the change's `design.md`. It cannot oscillate: widening the shell only makes the container wider, and 1360px is below the default 1440px cap. |
| Crossing the breakpoint    | The CV stays open for the same candidate, the button still reads «Ocultar», and the viewer moves to the other placement. The downloaded content is reused, so there is no second request and no second audit entry. The PDF viewer goes back to its first page. Moving into the table scrolls the row into view. If focus was inside the moved panel, it goes to the row's «Ocultar».                                                                                                                                                                                            |
| Content cache              | Only the open CV's content is kept in memory, at page level, so it survives the move. It is released, and its object URL revoked, when the CV closes, another opens, or the page is left.                                                                                                                                                                                                                                                                                                                                                                                        |
| Panel header               | The same in both placements: heading «CV de {{name}}», a link «Abrir ficha» to the candidate page, and an «Ocultar» button. The existing preview body follows unchanged: document picker when there are several documents, availability and failure states, viewer, and download fallback.                                                                                                                                                                                                                                                                                       |
| Closing                    | «Ocultar» on the row or the panel, opening another CV, or leaving the page. The CV also closes when its row leaves the table: page, sort or filter change, search re-run, «Quitar de la posición», or a deactivation that hides the row.                                                                                                                                                                                                                                                                                                                                         |
| Phones                     | The viewer always attempts to display the PDF inline, with no device or user-agent detection. The fallback message with «Descargar» appears only when the browser itself cannot render the PDF.                                                                                                                                                                                                                                                                                                                                                                                  |
| Motion                     | The side panel slides in and the expansion row opens with a short transition (≤200ms). Neither animates under `prefers-reduced-motion`.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| «Ver» and the table rules  | `data-tables` "Rows open their record" forbids «Ver» buttons that only repeat the row's navigation. This button shows a preview in place and never navigates, so the requirement text gets a clarifying sentence rather than an exception.                                                                                                                                                                                                                                                                                                                                       |

### Existing behaviour and contract

- `shared/components/cv-indicator.tsx` renders the tick (KTL-34). It is used by the three table
  components above.
- `features/candidates/components/candidate-cv-preview.tsx` takes a full `Candidate`, but uses only
  its id and its documents as a starting point. It then:
  - fetches the document list (`candidates.read`) and follows pending scans;
  - fetches the content through the audited, clean-scan-gated download endpoint
    (`documents.download`);
  - caches the content per component instance;
  - renders nothing without the permission or without documents.

  The candidate detail and edit pages use it (KTL-28).

- `frontend/src/styles.css` `.page-split` (container query at 1360px) and
  `app-layout.css` `.shell main:has(.page-split__aside .cv-preview)` (1920px shell).
- `shared/components/row-link.ts`: clicks on buttons, links and cells marked
  `data-row-link-ignore` never navigate.
- The candidate list and the advanced search both call `POST /api/candidates/search`. The search
  projection (`SearchResult`, built in `Infrastructure/Persistence/CandidateSearchQuery.cs`)
  returns:
  - `hasPrimaryCv`: a primary document exists, in any scan state or format;
  - `primaryCvDocumentId`.
- `GET /api/positions/{id}/candidates` (`PositionRepository.cs`, `PositionCandidateItem`) returns
  `hasPrimaryCv` only.
- Availability rule: `Application/Features/Documents/DocumentContract.cs`
  (`CandidateDocumentResponse.From`); SPA mirror `isPreviewable` in
  `candidate-cv-preview.logic.ts`.
- The CSV export's `cv_disponible` column and the has-CV search filter use `hasPrimaryCv` and do
  not change.

### API and data changes

- `Application/Features/Search/SearchContract.cs`: `SearchResult` gains `bool PrimaryCvPreviewable`
  and loses `Guid? PrimaryCvDocumentId`. `CandidateSearchQuery.cs` computes the flag in the same
  primary-document lookup. `SearchCandidates.cs` masks it to `false` without
  `Permissions.DocumentsDownload`.
- `Application/Abstractions/Persistence/IPositionRepository.cs` `PositionCandidateItem` and
  `Application/Features/Positions/PositionContract.cs` `PositionCandidateResponse` gain
  `PrimaryCvPreviewable`. `PositionRepository.cs` computes it and `ListPositionCandidates.cs` masks
  it.
- One shared previewability rule for both queries, consistent with `CandidateDocumentResponse.From`.
- No new endpoint, permission, table, migration or grant. The queries already read the documents
  table as `ktl_runtime`.

### Acceptance criteria

1. **Column.** Given an actor with `documents.download`, when any table in scope is shown, then it
   has no tick column and its last column is «CV». «Ver» appears only on rows whose primary CV is a
   clean PDF with a binary.
2. **Not previewable.** Given candidates whose primary CV is a `.docx`, pending, refused, failed or
   a legacy record without a binary, when a table lists them, then their «CV» cell is empty.
3. **No permission.** Given an actor without `documents.download`, when any table in scope is
   shown, then there is no «CV» column. Both APIs return `primaryCvPreviewable: false` for every
   row, and no response contains `primaryCvDocumentId`.
4. **Wide open.** Given a 1920×1080 window, when the user activates «Ver» on a row, then:
   - the CV appears in a panel to the right of the table, headed «CV de {{name}}»;
   - the row is highlighted and its button reads «Ocultar» with `aria-expanded="true"`;
   - the table stays visible and usable, and the URL does not change.
5. **Switch.** Given an open CV, when the user activates «Ver» on another row, then the panel shows
   the new candidate's CV. The previous row returns to «Ver» without highlight, and the page holds
   one viewer.
6. **Hide.** Given an open CV, when the user activates «Ocultar» on the row or on the panel, then
   the panel closes, the page returns to one column at the default width, and focus is on the
   row's «Ver».
7. **Narrow open.** Given a 1366×768 window, when the user activates «Ver», then the CV appears in
   a row directly below that candidate and nothing is added at the bottom of the page.
8. **Phone width.** Given a 390px-wide window, when a CV is open, then the page does not scroll
   horizontally, and the viewer fits the screen width even when the table scrolls sideways.
9. **Resize.** Given a CV open at 1920px, when the window is narrowed to 1280px, then:
   - the CV is shown under its row, and the row is in view;
   - the button still reads «Ocultar»;
   - no second content request is made.

   Widening again moves it back to the panel.

10. **Position page.** Given a CV open in «Candidatos de la posición», when the user activates «Ver»
    in «Candidatos que encajan», then the first CV closes. When the same candidate is in both
    tables, only the clicked row toggles and is highlighted.
11. **Row leaves.** Given an open CV, when the user changes page, sort order or filters, re-runs the
    search, or removes that candidate from the position, then the CV closes.
12. **Stale flag.** Given a primary CV that was replaced or removed after the table loaded, when
    «Ver» is activated, then the panel shows the existing availability or failure message. The
    panel is never empty, and no content is fetched for a document that is not clean.
13. **Navigation intact.** A click on a row outside its controls still opens the candidate. A click
    on «Ver», «Ocultar», or anywhere inside the panel or the expansion row never navigates.
14. **Phones display.** Given a 390px-wide browser that renders PDFs inline, when a CV is opened,
    then the PDF is displayed in the expansion row. Where the browser cannot render it, the row
    shows the fallback message with «Descargar».
15. **Audit.** Each first display of a CV records one download audit entry. Moving the viewer
    across the breakpoint records none.
16. **Candidate pages unchanged.** The KTL-28 acceptance criteria for the candidate detail and edit
    pages still pass.

### Implementation scope

**Backend:** see "API and data changes".

**Frontend** (paths under `frontend/src/app/`):

- A page-level row CV preview under `features/candidates/components/` (e.g. `row-cv-preview.tsx`,
  `.logic.ts` and `.css`), with:
  - a provider/hook holding the open key (table + candidate), the placement (from a
    `ResizeObserver` on the `.page-split` element, threshold shared with the CSS) and the
    single-entry content cache;
  - the cell button, the expansion row and the side panel;
  - pure helpers (placement from width, open-key handling) in the `.logic.ts` file.

  The tables read it from context, and each table passes its own table id.

- `candidate-cv-preview.tsx`:
  - accept a `candidateId` with optional initial documents, an optional header and an optional
    external content cache;
  - the detail and edit pages keep passing what they have today;
  - opened from a row, it shows a message instead of rendering nothing.
- `candidate-table.tsx`, `search-results.tsx`, `position-candidates-panel.tsx`:
  - remove `CvIndicator` and add the last «CV» column, row highlight and expansion row;
  - update `colSpan` values;
  - in `search-results.tsx`, remove `openCv`, `showOpenCv` and the empty actions column.
- `candidate-list-page.tsx`, `advanced-search-page.tsx`, `position-detail-page.tsx`: wrap the table
  regions in `.page-split` with the aside and provide the page-level context.
- Models: `CandidateListItem`, `SearchResult` and `PositionCandidate` gain `primaryCvPreviewable`.
  `primaryCvDocumentId` is dropped.
- Delete `shared/components/cv-indicator.tsx` and `.css`, and `tests/unit/cv-indicator.spec.tsx`.
- `assets/i18n/es.json`:
  - Add under `cvPanel.*`: «Ver», «Ocultar», «Ver el CV de {{name}}», «Ocultar el CV de {{name}}»,
    «CV de {{name}}» and «Abrir ficha».
  - Remove `cvIndicator.*`, `search.results.openCv`, `search.results.cvUnavailable`,
    `search.results.cvDownloadFailed` and `search.results.cvNotAllowed`.
  - The existing `*.column.cv` keys stay.
- Keep every existing `name=` and `data-testid`. Add test ids for the button, the expansion row and
  the panel.

### Verification

- **Backend (xUnit):**
  - `SearchApiTests` and `PositionCandidateApiTests` check the flag for:
    - a clean PDF primary: true;
    - `.docx`, pending, refused, scan-failed and legacy-without-binary primaries: false;
    - no primary: false;
    - an actor without `documents.download`: false;
    - `primaryCvDocumentId` absent from responses.
  - Unauthenticated and unauthorized callers are still refused.
  - Handler unit tests cover the masking. `SearchQueryPlanTests` is updated if the plan changes.
- **Frontend unit (Vitest):**
  - `candidate-list-page.spec.tsx`, `search-results.spec.tsx`, `position-pages.spec.tsx` and
    `candidate-cv-preview.spec.tsx`.
  - A new row CV preview spec for placement, one open CV, keying by table, closing when the row
    leaves, and focus handoff. jsdom has no layout, so it stubs `ResizeObserver`.
- **E2e (Playwright):**
  - A new `candidate-cv-row-preview.spec.ts` at 1920×1080, 1366×768 and 390px, covering criteria
    4–13.
  - Update `advanced-search.spec.ts`, `position-candidates.spec.ts` and
    `candidate-list-operations.spec.ts` for the removed tick and «Abrir CV».
  - Records created carry a `Date.now()` marker. Selectors use roles and test ids, not Spanish text.
- **Gates:** `npm test`, `npm run test:backend`, `npm run lint`, `npm run format:check`,
  `npm run build:all`.

### Documentation

Delta specs:

- `data-tables`:
  - replace "CV presence is shown as a tick" with a requirement for the row CV preview;
  - clarify the «Ver» sentence in "Rows open their record".
- `candidate-search` "Minimal search result projection": add the flag, remove the primary document
  identifier and its scenario.
- `position-candidates`:
  - "Position candidate API": add the flag to the item fields;
  - "Position page lists its candidates": the CV column holds «Ver».
- `private-document-storage` "Controlled inline preview": add a scenario for a preview opened from a
  table row. It goes through the same gate, and the flag grants nothing.

`README.md` only if it describes these tables or «Abrir CV».

### Non-functional requirements

- **Security and personal data:**
  - No new delivery path: content comes only from the existing audited, clean-scan-gated download
    response.
  - The flag is a derived boolean that reveals no filename, content type or scan code, and it is
    masked without `documents.download`.
  - Only the open CV is held in memory, and it is released on close.
  - Nothing new is logged.
- **Performance:**
  - The flag is computed in the existing correlated lookup of the primary document, which the
    partial unique index `UX_CND_Documents_CandidateId_Primary` serves. There are no per-row
    requests.
  - Opening a CV costs the same two requests as on the candidate page (document list, content).
- **Accessibility:**
  - Disclosure button semantics.
  - The expansion row follows its row in reading order.
  - The side panel is a labelled region after the table.
  - Focus stays on the button when a CV opens and returns to it when the CV closes or moves.
- **Responsive:** the placement follows the container width. This is the only layout decision made
  in code, and the design records it.
- **Browser support:** `ResizeObserver`, `:has()` and container queries are available in every
  evergreen browser the app targets.

### Out of scope

- An in-page PDF renderer (e.g. pdf.js) to display PDFs where the browser has no inline viewer,
  notably Chrome on Android. It would add a runtime dependency with a documented reason and change
  the "natively renderable" clause of `private-document-storage`. That is a separate ticket if the
  fallback proves insufficient.
- Previewing formats other than PDF, and downloading a non-PDF primary CV from the tables.
- Keyboard shortcuts to step to the next or previous CV, and a resizable panel.
- Other tables: a candidate's positions, the positions list and presets.
- The CSV export and the has-CV search filter.
