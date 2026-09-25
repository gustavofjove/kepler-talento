## Context

See `proposal.md` for the motivation, and the delta specs for the behavior.

- The shell's `main` has `max-width: 1180px; padding: 22px`
  (`frontend/src/app/core/layout/app-layout.css`). The sticky header is at least 64px tall, and
  `html` has `scroll-padding-top: 72px`.
- The confirm dialog and toasts are mounted outside `main`, and the catalog picker popovers are
  react-aria overlays portalled to `body`. Nothing inside the candidate sections uses
  `position: fixed`.
- `CandidateCvPreview` owns its own visibility. It returns `null` without `documents.download`, or
  when no document can be selected. After mount it refetches the document list and follows pending
  scans, so the candidate aggregate alone cannot tell whether it will render.
- `.grid.two` (`frontend/src/styles.css`) collapses only through `@media (max-width: 760px)`.
- AGENTS.md keeps the shell's navigation breakpoint (768px) in `primary-nav.css` and forbids
  `matchMedia` or `window.innerWidth` branching. Layout must stay CSS-only with one DOM tree.

**Stack impact.** CSS and markup in two pages and one component. There is no new dependency, no
data model change, no API change and no storage change.

## Goals / Non-Goals

**Goals:**

- One reusable CSS layout (`.page-split`) that any page can opt into with a sections column and an
  aside.
- The split turns on when the preview is actually in the DOM, and only then.
- Inner field grids respond to their column's width.

**Non-Goals:**

- A resizable or collapsible splitter, or persisting a layout preference.
- Changing preview fetching, caching, auditing or scan gating.
- Redesigning other pages for the wider shell beyond what their existing fluid layouts already do.

## Decisions

### 1. Width token on the shell, `:has()` for the wide variant

`.shell main` reads `max-width: var(--shell-max-width)`, with `--shell-max-width: 1440px` declared
on `.shell`. The rule `.shell main:has(.page-split__aside .cv-preview)` sets the width to 1920px.

- _Alternative: a layout-route prop or class toggled from React._ It needs shell plumbing and state
  for what is only a presentational fact, and it could get out of step with the preview's own
  render decision. Rejected.
- _Alternative: fill the full screen on every page._ Tables and short forms stretch awkwardly on
  2560px screens. Rejected in favor of a cap.

### 2. The split is keyed to the preview's presence

`.page-split__layout` is a one-column grid by default. Two columns apply only through
`.page-split__layout:has(> .page-split__aside .cv-preview)` inside the width query (Decision 3). The aside element is
always rendered; while the preview renders `null` it is empty, and `.page-split__aside:empty` is
`display: none` so the stacked gap does not appear.

- _Alternative: a `hasPreview(candidate, canDownload)` helper in `candidate-cv-preview.logic.ts`
  that the page uses to decide the layout._ It duplicates the component's rule and misses async
  changes, for example the refreshed list or a scan that settles. Rejected.

### 3. Width query: a container query on the page

`.page-split` is wrapped by the page's `.page` section. The content area's width is what matters,
so `.page-split` declares `container: page-split / inline-size`. Its two children are laid out by
an inner `.page-split__layout` grid, because a container cannot query itself.

- Structure: `.page-split` (container) > `.page-split__layout` (grid) > `.page-split__main` +
  `.page-split__aside`.
- Two columns apply at `@container page-split (min-width: 1360px)`. That is about 1320px of
  columns plus margin, and it keeps a 1366px viewport (about 1305px of content) stacked:
  `grid-template-columns: minmax(0, 1fr) minmax(560px, 45%)`.
- _Alternative: a viewport media query (about `min-width: 1420px`)._ It only works through
  arithmetic on the shell's padding and scrollbar, and breaks if the shell width changes. Rejected.

### 4. Inner grids follow `.page-split__main`

`.page-split__main` declares `container: page-split-main / inline-size`.
`@container page-split-main (max-width: 620px) { .grid.two { grid-template-columns: 1fr } }` is
scoped by the named container, so no other page is affected. The existing viewport rule stays. At
1920px the sections column is about 1030px, so field grids keep two columns. They collapse only
near the 1360px threshold.

Containment turns `.page-split__main` into the containing block for `position: fixed` descendants.
None exist, per the Context, and the popovers are portalled.

### 5. Sticky aside and viewer height

`.page-split__aside` sticks at `top: calc(64px + 20px)` when split. Its height is
`calc(100vh - 64px - 40px)`, and `align-self: start`. The preview panel inside is a flex column,
and the `<object>` viewer gets `flex: 1; min-height: 0`. When stacked, the viewer keeps
`min-height: 600px` (70vh below 767px), as today. The component drops `span-all` because it no
longer sits in a spanning grid.

### 6. Edit page wiring

The edit page wraps its main-data panel and the relation grid in `.page-split__main`, and renders
`<CandidateCvPreview candidate={candidate} />` in the aside only when `candidate` exists. The
new-candidate route therefore never renders it.

**Authorization model.** Unchanged. The preview component still gates on
`usePermission('documents.download')`, and the API enforces the permission and scan state. Layout
is not a control.

**Test strategy.**

- Unit (jsdom):
  - Structure: the preview sits inside `.page-split__aside` on both pages.
  - Presence of the edit-page preview with and without `documents.download`.
  - Absence on `/new`.
  - jsdom has no layout, so geometry is not tested here.
- E2e (Playwright), checking bounding boxes:
  - At 1920×1080: the preview is right of Datos principales on detail and edit, and stays in the
    viewport after scrolling to Documentos. The main width is at most 1440px on a page without a
    preview.
  - At 1366×768: the preview is below Documentos.
  - At 390px: no horizontal scroll.
  - A picker popover still opens inside the viewport.

## Risks / Trade-offs

- [Every page gets wider content, 1136px → 1396px] → The existing grids are fluid. Check the list,
  search, positions and admin pages at 1920px during verification.
- [`:has()` or container query support] → All evergreen browsers support both. The intranet has no
  legacy browser requirement. Without support, the page falls back to today's stacked layout.
- [A tall PDF viewer inside a sticky column on short screens] → The height is tied to the viewport,
  and the split needs at least 1360px of width, where screens are at least about 720px tall.
- [Frame reflow when the preview mounts late] → The split appears as soon as the preview section
  renders, including its loading state, so it happens once and early.

## Migration Plan

Frontend-only deployment through the usual image build. Roll back by reverting the commit. There is
no data or API state to migrate.
