# KTL-28 — Wider shell and CV preview beside the candidate content

## [original]

What are your thoughts on the maximum width of the site? I think it's currently fixed to 1136px
and I wonder if it should be wider. What is the standard and what's a suitable value for this
site?

There's a reason for this question and it's the fact that I'd like to add the CV doc side by side
with the content on the Candidate view and edit pages. Some of the content is already prepared for
this as it's aligned to the left leaving space on the RHS and where this is not the case (Edit
candidate main properties, job experience, Education, etc.) there are just two columns that can
become one.

Anyway I believe the content container should be wider overall but sorting this issue should be
also ideal.

## [enhanced]

**Status:** Proposed

### Summary

The shell caps `main` at `max-width: 1180px` with 22px padding
(`frontend/src/app/core/layout/app-layout.css`), so every page gets 1136px of content, whatever
the screen. That suits reading pages, but Kepler Talento is a data tool used mostly on 1920px
office monitors and on 1536px or 1366px Windows laptops. At 1920px the cap leaves about 780px
unused.

The candidate detail page shows the CV preview (`CandidateCvPreview`) as a full-width panel below
Documentos. The edit page has no preview. HR staff read the CV while they check or type the
candidate's data, so they scroll back and forth between the two.

This ticket widens the shell and puts the CV preview in a sticky column on the right of the
candidate detail and edit pages, whenever the screen has room for both.

### User story

As an HR user reviewing or editing a candidate, I want the candidate's CV visible beside their
data, so that I can compare and transcribe without scrolling between the sections and the
document.

### Decisions

| Topic                    | Decision                                                                                                                                                                                                                                                                                                       |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Default shell width      | `main` max width **1440px** on every page (about 1396px of content), set once as a `--shell-max-width` custom property.                                                                                                                                                                                        |
| Width with a CV preview  | **1920px** when the page shows the CV preview, selected with `.shell main:has(...)` so the shell needs no per-route configuration.                                                                                                                                                                             |
| When the split applies   | When the page's content box is at least **1360px** wide. Rough sizes: a PDF fitted to width is readable from about 620px, and the two-column candidate form needs about 680px with panel padding. Below 1360px the preview stays stacked after the sections, as today.                                         |
| Column sizes             | Content `minmax(0, 1fr)`, preview `minmax(560px, 45%)`.                                                                                                                                                                                                                                                        |
| Preview column behavior  | `position: sticky` below the 64px sticky header. Its height is the viewport minus the header and gaps, and the PDF viewer fills it, so the document stays in view while the sections scroll.                                                                                                                   |
| Two-column inner grids   | `.grid.two` inside the content column collapses to one column according to the column's width (a container query), not the viewport's. The global `@media (max-width: 760px)` rule stays for every other page.                                                                                                 |
| When there is no preview | `CandidateCvPreview` renders nothing without `documents.download`, without documents, or without a document to select. In those cases the page stays one column at the default width. The split is keyed to the preview's presence in the DOM (`:has(.cv-preview)`), not to a duplicated page-level predicate. |
| Edit page                | Gains the same preview, only for an existing candidate. `/app/candidates/new` has no documents and does not change.                                                                                                                                                                                            |
| Spec change              | `candidate-profile-pages` "Competencias panel and stacked sections" currently says the panels span the full content width with the CV preview after them. This change modifies that requirement.                                                                                                               |

### Existing behavior and contract

- `frontend/src/app/core/layout/app-layout.css`: `.shell main { max-width: 1180px; padding: 22px }`.
  The sticky header has a `min-height` of 64px, and `html { scroll-padding-top: 72px }`.
- `frontend/src/styles.css`: `.page` is a 20px-gap grid, `.grid.two` has two columns, and it
  collapses at `@media (max-width: 760px)`.
- `frontend/src/app/features/candidates/components/candidate-cv-preview.tsx`: a
  `section.panel.span-all.cv-preview` (`data-testid="candidate-cv-preview"`). It returns `null`
  unless the caller holds `documents.download` and a document is selected. It refetches the
  document list and follows pending scans itself.
- `candidate-cv-preview.css`: the viewer has `min-height: 600px` (70vh below 767px).
- `.grid.two` is used by `candidate-form.tsx`, `candidate-education.tsx`,
  `candidate-experience.tsx` and `candidate-documents.tsx`. The Competencias family rows
  (KTL-27) are already one column.
- Catalog picker popovers are react-aria overlays portalled to `document.body`, so container
  containment on an ancestor does not clip or reposition them.
- The change needs no API, permission, data or storage work. Preview fetching, auditing and scan
  gating are unchanged (`private-document-storage` "Controlled inline preview").

