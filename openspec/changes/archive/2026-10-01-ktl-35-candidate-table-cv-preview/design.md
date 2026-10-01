## Context

See `proposal.md` for the motivation and `specs/data-tables/spec.md` for the behaviour. The brief
(`openspec/KTL-35.md`) records the UX decisions agreed with the product owner.

Current state that shapes the approach:

- **Search projection.** `POST /api/candidates/search` feeds the candidate list, the advanced search
  and the position matches.
  - `SearchCandidatesHandler` returns `SearchPage<CandidateSearchItem>` straight from the
    repository.
  - `CandidateSearchQuery.Project` builds the item with two correlated subqueries on
    `"CND_Documents"`: primary presence and primary id.
- **Position candidates.** `GET /api/positions/{id}/candidates` builds `PositionCandidateItem` in
  `PositionRepository` (primary presence only) and maps it to `PositionCandidateResponse` in
  `ListPositionCandidatesHandler`.
- **Availability rule.** `CandidateDocumentResponse.From` derives «Available» from
  `ScanState == Clean` unless the document is a legacy record without a binary
  (`SourceKey != null && Sha256` blank). The SPA's `isPreviewable` adds
  `mimeType === 'application/pdf'`. `ContentType`, `Sha256`, `SourceKey` and `ScanState` are
  plaintext columns (only `OriginalFileName` is encrypted, KTL-33), so the rule translates to SQL.
- **`CandidateCvPreview`:**
  - takes a whole `Candidate`;
  - fetches the document list itself and follows pending scans;
  - fetches the content through `documentService.openPreview`, the audited download response;
  - keeps a per-instance `Map<documentId, Blob>`;
  - returns `null` without permission or documents.
- **Layout.** The candidate page (one page with per-panel edit mode since KTL-29) lays it out with `.page-split`. That is a CSS
  container query at 1360px, plus `.shell main:has(.page-split__aside .cv-preview)` for the 1920px
  shell. The DOM is identical at every width.
- **Tables.** Rows open their record through `useRowLink`, which ignores clicks on buttons and on
  `[data-row-link-ignore]` cells.

## Goals / Non-Goals

**Goals:**

- One page-level mechanism, shared by all four tables and three pages, that owns the open CV, its
  placement and its content.
- Reuse `CandidateCvPreview` (states, picker, viewer, fallback) and the KTL-28 split CSS rather
  than a second viewer or layout.
- Compute "can be previewed" once, in SQL, with the same rule the document API uses.

**Non-Goals:**

- Changing the candidate page's layout. Its behaviour changes only in when the preview appears (D12).
- An in-page PDF renderer, previewing other formats, or stepping between CVs by keyboard shortcut.
- Changing the has-CV filter, `hasPrimaryCv`, or the CSV export.

## Decisions

### D1. One previewability expression, in the domain

Add `CandidateDocument.IsPreviewable` as a static
`Expression<Func<CandidateDocument, bool>>`:

- `ScanState == Clean`;
- `ContentType == "application/pdf"`;
- not (`SourceKey != null` and `Sha256` blank).

Also expose a compiled predicate for in-memory use. `CandidateSearchQuery` and `PositionRepository`
use the expression inside their correlated subquery:

```csharp
dbContext.Documents
    .Where(d => d.CandidateId == candidate.Id && d.IsPrimary)
    .Where(CandidateDocument.IsPreviewable)
    .Any()
```

`CandidateDocumentResponse.From` keeps its own availability mapping. A unit test pins the three
pieces together (the domain predicate, the response's «Available», and the SPA's `isPreviewable`
fixture cases), so they cannot drift.

- _Alternative:_ return the primary document's scan state and content type and let the SPA decide.
  Rejected: it exposes scan internals and content types that the projection explicitly excludes,
  and it duplicates the rule.
- _Alternative:_ a stored/generated column. Rejected: it needs a migration for a value that is
  cheap to derive, and it would have to track scan transitions.

