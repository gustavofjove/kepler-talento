## Context

See `proposal.md` (Why) and the enhanced brief `openspec/KTL-39.md` for the observed defects.

- `CandidateDocuments` (`frontend/src/app/features/candidates/components/candidate-documents.tsx`)
  renders rows with the shared `.item-row` / `.item-main` rules from `styles.css`. `.item-main` is
  `display: grid`, which stretches every `.badge`; `.item-row` spreads its buttons with
  `space-between`. Only Educación and Experiencia share `.item-main`; Notas uses `.item-row` alone.
- The component is mounted once and switched between modes by `readOnly` (KTL-29), so polling,
  the selected file and the dirty report survive mode changes. That shape stays.
- `CandidateDocument` already carries everything the new row needs: `originalFilename`,
  `mimeType`, `sizeBytes`, `uploadedAt`, `isPrimary`, `documentType`, `availabilityState`.
- Backend: `SetPrimaryCandidateDocumentHandler` checks `documents.upload`, then calls
  `IDocumentRepository.SetPrimaryAsync`, which accepts any document. The domain already defines
  availability as `CandidateDocument.CanBeDownloaded` (clean scan and binary present), the same
  rule behind `availabilityState: "Available"`. `DocumentErrors.NotAvailable`
  (`document.not_available`) is declared but unused. `ConflictException` maps to 409 in
  `GlobalExceptionHandler`. The SPA's `AppError` keeps the problem `code` and `detail`.
- Upload (`UploadCandidateDocumentHandler`) sets the primary flag on a `PendingScan` document; it is
  deliberately left alone.

## Goals / Non-Goals

**Goals:**

- One consistent row component and stylesheet for documents, driven by pure helpers that are
  unit-testable without rendering.
- The primary rule enforced by the API; the UI only mirrors it.
- Keep every existing `data-testid` and `name=` the Playwright suite relies on.

**Non-Goals:**

- No change to `DocumentService`, `api-transport.ts`, polling cadence, upload validation or the CV
  preview.
- No generic list/row component for other panels; only the `.item-main` rule is corrected for
  Educación and Experiencia.
- No database, migration, grant or endpoint-shape change.

## Decisions

### D1 — Availability check in the handler, not the repository

`SetPrimaryCandidateDocumentHandler` loads the document with `documents.FindAsync` after the
permission guard, throws `DocumentErrors.Missing()` if absent and the new
`DocumentErrors.NotAvailableException()` if `!document.CanBeDownloaded`, then calls
`SetPrimaryAsync` as today.

- Why: the rule is a business rule over an aggregate the domain already models; the handler is
  where the slice's guards live, and the order (permission → existence → availability) keeps the
  endpoint fail-closed and non-revealing.
- Race: availability can only move from pending to clean or refused; a document refused between the
  check and the write is the same window that exists for any scan result and is acceptable. A clean
  document never becomes unavailable except by removal, which `SetPrimaryAsync` already reports as
  not found.
- Alternative: a `WHERE` clause inside `SetPrimaryAsync` returning a new outcome. Rejected: it
  spreads the availability rule into persistence and adds an outcome enum value for one caller.
- Status 409 (`ConflictException`) over 422: the request is well-formed; it conflicts with the
  document's current state. The ProblemDetails title is the generic conflict title; the `detail`
  carries the Spanish message the SPA shows.

### D2 — Pure helpers in `candidate-documents.logic.ts`

- `documentStateChip(state)` → `{ labelKey, tone } | undefined` (undefined for `Available`; unknown
  or missing state treated as legacy, as today).
- `documentExplanationKey(state)` → explanation key or undefined.
- `documentDetails(document)` → `{ format, sizeBytes, type? }`: upper-case extension of
  `originalFilename` (falling back to the `mimeType` subtype), type only when not `CV`.
- `formatFileSize(bytes)` → `formatNumber` with `style: 'unit'`: kilobytes (rounded, at least 1)
  below 1 MB, megabytes with one decimal above. Units come from `Intl`, so no unit copy is
  hardcoded.
- `canMarkPrimary(document)` → available and not primary.
- Why: AGENTS asks for pure helpers outside `.tsx` modules (fast refresh), and the brief wants one
  tone mapping, not inline conditionals.

### D3 — Row markup and layout

A `<ul className="document-list">` of `<li className="document-row" data-testid="candidate-document">`,
each a CSS grid with named areas `icon main state actions` and columns
`16px minmax(0, 1fr) auto auto`. The actions cell holds fixed 32px slots; a slot whose action does
not apply renders an empty `<span className="document-row__slot" aria-hidden="true">`.

- Why per-row grids with fixed slots rather than one grid for the list (`subgrid` or
  `display: contents`): the actions column has a constant width per mode, which is all alignment
  needs, and `display: contents` on `<li>` has a history of dropping list semantics.
- Why not a `.data-table` like Posiciones: a table needs horizontal scrolling at 390px, and the
  row has one main cell plus two narrow ones.
- Narrow panels use `container-type: inline-size` on the section and `@container (max-width: 520px)`
  to move state and actions to a second row. AGENTS forbids branching on `window.innerWidth` /
  `matchMedia` and keeps the shell breakpoint in `primary-nav.css`; a container query is pure CSS
  scoped to the panel, so it respects both rules. jsdom ignores it, so layout is covered by e2e.
- `document-availability` is rendered on every row as the state cell, with
  `data-state={availabilityState ?? 'LegacyUnavailable'}`; it contains the `StatusChip` (KTL-38) or
  nothing. E2e specs assert the attribute instead of Spanish text.

