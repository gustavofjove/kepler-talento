## Why

HR staff read a candidate's CV while they check or transcribe the candidate's data. Today the CV
preview is a full-width panel below every section on the detail page, and the edit page has none,
so users scroll back and forth between the data and the document. The shell also caps content at
1136px on every screen. On the 1920px office monitors most users have, that leaves about 780px
unused, and it is too narrow to fit the data and a readable PDF side by side.

## What Changes

- **Wider shell.** The content area of every page inside the shell grows from 1136px to 1396px
  (`main` max width 1440px).
- **CV beside the data.** On the candidate detail page and on the edit page of an existing
  candidate, the CV preview becomes a right-hand column next to the sections whenever the content
  area is at least 1360px wide. On those pages the shell may widen to 1920px.
  - The column stays in view (sticky) while the sections scroll.
  - Below that width, the preview stays stacked after the last section, as today.
- **CV preview on the edit page.** The edit page of an existing candidate gains the same preview,
  under the same `documents.download` rule as the detail page. The new-candidate page is
  unchanged.
- **Form grids follow their column.** Two-column field grids inside the content column (main data,
  Educación, Experiencia, Documentos) drop to one column when the column is narrow, whatever the
  viewport width.
- **No preview, no split.** When the preview is not shown (no permission, no documents, nothing to
  select), the page keeps one column at the default width.
- The section order from KTL-27 is unchanged. Every `data-testid` and `name=` attribute is kept.
  There is no new copy.

**Actors.** Recruiters (`rrhh_user`, `rrhh_admin`) viewing and editing candidates, and any reader
of the candidate detail page who holds `documents.download`.

**Key entities.** None new. Candidate documents and their preview are presented in a different
place only.

**Assumptions.**

- Users work mostly at 1920px (office monitors), 1536px (laptops at 125% scaling) and 1366px wide.
- A PDF fitted to width is readable from about 620px. The two-column candidate form needs about
  680px including panel padding. Together with the gap they need about 1320px. The threshold is
  set to a 1360px content area. It keeps 1366px laptops (about 1305px of content) stacked, and
  splits from about 1420px viewports, including 1536px laptops.
- `:has()` and CSS container queries are available in every browser the intranet supports.

**Edge cases.**

- The document list refreshes after the page loads, for example when a pending scan settles. The
  split appears or disappears with the preview, with no page-level state.
- A candidate whose only documents are not previewable (a non-PDF, or pending, refused or errored
  scans) still shows the preview panel with its message, so the split applies.
- At 390px nothing changes. The page does not scroll horizontally, and the viewer keeps 70vh.

**Success criteria.**

- At 1920×1080, a reader with `documents.download` sees the CV to the right of Datos principales
  on both pages, and it stays visible when scrolled to Documentos.
- At 1366×768, both pages look as they do today.
- Pages without a preview are at most 1440px wide at 1920px.

**Personal data, permissions and storage.** No new data path, endpoint, permission or grant. The
preview is the existing component. It still requests content only for callers holding
`documents.download`, through the audited, clean-scan-gated API response (principles 1 and 3).
Layout does not act as a control.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `candidate-profile-pages`: the "Competencias panel and stacked sections" requirement changes.
  Sections stay stacked, but the CV preview sits beside them on wide screens, and the edit page of
  an existing candidate gains the preview (a new requirement, "CV preview beside the candidate
  sections").
- `primary-navigation`: adds "Shell content width", the default 1440px cap on the shell's content
  area.

## Impact

- Frontend only:
  - `frontend/src/app/core/layout/app-layout.css`
  - `frontend/src/styles.css`
  - `frontend/src/app/features/candidates/pages/candidate-detail-page.tsx` and
    `candidate-edit-page.tsx`
  - `frontend/src/app/features/candidates/components/candidate-cv-preview.tsx` and its `.css`
- Every page in the shell gets a wider content area. Wide tables and forms on other pages stretch
  to 1396px.
- Tests: the candidate page unit specs and `tests/e2e/candidate-profile.spec.ts`.
- No API, database, dependency or i18n changes.