**Query cost.** The subquery is served by the partial unique index
`UX_CND_Documents_CandidateId_Primary`: at most one row per candidate, plus one heap check. It
replaces the removed `PrimaryCvDocumentId` subquery, so the statement keeps two correlated
subqueries. `SearchQueryPlanTests` and `EncryptedSearchPerformanceTests` guard this.

### D2. Mask in the handlers, not the queries

`SearchCandidatesHandler` and `ListPositionCandidatesHandler` read
`actor.HasPermission(Permissions.DocumentsDownload)` after their existing guards. When it is
missing, they return items with `PrimaryCvPreviewable = false` (a record `with` over the page).
The repositories stay actor-agnostic, which keeps the query-plan evidence about the real
statement. Handler unit tests with the hand-written actor double prove the masking, and
integration tests prove it over HTTP.

- _Alternative:_ pass the permission into the query and skip the subquery. It saves a subquery
  for non-downloaders, but splits the plan evidence into two statements. Not worth it at page size
  ≤ 100.

### D3. Drop `PrimaryCvDocumentId`

Remove it from `CandidateSearchItem`, the SPA's `SearchResult` and `CandidateListItem`, and every
test that asserts it. Its only consumer, «Abrir CV», is removed, and the preview resolves documents
through the candidate's document list. This narrows the projection (principle 1).

- _Alternative:_ keep it to preselect the primary document in the preview. Unneeded:
  `pickDefaultDocument` already prefers the primary.

### D4. Page-level controller: `RowCvPreview`

New module `features/candidates/components/row-cv-preview/`:

- **`row-cv-preview.logic.ts`** (pure):
  - `CV_SPLIT_MIN_WIDTH = 1360`, with a comment that pairs it with the `@container page-split`
    rule;
  - `placementFor(width)` → `'side' | 'inline'`;
  - `openKey(tableId, candidateId)`;
  - `isOpenRowGone(openKey, visibleKeys)`.
- **`row-cv-preview.tsx`:**
  - `RowCvPreviewProvider` holds `open: { tableId, candidateId, name } | null`, the placement and
    the single-entry content cache (D6). It exposes them through context.
  - `useRowCvPreview(tableId)` gives a table `isOpen(candidateId)`, `toggle(row)`,
    `reportVisible(candidateIds)`, `placement`, and a ref setter for the `.page-split` element.
  - Components: `RowCvButton` (the cell), `RowCvInlineRow` (the `<tr>` with `colSpan`) and
    `RowCvSidePanel` (rendered in `.page-split__aside`). Both of the latter wrap `CandidateCvPreview`
    with the shared header (`«CV de {{name}}»`, «Abrir ficha», «Ocultar»).
- **`row-cv-preview.css`:**
  - `.is-cv-open` row highlight;
  - `.cv-row` expansion;
  - the inline content pinned to the scroll container's width (`.table-wrap` becomes an
    inline-size container, and the cell content uses `width: 100cqi; position: sticky; left: 0`);
  - the slide-in, under `prefers-reduced-motion: no-preference`.

Each page wraps its table region in `<RowCvPreviewProvider>` and
`.page-split > .page-split__layout > .page-split__main + .page-split__aside`. The aside renders
`RowCvSidePanel` only when the placement is `side` and a CV is open. The existing `:has()` rules
then split the grid and widen the shell with no new CSS. Filters, criteria and the position
description stay outside the split. On the position page one provider wraps both panels, so "one
open CV" and the table + candidate key span them.

The context is feature-local: the tables in `search` and `positions` already import from
`features/candidates`, and nothing is added to `core/di`. The controller is UI state, not a
service.

- _Alternative:_ put the state in each table. Rejected: the position page needs one open CV across
  two tables.
- _Alternative:_ a singleton service with a signal. Rejected: the state is per page instance and
  must reset on leaving. A provider scoped to the page does that for free.

### D5. Placement chosen in code: a documented exception to "one DOM tree"