### Acceptance criteria

1. **Wider shell.** Given a 1920px-wide viewport, when any page inside the shell is shown without
   a CV preview, then `main` is at most 1440px wide and centered.
2. **Detail page split.** Given a user with `documents.download` and a candidate with a clean PDF,
   when the detail page is shown at 1920×1080, then the CV preview is to the right of Datos
   principales, and the sections keep their KTL-27 order in the left column.
3. **Edit page split.** Given the same user with `candidates.update`, when the edit page of that
   candidate is shown at 1920×1080, then the CV preview is to the right of Datos principales.
4. **Sticky preview.** Given the split layout, when the user scrolls down to Documentos, then the
   preview stays fully visible below the header.
5. **Stacked fallback.** Given a viewport of 1366×768, when either page is shown, then the preview
   is below the last section, as today.
6. **No preview, no split.** Given a user without `documents.download`, or a candidate without
   documents, when either page is shown at 1920px, then the page is one column at the default
   width, with no empty right-hand column.
7. **Inner grids follow the column.** Given the split layout, when the content column is narrower
   than the container breakpoint, then the main-data form, Educación, Experiencia and Documentos
   forms show their fields in one column. When it is wider, they keep two.
8. **Narrow viewport unchanged.** At 390px wide both pages still have no horizontal scroll, and the
   preview keeps its 70vh viewer.
9. **Popovers intact.** In the split layout the Competencias picker and level popovers open inside
   the viewport.

### Implementation scope

Frontend only.

- `frontend/src/app/core/layout/app-layout.css`:
  - Add `--shell-max-width: 1440px`, used by `.shell main`.
  - Add a `.shell main:has(.page-split .cv-preview)` override to 1920px.
- `frontend/src/styles.css`, a shared `.page-split` layout:
  - The wrapper is a grid that stays one column by default.
  - Its main column (`.page-split__main`) is an inline-size container.
  - The split is enabled at a width-based breakpoint only when `:has(.cv-preview)` matches.
  - A sticky aside (`.page-split__aside`).
  - An `@container` rule that collapses `.grid.two` inside the main column.
  - Use the Kepler tokens and add no inline styles.
- `frontend/src/app/features/candidates/pages/candidate-detail-page.tsx`: wrap the section grid
  in `.page-split__main`, and move `<CandidateCvPreview>` into `.page-split__aside`.
- `frontend/src/app/features/candidates/pages/candidate-edit-page.tsx`: wrap the main-data panel
  and the section grid in the same structure. Render `<CandidateCvPreview candidate={candidate} />`
  in the aside when `candidate` exists.
- `frontend/src/app/features/candidates/components/candidate-cv-preview.tsx/.css`:
  - Drop `span-all`.
  - In the aside, the panel is a flex column and the viewer fills the remaining height
    (`flex: 1; min-height: 0`).
  - When stacked, the viewer keeps `min-height: 600px`.
- Keep every `data-testid` and `name=` attribute. No copy changes, so `es.json` is unchanged.

### Verification

- Unit (`frontend/tests/unit/`):
  - `candidate-edit-page.spec.tsx` renders the preview for a user with `documents.download` and
    omits it without the permission.
  - `candidate-detail-page.spec.tsx` still finds `candidate-cv-preview` inside the aside.
- E2e (`frontend/tests/e2e/candidate-profile.spec.ts`):
  - Acceptance criteria 2 to 9, compared through bounding boxes at 1920×1080, 1366×768 and
    390px.
  - Selectors use roles and test ids, not Spanish text.
- Regression: `navigation-responsive.spec.ts`.
- Gates: `npm test`, `npm run lint`, `npm run format:check`, `npm run build:all`.

### Documentation

- A delta spec for `candidate-profile-pages` that modifies "Competencias panel and stacked
  sections" for the split layout.
- `README.md` only if it describes the candidate page layout.

### Non-functional requirements

- **Security and personal data:** no new data path. The preview keeps its permission check and
  the audited, clean-scan-gated fetch. Hiding the column is not a control.
- **Accessibility:**
  - DOM and reading order stay content first, then preview.
  - The preview keeps its heading and viewer label.
  - Keyboard focus order does not jump between the columns.
- **Responsive:** one DOM tree for every width. The container query and the width breakpoint are
  CSS only. There is no `matchMedia` or `window.innerWidth`.
- **Browser support:** `:has()` and container queries are available in every evergreen browser
  the app targets.

### Out of scope

- A resizable splitter, or a control that collapses or detaches the preview.
- Previewing formats other than PDF.
- Split layouts on pages other than the candidate detail and edit pages.
