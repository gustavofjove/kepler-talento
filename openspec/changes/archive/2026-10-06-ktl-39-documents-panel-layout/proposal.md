## Why

The Documentos panel on the candidate page is the worst-looking panel in both read-only and edit
mode. Its chips stretch into bars as wide as the filename (`.item-main` is a grid, so each `.badge`
is stretched), its buttons are spread across the row by `space-between` and never line up from row
to row, every row carries a redundant «CV» chip, and the document «Disponible» chip repeats the word
the candidate's own availability chip (KTL-38) uses with another meaning. The upload form shows the
browser's untranslated «Choose File / No file chosen» control and stacks «Subir CV» over «Hecho».
Edit mode also offers «Marcar principal» on pending, refused and legacy files, and the API accepts
it, so a candidate can end up with a primary CV that nobody can open (brief:
`openspec/KTL-39.md`).

## What Changes

- Each document becomes one row on a fixed grid: file icon, filename (one line, ellipsis) with
  «Principal» beside it, a details line («PDF · 240 kB · 12 sept 2026»), a state chip and fixed
  action slots. The «CV» type chip is removed; a type other than `CV` goes in the details line.
- State chips by exception, with the KTL-38 tones: neutral «En análisis», danger «Error de
  análisis», danger «Rechazado», neutral «Sin archivo». An available document shows no chip; the
  download button signals it. The explanation of an unavailable document replaces its details line,
  and the "still scanning" notice moves into the pending row.
- Row actions become 32px icon buttons (download, mark primary, remove) in fixed slots, with
  accessible names that include the filename. Mark primary is offered only on available,
  non-primary documents.
- **API:** `PUT /api/candidates/{candidateId}/documents/{id}/primary` refuses a document that is
  not available with 409 and the stable code `document.not_available`. Upload with «Marcar como CV
  principal» is unchanged: a file still being scanned can become primary on upload.
- The upload form becomes a «Subir documento» subsection: a dashed drop area with «Seleccionar
  archivo» and a formats/size hint (dropping a file selects it), then a card for the selected file
  with its size, a clear button, the «Marcar como CV principal» checkbox and «Subir documento». The
  native input stays in the DOM, hidden. The panel footer keeps only «Hecho».
- Below 520px of panel width the chip and actions move under the filename (container query).
- `.item-main` becomes an inline wrapping row, so the chips in Experiencia and Formación are sized
  to their label and sit on the same line as their text.

Actors: readers of the candidate page (`candidates.read`, download with `documents.download`) see
the read-only rows; document managers (`documents.upload`) get the edit-mode actions and upload.

Edge cases: a document with no availability state renders as legacy; a stale list whose star hits
the new 409 shows the API message and refreshes; dropping several files selects only the first; a
selected but not uploaded file keeps the KTL-29 dirty protection; a primary file that the scan
later refuses stays primary until another one is starred.

Success criteria: the 12 acceptance scenarios of the enhanced brief — aligned action columns at
1280px and 390px without horizontal scroll, chips only for unavailable states, no native file-input
text, the 409 refusal for every unavailable state with the primary unchanged, and `npm run lint`,
`npm run format:check`, `npm test` and `npm run test:backend` passing.

Personal data and security: documents and their filenames are personal data. Filenames appear only
in the DOM (text, `title`, `aria-label`) and are never logged or placed in error messages. The API
change tightens a write: the `documents.upload` guard still runs before the document is read, so an
unauthorized caller learns nothing about its state, and the 409 body carries no storage key, path
or scanner detail (principles 1 and 3). No RLS policy, grant, storage path, table or role changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `candidate-profile-pages`: in the Documentos panel, mark-primary is offered only for available
  documents, a state is shown only for documents that are not available, and the upload control
  is a «Subir documento» subsection with a drop area and a selected-file card.
- `candidate-management`: designating a document primary is refused with a stable code when the
  document is not available; upload may still designate a pending document primary.
- `frontend-api-transport`: a document that settles as available is shown without a state label
  and with a download, instead of being labelled «disponible».

## Impact

- Frontend: `features/candidates/components/candidate-documents.tsx`, new
  `candidate-documents.logic.ts` and `candidate-documents.css`; new shared
  `shared/components/icons.tsx` (download icon moved from `row-cv-preview.tsx`, plus file, star,
  bin, upload and close); `styles.css` (`.item-main`); `assets/i18n/es.json` (and `en.json`).
- Backend: `Application/Features/Documents/ManageCandidateDocuments.cs` (availability check in
  `SetPrimaryCandidateDocumentHandler`) and `DocumentContract.cs` (exception factory for the
  existing `document.not_available` code). No endpoint, table, migration or grant change.
- Tests: unit `candidate-documents.spec.tsx` and a new logic spec; e2e
  `candidate-documents.spec.ts`, `secure-access.spec.ts`, `security-ops.spec.ts` (state through
  `data-state`); xUnit `CandidateApiTests.cs` (new refusal test; the existing swap test marks its
  document clean first).
- Docs: `docs/ktl-9/documents.md` (409 result, UI states table), `docs/CORPORATE_IDENTITY_Kepler.md`
  (document chips).
- No new runtime dependency.