AGENTS.md and KTL-28 keep responsive layout in CSS with a single DOM. That works on the candidate
page because the preview has one DOM position. Here the two positions are structurally different:
a `<tr>` inside `<tbody>`, and an aside outside the table. CSS cannot move a node out of a table,
and `display: contents` tricks break table semantics.

So the provider observes the `.page-split` element with a `ResizeObserver` and derives the
placement from its inline size.

- **Simpler alternatives considered:**
  - _Render both and hide one with CSS._ Two `<object>` viewers, a double fetch or shared blob
    juggling, duplicate test ids, and a hidden viewer still loading. Rejected.
  - _Always the inline row._ At wide widths a fit-to-width PDF across ~1400px is very tall, and the
    table is pushed down by a full CV height. Rejected by the product owner.
  - _Always the side panel._ Below 1360px the table column would be under ~700px, and on phones
    there is no side. Rejected.
- **Mitigation:** the observer measures the container, never `window` or `matchMedia`, and uses
  the same 1360px constant as the CSS, with a comment on both sides. It cannot oscillate: widening
  the shell only makes the container wider, and 1360 is below the default 1396px content width.
  jsdom has no layout, so the placement function is pure and unit-tested, and the provider is
  tested with a stubbed `ResizeObserver`. The real layout is covered by Playwright at 1920, 1366
  and 390.
- **Initial placement:** before the first observation the provider assumes `inline`, and it only
  renders a CV after the user acts. So there is no flash.

### D6. Content cache lifted to the provider, one entry

`CandidateCvPreview` gains an optional `contentCache` prop (`get(documentId)`, `set(documentId,
blob)`). Without it, the component keeps its internal map, which the candidate page uses.

The provider's cache holds at most one entry, the open CV's current document. Moving between
placements remounts the viewer: the `<object>` reloads, but from the cached blob, with no request
and no audit entry. The entry is dropped when the CV closes, another candidate opens, or the
provider unmounts. Object URLs stay owned and revoked by the preview component, as today.

- _Alternative:_ keep the viewer mounted and move its DOM node with a portal to a changing
  container. React remounts children when a portal's container changes, and browsers reload a
  moved `<object>` anyway. There is no gain over a cached remount.
- _Alternative:_ an unbounded per-page cache. Rejected: it keeps more personal data in memory than
  the feature needs.

### D7. `CandidateCvPreview` generalised, not forked

Props become:

- `candidateId`;
- optional `initialDocuments` (the candidate page passes `candidate.documents`);
- optional `header` (replaces the default «Vista previa del CV» `h2`);
- optional `contentCache`;
- optional `emptyMessage`.

With `emptyMessage` set (the table flow), no documents shows that message instead of `null`, so an
opened CV is never an empty area when the flag went stale. The effect that resets on candidate
change keys on `candidateId`. The candidate page changes only its call site, plus the gate in D12.

### D8. Closing when the row leaves

Each table calls `reportVisible(tableId, candidateIds)` in an effect whenever its rendered rows
change. If the open key's table reports a list without that candidate, the provider closes it.
This covers paging, sorting, filtering, search re-runs, «Quitar de la posición», and bulk
deactivation that hides rows, without each page wiring its own close calls.

### D9. Focus and scroll

- Opening keeps focus on the button, following the disclosure pattern.
- The panel's «Ocultar» and any close that happens while focus is inside the CV return focus to
  the row's button. The provider keeps a ref per open button.
- On a `side` → `inline` switch, the provider calls `scrollIntoView({ block: 'nearest' })` on the
  open row. If focus was inside the old container, it focuses the row's button.

### D10. Column and permission in the SPA

Each table reads `usePermission('documents.download')` once at the top and omits the «CV» `<th>`
and `<td>` without it. The button renders when `row.primaryCvPreviewable`. The API already masks
the flag, so the permission check is only there to hide the whole column.

In `search-results.tsx`:

- `openCv`, `canOpenCv`, `showOpenCv` and the `cvNotAllowed` paragraph go;
- the empty actions `<th>` renders only when `renderRowAction` is given;
- `colSpan` values derive from the rendered column count.