### D4 — Icon buttons and shared icons

`frontend/src/app/shared/components/icons.tsx` exports `DownloadIcon` (moved verbatim from
`row-cv-preview.tsx`) plus `FileIcon`, `StarIcon`, `TrashIcon`, `UploadIcon`, `CloseIcon`, all
decorative (`aria-hidden`), 16px viewBox, 1px strokes on half-pixel coordinates like the existing
icon. Buttons are `button ghost icon-button` (32×32) with `aria-label` and `title` from
`action.download|markPrimary|remove` interpolating the filename; remove adds an `is-danger` modifier
that fills with `--danger` on hover. Test ids: `document-download`, `document-mark-primary`,
`document-remove`.

- Alternative: text buttons with fixed widths. Rejected in the brief: wider columns and they wrap
  first on narrow panels.

### D5 — Upload subsection and drag and drop

The form keeps `onSubmit={upload}` and becomes
`<form className="section-block candidate-add-form document-upload">` with an `h3`. The native
`<input type="file">` stays in the form with its `id`, `name`, `data-testid`, `accept` and `ref`,
hidden with a new `.visually-hidden` utility in `styles.css` (clip pattern, so it stays in the
accessibility tree and `setInputFiles` works). «Seleccionar archivo» is a `button` that calls
`fileInput.current.click()`.

- Drop area (`data-testid="document-drop-zone"`): `onDragOver` prevents default and sets an
  `isDragging` flag for the highlight; `onDragLeave` clears it; `onDrop` prevents default and
  selects `event.dataTransfer.files[0]`. The selection lives in the existing `selectedFile` state,
  so the dirty report and upload path are unchanged. Native events, no dependency.
- Selected card (`data-testid="document-selected"`): file icon, name, `formatFileSize`, a clear
  button (`document-clear`) that resets `selectedFile` and the input's value, the
  `document-is-primary` checkbox and the `document-upload` submit button.
- `FormError` (`document-upload-error`) renders inside the form above the card.
- Alternative: upload on selection. Rejected (out of scope): it removes the chance to untick
  «Marcar como CV principal» after picking the file.

### D6 — Mark-primary failure handling

On any `setPrimary` failure the component shows the error through `useErrorToast` (which shows the
API `detail`) and calls `refresh()`. No branch on `document.not_available`: refreshing is cheap and
also covers the existing primary-conflict 409 and a removed document.

### D7 — `.item-main` for Educación and Experiencia

`.item-main` becomes `display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px;
margin: 0`. Adjacent JSX text nodes form a single anonymous flex item, so «Empresa (Sector) · 3
años» stays one run after the chip. Documents no longer use `.item-main`, so only those two panels
are affected.

### D8 — Copy

New keys under `candidate.profile.documents.*` as listed in the brief (`details`,
`detailsWithType`, `state.refused`, `state.legacy`, `action.*`, `upload.*`); `state.pending`,
`state.error`, `explanation.*`, toasts and failure messages stay. Keys that become unused are
removed. `candidate-documents.tsx` is not in `LEGACY_HARDCODED_COPY`, so lint already enforces
`t()` for JSX text; attribute copy is translated by hand.

## Risks / Trade-offs

- [Existing integration test marks a never-scanned upload primary] → Update
  `Marking_a_second_uploaded_document_primary_demotes_the_first` to mark the document clean through
  the test `DbContext` (`MarkClean`) before the `PUT`; this also documents the new rule.
- [Other clients rely on marking pending documents primary] → None exist besides the SPA, which
  only offers the star on available documents; the import tool writes the database directly.
- [A primary that the scan later refuses stays primary] → Accepted, as in the brief: the panel
  shows it as «Rechazado» with «Principal», and the manager can star another available document.
- [Icon-only actions are less discoverable] → `title` tooltips and accessible names with the
  filename; download keeps the icon already used in the candidate list.
- [RTL matches accessible names exactly] → Unit specs switch to the per-file names or test ids.
  Playwright matches substrings, so e2e lookups by «Descargar» keep working.
- [Container queries are not evaluated by jsdom] → Narrow layout is asserted in the existing e2e
  no-horizontal-scroll checks at 390px.

## Test strategy

- Vitest: new `tests/unit/candidate-documents.logic.spec.ts` for the helpers; rewritten
  `tests/unit/candidate-documents.spec.tsx` for rows, chips, action slots per mode and permission,
  mark-primary refusal → toast and refresh, upload card, clear, drop (`fireEvent.drop` with
  `dataTransfer.files`), dirty reports, and the existing abort-on-unmount case;
  `candidate-cv-preview.spec.tsx` still passes after the icon move.
- Playwright: `candidate-documents.spec.ts`, `secure-access.spec.ts`, `security-ops.spec.ts` assert
  `data-state`; the documents journey checks the drop area, selected card and aligned layout at
  1280 and 390 without horizontal scroll.
- xUnit integration (`CandidateApiTests`): refusal for pending, infected/rejected, scan-failed and
  legacy-without-binary documents (409, `document.not_available`, primary unchanged, no
  `document.primary.changed` audit); unauthorized and forbidden callers refused before the
  availability check; the updated swap test.

## Migration Plan

No data migration. Deploy API and SPA together as usual; rollback is a redeploy of the previous
images. The API change only adds a refusal, so an older SPA against the new API simply sees a 409
toast if it offers the star on an unavailable document.
