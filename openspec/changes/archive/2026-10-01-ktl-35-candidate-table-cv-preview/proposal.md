## Why

The candidate list, the advanced search results and both candidate tables on a position page show
a «CV» tick (KTL-34) that tells HR a CV exists but does not let them read it. Screening a list
means opening each candidate page to see the CV preview (KTL-28) and coming back, which loses the
reader's place and slows every comparison. Source brief: `openspec/KTL-35.md`.

## What Changes

- **«Ver» replaces the tick.** In the four tables in scope, the «CV» column moves to the last
  position and holds a «Ver» button. The button appears only when the row's primary CV can be
  previewed and the actor holds `documents.download`. Without that permission the column is not
  rendered.
- **The CV opens on the same page.**
  - When the page's content area is at least 1360px wide, the CV opens in a sticky panel beside
    the table, reusing the KTL-28 split layout and 1920px shell.
  - Below that width, it opens in an expansion row directly under the candidate.
  - One CV is open per page, across both position tables. The button toggles «Ver»/«Ocultar»,
    and the open row is highlighted.
  - Resizing across the breakpoint keeps the CV open and moves it without downloading it again.
- **The CV closes** when its row leaves the table (page, sort, filter, re-run, removal from the
  position), when another opens, or on «Ocultar».
- **New API flag `primaryCvPreviewable`** on search items (`POST /api/candidates/search`, used by
  the candidate list, the advanced search and position matches) and on position candidate items
  (`GET /api/positions/{id}/candidates`).
  - It is true only for a primary document that is clean, is a PDF and has a binary.
  - It is always false for an actor without `documents.download`.
- **BREAKING (API):** `primaryCvDocumentId` is removed from search items. Its only consumer,
  «Abrir CV», is removed.
- **BREAKING (UI):** «Abrir CV» is removed from the advanced search results, together with its
  permission notice. The row's download button replaces it in all four tables.
- **Download from the row, and icon buttons.** (Added during implementation at the product owner's
  request.) The «CV» cell also offers a disk button, left of «Ver», whenever the primary CV can be
  downloaded: clean, with its file, in any format. A `.docx` CV gets the download alone, which
  restores what «Abrir CV» offered. A second flag, `primaryCvDownloadable`, follows the same
  masking. Both buttons are icons with accessible names and tooltips. «Ver»/«Ocultar» is an eye,
  crossed out when open, with an arrow towards where the CV opens.
- **The candidate page follows the same rule.** It shows its CV preview only when the primary CV
  can be previewed (the rule behind the flag), instead of whenever the candidate has documents. A
  `.docx`, refused or non-primary-only candidate gets a one-column page and downloads from
  «Documentos». A pending primary CV shows the preview once the scan reports it clean. (Added
  during implementation at the product owner's request.)
- The shared `CvIndicator` component, its CSS, spec and `cvIndicator.*` copy are deleted. The
  `CandidateCvPreview` component is generalised to take a candidate id, so the candidate page and
  the tables share it.
- New Spanish copy under `cvPanel.*` in `es.json`. The removed actions' keys are deleted.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `data-tables`:
  - the tick requirement is replaced by a row CV preview requirement (button, placement, one open
    CV, highlight, closing, resize);
  - "Rows open their record" clarifies that a «Ver» which previews in place is not a navigation
    duplicate.
- `candidate-search`: the minimal search projection gains the previewable flag, masked without
  `documents.download`, and loses the primary document identifier.
- `position-candidates`:
  - the position candidate list item gains the same flag;
  - the position page's tables show «Ver» instead of CV availability.
- `position-management`: match rows and advanced search results offer «Ver» instead of «Abrir CV».
- `candidate-profile-pages`: the candidate page's CV preview is shown only when the primary CV can
  be previewed.
- `primary-navigation`: the shell also widens while a table CV panel is shown beside a table.
- `private-document-storage`: the controlled inline preview also covers previews opened from a
  table row. The flag grants nothing.

## Impact

- **Personal data (principle 1):**
  - CV content reaches the browser only through the existing audited, permission-checked,
    clean-scan-gated download response. There is no new delivery path, URL or storage change.
  - The new flag is a derived boolean. It reveals no filename, content type, scan state or failure
    code, and is `false` for actors who cannot download.
  - The primary document identifier stops leaving the API in search responses.
  - The browser keeps only the open CV's content in memory and releases it on close.
  - Nothing new is logged.
- **Authorization, least privilege, storage (principle 3):**
  - No new endpoint, permission, role, grant, RLS policy, table or migration.
  - Search and position candidate reads keep their fail-closed checks.
  - The flag is masked in the handlers, and the download endpoint keeps its own permission and scan
    gates.
- **Actors:** HR users with `candidates.read` who screen lists. Those who also hold
  `documents.download` get «Ver».
- **Key entities:** `CandidateDocument` (primary flag, scan state, content type, binary
  presence), `SearchResult`, `PositionCandidateItem`.
- **Assumptions:**
  - PDF is the only natively previewable format.
  - The KTL-28 threshold (1360px of content) suits tables too: about 750px of table beside a 630px
    panel at a 1440px window.
- **Edge cases:**
  - the same candidate in both position tables;
  - a flag gone stale after the list loaded (the panel shows the availability message and never an
    empty panel);
  - crossing the breakpoint with focus inside the panel;
  - phones whose browser cannot render a PDF inline (fallback with «Descargar»);
  - a row removed while its CV is open.
- **Success criteria:** the 16 acceptance criteria in `openspec/KTL-35.md` pass through xUnit,
  Vitest and Playwright evidence, including fail-closed checks that an actor without
  `documents.download` gets no flag and no button.
- **Code:**
  - `backend/Application/Features/Search`, `backend/Application/Features/Positions`,
    `backend/Application/Abstractions/Persistence/IPositionRepository.cs`,
    `backend/Infrastructure/Persistence` (`CandidateSearchQuery.cs`, `PositionRepository.cs`), and
    a shared previewability rule;
  - `frontend/src/app/features/candidates` (preview, table, list page, new row CV preview),
    `features/search` (results, page), `features/positions` (panel, detail page),
    `shared/components/cv-indicator.*` (deleted), `styles.css`, `app-layout.css`,
    `assets/i18n/es.json`.
- **Tests:** backend search and position candidate API/handler tests, and frontend unit specs for
  the three tables, the preview and the new controller. A new e2e spec, with updates to the search,
  position and candidate list e2e specs.
- **Dependencies:** none new. `ResizeObserver` is a platform API.