### D11. Copy

New flat keys in `es.json`:

- `cvPanel.show` («Ver») and `cvPanel.hide` («Ocultar»);
- `cvPanel.showLabel` («Ver el CV de {{name}}») and `cvPanel.hideLabel` («Ocultar el CV de
  {{name}}»);
- `cvPanel.title` («CV de {{name}}»);
- `cvPanel.openProfile` («Abrir ficha»);
- `cvPanel.empty` («Este candidato ya no tiene un CV que se pueda previsualizar.»), the stale-flag
  message of D7;
- `cvPanel.downloadLabel` («Descargar el CV de {{name}}»), `cvPanel.downloadFailed` and
  `cvPanel.downloadUnavailable`, for D13.

Since D14 the row buttons show icons, so `cvPanel.show` and `cvPanel.hide` remain as the panel's
«Ocultar» text and as the base of the accessible names.

Removed keys: `cvIndicator.present`, `cvIndicator.absent`, `search.results.openCv`,
`search.results.cvUnavailable`, `search.results.cvDownloadFailed` and
`search.results.cvNotAllowed`. English values go in `en.json`. The new components render no
hardcoded copy, so none is added to `LEGACY_HARDCODED_COPY`.

### D12. The candidate page shows its preview only for a previewable primary CV

Added during implementation at the product owner's request. The candidate page follows the rule
behind `primaryCvPreviewable`. `CandidateCvPreview` gains a `requirePreviewablePrimary` prop,
which the candidate page sets. With it, the component renders nothing unless the primary document
in its **live** document list passes `isPreviewable`, the SPA mirror of
`CandidateDocument.IsPreviewable` pinned by the D1 test.

The gate reads the live list rather than the page's `candidate.documents`. The component keeps
listing documents and following pending scans, so a pending primary that turns clean appears
without a reload. While the gate is closed, no content is requested: the content effect already
runs only for a selected previewable document, and the selection is forced to the primary.
`.page-split__aside:empty` and the `:has(.cv-preview)` rules then give the one-column page.

The rows do not set the prop. There, «Ver» already implies a previewable primary, and a stale
flag shows the availability message instead (D7).

- _Alternative:_ a `primaryCvPreviewable` field on the candidate detail response, computed in
  SQL. Rejected: the page already holds every document's availability and content type, the SPA
  rule is pinned to the server rule by test, and a server field would not update when a pending
  scan settles on the open page.
- _Alternative:_ keep offering non-primary previewable documents when the primary cannot be
  previewed. Rejected: the product owner asked for one rule across tables and page.

### D13. Download from the row through the document list

Added during implementation at the product owner's request. `primaryCvDownloadable` sits beside
the previewable flag:

- `CandidateDocument.IsDownloadable`: a clean scan and a binary that exists, in any format;
- the same correlated primary-document subquery as D1;
- the same handler masking as D2, and the same pinning test against «Available».

`RowCvDownloadButton` resolves the primary document through `documentService.list(candidateId)`
and calls `documentService.download`. That is two requests, both permission-checked, and the
download is audited as today. A stale flag (no available primary in the list) shows a warning
toast and downloads nothing.

- _Alternative:_ restore `primaryCvDocumentId` to download in one request. Rejected: D3 removed it
  to narrow the projection, and a click-time list read costs one small request.
- _Alternative:_ a `GET .../documents/primary/content` endpoint. Rejected: it is a new delivery path
  for the same bytes, and the existing per-document route already carries every gate.

### D14. Icon buttons

The download and «Ver»/«Ocultar» buttons are icon-only: a disk, and an eye that is crossed out
when open. Both are inline SVG, so there is no icon dependency. Each has an `aria-label` naming
the candidate and the same text as `title`. The eye button adds a decorative arrow that follows
the placement:

| Placement      | «Ver» | «Ocultar» |
| -------------- | ----- | --------- |
| Beside (side)  | →     | ←         |
| Under (inline) | ↓     | ↑         |

The panel's own «Ocultar» stays a text button.

### D15. Opening and closing motion

Added during implementation at the product owner's request. Opening plays a CSS animation of
about 260 ms with an ease-out curve:

- beside the table, the panel slides in from the right;
- under the row, the row unfolds (`grid-template-rows` 0fr → 1fr, with the cell's padding).

Hiding the CV the user asked to hide plays the reverse in about 220 ms. The provider marks the
open CV `closing`, and the button and row already read as hidden. It keeps the CV mounted for
`CV_CLOSE_MS` and then removes it. The CSS variable `--row-cv-close` and the constant are kept
equal. Everything else that removes a CV is immediate: opening another CV, the row leaving, or the
page unmounting. The page therefore never waits on an animation, and a pending removal is
cancelled.

Users who prefer reduced motion get no animation and an immediate removal. `animatesClose` reads
`prefers-reduced-motion` through `matchMedia`. This is a motion preference, not a layout decision,
so it does not reopen D5. Where `matchMedia` is missing (jsdom), the CV is removed at once.

- _Alternative:_ `transitionend`/`animationend` to remove the CV. Rejected: the event never fires
  when the animation is suppressed or in jsdom, so the CV could stay mounted indefinitely.

### D16. Action and «CV» cells ignore row clicks

Added during implementation at the product owner's request. The «CV» cell in all four tables, and
the actions cell in the search, matches and position candidate tables, carry
`data-row-link-ignore`. `useRowLink` then ignores any click in them, as it already did for the
selection cell, so a click just beside a button does nothing. `row-link.css` gives these cells the
default cursor. The rest of the row still opens the candidate.

### Stack, data, authorization and storage impact

| Area               | Impact                                                                                                                                                                                                  |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Technical stack    | No new dependency (npm or NuGet). `ResizeObserver` and container query units are platform features.                                                                                                     |
| Data model         | None: no table, column, index, constraint or migration. Two read projections change shape (one field added to both, one removed from search).                                                           |
| Authorization      | No new permission. Existing guards unchanged and still first. A new read of `documents.download` in two handlers only lowers what is returned.                                                          |
| Database privilege | `ktl_runtime` already selects from `"CND_Documents"` in both queries. No grant change.                                                                                                                  |
| Storage            | Unchanged. Content is served only by the existing download response (quarantine, clean gate, audit).                                                                                                    |
| Test strategy      | xUnit handler and API tests for the flag matrix and masking. Vitest for the logic, provider, tables and preview. Playwright for the layout at three widths, resize, and the position page's two tables. |

## Risks / Trade-offs

- **Table reflow.** Opening the side panel narrows the table, so the clicked row can shift. →
  Rows keep their height where possible (the name/email cell may wrap). Scroll anchoring keeps the
  clicked row's viewport offset, and e2e asserts the row stays in view.
- **Shell widening shift.** On screens wider than 1440px the content re-centres when the first CV
  opens. → Same behaviour as KTL-28's candidate page, accepted there. It happens only on
  open/close, not when switching candidates.
- **PDF position lost on resize.** The viewer returns to page 1 when the CV moves. → Moves happen
  only when the threshold is crossed, which is rare. The content is not re-downloaded.
- **No inline PDF on some phones** (notably Chrome on Android). → The native attempt is always
  made, and the existing fallback offers «Descargar». An in-page renderer is out of scope (see the
  brief).
- **Loss of «Abrir CV» for non-PDF primaries** on the search page. → Download stays available from
  the candidate page's «Documentos» panel. Accepted by the product owner.
- **Stale flag.** A document may change between list load and click. → The preview re-reads the
  document list and shows availability states. The content endpoint re-checks permission and scan
  state.
- **API consumers of `primaryCvDocumentId`.** The SPA is the only client. → Removed in the same
  change, and `SearchApiTests` asserts its absence.

## Migration Plan

This is a single deployable slice with no data migration. Backend and SPA ship together through
the usual Compose build. Rollback means reverting the change: no persisted state depends on it.
