# KTL-39 — Documentos panel layout

**Status:** Draft
**Depends on:** KTL-29 (per-panel edit mode), KTL-38 (status chips with semantic tones)

## [original]

## Summary

Rebuild the layout of the Documentos panel on the candidate page so its rows line up, its chips
carry information, and its upload form matches the other panels. It is currently the worst-looking
panel on the page, in both read-only and edit mode.

Problems seen on the running app (`candidate-documents.tsx`):

1. **Chips stretch into bars.** `.item-main` is `display: grid` (`frontend/src/styles.css`), so each
   `.badge` is a grid item stretched to the width of the filename. «Principal» and «Disponible»
   stack under the name as long pills.
2. **Buttons do not line up.** `.item-row` uses `justify-content: space-between`, which spreads one
   to three buttons across the row. Rows with different actions put «Descargar» at different
   positions, so nothing forms a column.
3. **The chips add little.** Every row shows a «CV» type chip, because the UI always uploads with
   type `CV`. The document «Disponible» chip uses the same word as the candidate's «Disponible»
   availability chip (KTL-38) on the same page, with a different meaning.
4. **Edit mode is noisy.** Each row has three differently coloured buttons (navy «Descargar», white
   «Marcar principal», red «Eliminar»). «Marcar principal» is offered on pending, refused and
   legacy documents, which can leave the candidate with a primary CV that cannot be opened.
5. **The upload form looks unfinished.** The native file input shows the browser's own text
   («Choose File / No file chosen»), in the browser language rather than Spanish. The «Marcar como
   CV principal» checkbox floats at the top of its grid cell. «Subir CV» and the panel's «Hecho»
   are stacked as two primary buttons.

This is a presentation change, plus one rule about which documents can be marked primary (see
change 3). Storage, scanning, permissions and the other API contracts do not change.

## Specs updated

- `openspec/specs/candidate-profile-pages/spec.md`, requirement _Notes and documents act
  immediately in edit mode_:
  - A document can be marked primary only when it is available (clean, binary present).
  - The document list shows a state chip only for documents that are not available.

## Changes

### 1. Document row layout

Replace the `.item-row` markup with a document row on a fixed grid of four columns:

| Column  | Content                                                                              |
| ------- | ------------------------------------------------------------------------------------ |
| Icon    | A 16px file icon in `--fg-3`.                                                        |
| Main    | The filename on one line (ellipsis, full name in `title`), then a 12px details line. |
| State   | The state chip, only when the document is not available (change 2).                  |
| Actions | Icon buttons in fixed slots (change 3).                                              |

- «Principal» sits on the filename line as a plain `.badge`. It is the only orange chip in the row.
- The details line reads `{format} · {size} · {date}`, e.g. «PDF · 240 KB · 12 sept 2026», built
  from `originalFilename`/`mimeType`, `sizeBytes` and `uploadedAt`. Use `formatDate` and
  `formatNumber`; the sentence is one interpolated i18n key, not concatenated fragments.
- For a document that is not available, the explanation replaces the details line («No ha superado
  el análisis de seguridad.», «Documento heredado sin archivo asociado.», «No se ha podido
  analizar el archivo. Inténtalo de nuevo.»).
- The «CV» type chip is removed. A document whose type is not `CV` shows the type in the details
  line.
- Below 520px of panel width, the state chip and the actions move to a second line under the name.
  Use a container query on the panel, not a viewport media query or `matchMedia`; the shell's
  single 768px breakpoint stays in `primary-nav.css`.
- The list is a `<ul>` of `<li>` rows. The rows keep `data-testid="candidate-document"`.

### 2. State chips by exception

Use the KTL-38 tones through one helper in a new `candidate-documents.logic.ts`
(`documentStateChip(state)`), not inline conditionals:

| `availabilityState` | Chip                | Tone    |
| ------------------- | ------------------- | ------- |
| `Available`         | none                | —       |
| `Pending`           | «En análisis»       | neutral |
| `Error`             | «Error de análisis» | danger  |
| `Refused`           | «Rechazado»         | danger  |
| `LegacyUnavailable` | «Sin archivo»       | neutral |

- An available document shows no chip; the download button signals that it can be used. This
  removes the clash with the candidate availability chip.
- Every row keeps an element with `data-testid="document-availability"` carrying the state in a
  `data-state` attribute, so tests can assert the state whether or not a chip is drawn.
- When scan polling gives up, the «El análisis sigue en curso» message and its «Actualizar» button
  move into the pending row, instead of below the list.

### 3. Row actions as icon buttons in fixed slots

| Slot     | Mode       | Shown when                                  | Icon     |
| -------- | ---------- | ------------------------------------------- | -------- |
| Download | read, edit | `documents.download` and document available | download |
| Primary  | edit       | `documents.upload`, not primary, available  | star     |
| Remove   | edit       | `documents.upload`                          | bin      |

- Buttons are `.button.ghost` icon buttons, 32×32. A slot whose action does not apply renders an
  empty 32px placeholder, so the columns line up across rows.
- Every button has an `aria-label` and a `title` naming the file («Descargar cv.pdf», «Marcar
  cv.pdf como CV principal», «Eliminar cv.pdf»). Existing tests that find «Descargar» by
  accessible name keep matching.
- The remove button turns red on hover. It keeps the existing confirmation dialog.
- `DownloadIcon` moves from `row-cv-preview.tsx` to a shared icons module
  (`frontend/src/app/shared/components/icons.tsx`), alongside new file, star, bin, upload and close
  icons drawn the same way (16px grid, 1px strokes on half-pixel coordinates).
- **Primary only for available documents.** The star is not offered for pending, refused, error or
  legacy documents. The API enforces the same rule: `SetPrimaryAsync` refuses a document that is
  not clean or has no binary, with a stable error code mapped through `GlobalExceptionHandler`
  (fail closed). Hiding the star is not the control.

### 4. Upload as a «Subir documento» subsection

In edit mode, the upload form sits below the list, separated the same way as «Nueva experiencia»
(`.candidate-add-form`: top rule, `h3` title «Subir documento»).

- **No file selected:** a dashed drop area with an upload icon, «Arrastra aquí un archivo o
  selecciónalo», a hint «PDF, Word, ODT, RTF, TXT o imagen · máximo 20 MB», and a ghost
  «Seleccionar archivo» button. Dropping a file on the area selects it.
- The native `<input type="file">` is visually hidden (still focusable through the button/label)
  and keeps `name="file"`, `data-testid="document-file"` and the `accept` list, so Playwright's
  `setInputFiles` keeps working. The browser's untranslated «Choose File / No file chosen» text no
  longer appears.
- **File selected:** the drop area is replaced by a card with the file icon, name and size, a ✕
  button («Quitar archivo») that clears the selection, the «Marcar como CV principal» checkbox
  (`data-testid="document-is-primary"`, checked by default as today) and the «Subir documento»
  button (`data-testid="document-upload"`), all inside the card. The panel footer keeps only
  «Hecho».
- The selected-file card is the panel's unsaved state: `onDirtyChange` reports it as today.
- Upload errors (`document-upload-error`) render inside the subsection, above the card.

### 5. Copy

New and changed keys under `candidate.profile.documents.*` in `es.json`: details line, the new
chip labels («Rechazado», «Sin archivo»), the per-file action labels, the upload subsection title,
drop text, hint, «Seleccionar archivo», «Quitar archivo» and «Subir documento». Remove keys that
are no longer used (`state.available`, `state.unavailable`, `markPrimary`, `upload`, `file` as
applicable). Attribute copy (`aria-label`, `title`) is translated too.

## Out of scope

- The CV preview column (`candidate-cv-preview.tsx`) and its document selector.
- Selecting a document in the preview by clicking its name in the list (possible follow-up).
- Upload on file selection without a confirm step, or changing the default of «Marcar como CV
  principal».
- The same `.item-main` stretching in Experiencia and Formación. It probably affects them too, but
  it was not reproduced (no data in the development database); track separately.
- Any change to storage, scanning, download or permissions.

## Acceptance criteria

- In read-only mode each document is one row: file icon, filename with «Principal» when primary,
  a details line, a state chip only when the document is not available, and a download icon button
  when it can be downloaded. Chips are sized to their label.
- In edit mode the download, primary and remove buttons sit in the same columns on every row,
  whatever actions a row has.
- Pending documents show a neutral «En análisis» chip, refused and error documents a danger chip,
  legacy documents a neutral «Sin archivo» chip. Available documents show no chip.
- The star is offered only on available, non-primary documents, and the API refuses to mark an
  unavailable document primary, with a backend test for each unavailable state.
- The upload subsection shows no native file-input text. A selected file appears in a card with
  name, size, clear button, checkbox and «Subir documento»; the panel footer shows only «Hecho».
  Dropping a file on the area selects it.
- At 390px and 1280px the panel has no horizontal scroll, and state and actions wrap under the name
  on narrow panels.
- All existing `data-testid` and `name=` attributes stay. New e2e selectors hardcode no Spanish
  text.
- Tests updated: `tests/unit/candidate-documents.spec.tsx` (row content, chip by state, action
  slots per mode and permission, upload card and dirty state, drop), `tests/e2e/candidate-documents.spec.ts`
  (state through `data-state` instead of the «Disponible» text), and a backend test for the
  set-primary refusal.
- `npm run lint`, `npm run format:check`, `npm test` and `npm run test:backend` pass.

## Open questions

- Should the API refusal to mark an unavailable document primary be part of this ticket or a
  separate backend ticket? Proposed: this ticket, so the UI rule is never the only control.
- Icon buttons or text buttons for the row actions? Proposed: icons, matching the CV column of the
  candidates list and keeping the columns narrow; text buttons would need fixed widths.
- Is drag and drop worth keeping? Proposed: yes; it is a few lines and CVs often arrive as email
  attachments.

## [enhanced]

### User story

As a **recruiter managing a candidate's documents** (`documents.upload`), I want the Documentos
panel to show each file as one tidy row, with its state only when something is wrong and its
actions always in the same place, so that I can find, download, replace or clean up a CV at a
glance and cannot make an unusable file the candidate's primary CV.

As a **reader** (`documents.download` only), I want the read-only panel to show which file is the
primary CV, when each one was received, and a download button for the usable ones.

### Resolved decisions

| Question                               | Decision                                                                                                                                                                                                                    |
| -------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Primary only for available documents   | **The API refuses** `PUT …/primary` for a document that is not available (409, `document.not_available`). Upload with «Marcar como CV principal» is **unchanged**: a file still being scanned can become primary on upload. |
| Row actions                            | **Icon buttons** (download, star, bin) with `aria-label` and `title` naming the file.                                                                                                                                       |
| Drag and drop                          | **Included**: dropping a file on the upload area selects it.                                                                                                                                                                |
| «Disponible» chip                      | **Hidden** for available documents; chips only for exceptions.                                                                                                                                                              |
| Experiencia and Formación `.item-main` | **Included**: fix the shared rule so chips size to their label in those panels too.                                                                                                                                         |

"Available" means exactly what the API already reports as `availabilityState: "Available"` and
what the domain exposes as `CandidateDocument.CanBeDownloaded`: a `Clean` scan and a binary that
exists (not a legacy record without one). No new state is introduced.

### Functional behaviour

**Read-only (all holders of `candidates.read`; download needs `documents.download`)**

- Documents are listed in the order the API returns them (unchanged), one row each: file icon,
  filename (one line, ellipsis, full name in `title`), «Principal» chip when primary, and a
  details line.
- Details line: `{FORMAT} · {size} · {date}` («PDF · 240 kB · 12 sept 2026»). Format is the upper-
  case extension of `originalFilename`, size is `sizeBytes` formatted with `formatNumber` in kB or
  MB (`style: 'unit'`, no hardcoded units), date is `uploadedAt` with `formatDate`
  (`dateStyle: 'medium'`). A `documentType` other than `CV` is prefixed («Carta · PDF · …»).
- A document that is not available shows its chip (table below) and its explanation in place of
  the details line.
- A download icon button is shown when the actor holds `documents.download` and the document is
  available.
- No documents: the existing empty state «Sin CV adjunto.» stays.

**Edit mode («Editar», `documents.upload`)**

- Every row gets three fixed 32px action slots: download, mark primary, remove. A slot whose
  action does not apply is an empty placeholder, so the buttons form columns across rows.
- Mark primary (star) is offered only on documents that are available and not primary. Activating
  it calls the existing `PUT …/primary`, refreshes the list and shows «CV principal actualizado.»
  (unchanged). If the API refuses with `document.not_available` (the list was stale), the error
  toast shows the API message and the list is refreshed.
- Remove (bin) keeps the existing danger confirmation and toasts.
- «Hecho» in the panel footer returns to read-only (unchanged).

**Upload subsection (edit mode only)**

- Below the list, a `.candidate-add-form` block titled «Subir documento».
- No file selected: a dashed area with an upload icon, «Arrastra aquí un archivo o selecciónalo»,
  the hint «PDF, Word, ODT, RTF, TXT o imagen · máximo 20 MB» and a ghost «Seleccionar archivo»
  button that opens the native picker. Dragging a file over the area highlights it; dropping it
  selects the first file. Dropping several files selects only the first (no batch upload).
- File selected: a card with file icon, name, size, a ✕ icon button («Quitar archivo»), the
  «Marcar como CV principal» checkbox (checked by default, as today) and the «Subir documento»
  primary button. Client checks (extension, empty, 20 MB) still run in `DocumentService.upload`
  when uploading, as today.
- On success: the card resets to the empty area, the toast «Archivo aceptado. El análisis de
  seguridad está en curso.» shows, and the new row appears with «En análisis» (unchanged flow).
- Errors (`document-upload-error`) render inside the subsection above the card.
- The selected file is the panel's unsaved state, reported through `onDirtyChange` as today, so
  the KTL-29 discard and leave confirmations keep working.

**State chips** (KTL-38 tones, one helper `documentStateChip(state)` in
`candidate-documents.logic.ts`)

| `availabilityState` | Chip                | Tone    | Details line replaced by                                   |
| ------------------- | ------------------- | ------- | ---------------------------------------------------------- |
| `Available`         | —                   | —       | —                                                          |
| `Pending`           | «En análisis»       | neutral | — (details line stays)                                     |
| `Error`             | «Error de análisis» | danger  | «No se ha podido analizar el archivo. Inténtalo de nuevo.» |
| `Refused`           | «Rechazado»         | danger  | «El archivo no ha superado el análisis de seguridad.»      |
| `LegacyUnavailable` | «Sin archivo»       | neutral | «Documento heredado sin archivo asociado.»                 |

When scan polling gives up, «El análisis sigue en curso.» and the «Actualizar» button render in
the pending row's details line instead of below the list.

### API contract change

Only one endpoint changes; no new routes, tables, migrations or grants.

| Endpoint                                                   | Change                                                                                                     |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `PUT /api/candidates/{candidateId}/documents/{id}/primary` | New refusal: **409** ProblemDetails, `code: "document.not_available"`, when the document is not available. |

- `backend/Application/Features/Documents/ManageCandidateDocuments.cs`,
  `SetPrimaryCandidateDocumentHandler`: after the existing `DocumentErrors.Require(actor,
Permissions.DocumentsUpload)` guard, load the document with `documents.FindAsync`; missing →
  `DocumentErrors.Missing()` (404, unchanged); `!document.CanBeDownloaded` → throw the new
  `DocumentErrors.NotAvailableException()`; only then call `SetPrimaryAsync`.
- `DocumentContract.cs`: add `NotAvailableException()` returning
  `new ConflictException(NotAvailable, "Solo un documento disponible puede ser el CV principal.")`,
  reusing the existing, so far unused, `NotAvailable` code. `GlobalExceptionHandler` maps it to 409.
- A refused designation writes no audit event and changes nothing, like the not-found case.
- `DocumentEndpoints.cs`: the endpoint already declares `ProducesProblem(409)`; no change.
- Upload (`UploadCandidateDocument.cs`) is not changed.
- Order stays fail-closed: authentication and `documents.upload` are checked before the document
  is read, so an unauthorized caller learns nothing about the document's state.

### Files to change

| Area     | File                                                                                        | Change                                                                                                                                                        |
| -------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Frontend | `frontend/src/app/features/candidates/components/candidate-documents.tsx`                   | New row, action-slot and upload markup; drop handling; keeps polling, dirty and toast logic.                                                                  |
| Frontend | `…/candidates/components/candidate-documents.logic.ts` (new)                                | `documentStateChip`, `documentDetails` (format, size, type), `canMarkPrimary`; pure, unit-tested.                                                             |
| Frontend | `…/candidates/components/candidate-documents.css` (new)                                     | Row grid, action slots, container query at 520px, drop area, selected-file card. Plain CSS, Kepler tokens only.                                               |
| Frontend | `frontend/src/app/shared/components/icons.tsx` (new)                                        | `DownloadIcon` moved from `row-cv-preview.tsx`, plus file, star, bin, upload and close icons (16px grid, 1px strokes on half-pixel coordinates).              |
| Frontend | `…/candidates/components/row-cv-preview/row-cv-preview.tsx`                                 | Import `DownloadIcon` from the shared module.                                                                                                                 |
| Frontend | `frontend/src/styles.css`                                                                   | `.item-main`: inline flow (`display: flex; flex-wrap: wrap; align-items: center; gap: 4px 8px; margin: 0`) so chips size to content in Experiencia/Formación. |
| Frontend | `frontend/src/assets/i18n/es.json` (and `en.json`, optional)                                | Keys below; remove unused ones.                                                                                                                               |
| Backend  | `backend/Application/Features/Documents/ManageCandidateDocuments.cs`, `DocumentContract.cs` | Availability check and new exception factory.                                                                                                                 |

No new npm or NuGet dependency. The service layer (`DocumentService`) and `api-transport.ts` do not
change; drag and drop uses native `dragover`/`drop` events.

Attributes that stay: `data-testid` `candidate-documents`, `candidate-document`,
`document-availability` (now on every row, with `data-state="{availabilityState}"`; its text is the
chip label or empty), `document-file`, `document-is-primary`, `document-upload`,
`document-upload-error`; `name="file"` and `name="isPrimary"`. New: `document-drop-zone`,
`document-selected`, `document-clear`, `document-mark-primary`, `document-remove`,
`document-download`.

### Copy (`candidate.profile.documents.*`)

| Key                  | Spanish                                          |
| -------------------- | ------------------------------------------------ |
| `details`            | `{{format}} · {{size}} · {{date}}`               |
| `detailsWithType`    | `{{type}} · {{format}} · {{size}} · {{date}}`    |
| `state.refused`      | Rechazado                                        |
| `state.legacy`       | Sin archivo                                      |
| `action.download`    | Descargar {{name}}                               |
| `action.markPrimary` | Marcar {{name}} como CV principal                |
| `action.remove`      | Eliminar {{name}}                                |
| `upload.title`       | Subir documento                                  |
| `upload.drop`        | Arrastra aquí un archivo o selecciónalo          |
| `upload.hint`        | PDF, Word, ODT, RTF, TXT o imagen · máximo 20 MB |
| `upload.choose`      | Seleccionar archivo                              |
| `upload.clear`       | Quitar archivo                                   |
| `upload.submit`      | Subir documento                                  |

Kept: `title`, `empty`, `primary`, `state.pending`, `state.error`, `explanation.*`, `isPrimary`,
the toast and failure messages, `removeTitle`/`removeMessage`, `stillScanning`, `refresh`.
Removed when unused: `state.available`, `state.unavailable`, `download`, `markPrimary`, `remove`,
`file`, `upload`. The API message for `document.not_available` is Spanish and shown as returned.

### Acceptance criteria

1. **Read-only row.** Given a candidate with a primary available PDF, when a reader with
   `documents.download` opens the page, then the Documentos row shows the filename, «Principal»,
   «PDF · {size} · {date}», no state chip, and one button named «Descargar {filename}».
2. **Exception chips.** Given documents in each state, then `Pending` shows a neutral «En
   análisis», `Error` a danger «Error de análisis», `Refused` a danger «Rechazado» and
   `LegacyUnavailable` a neutral «Sin archivo»; `Available` shows none. Each row's
   `document-availability` carries the matching `data-state`.
3. **No download without permission.** Given a reader without `documents.download`, no download
   button is rendered (unchanged rule).
4. **Aligned actions.** Given a manager in edit mode with an available primary, an available
   non-primary and a pending document, then each row has three action slots of equal width at the
   same horizontal positions; the star appears only on the available non-primary row.
5. **Mark primary.** When the manager activates the star on an available document, then it becomes
   primary, the previous primary loses «Principal», and «CV principal actualizado.» is shown.
6. **API refuses unavailable primary.** Given a document in `PendingScan`, `Infected`, `Rejected`
   or `ScanFailed`, or a legacy document without binary, when an actor with `documents.upload`
   calls `PUT …/primary`, then the response is 409 with `code: "document.not_available"` and the
   candidate's primary document is unchanged.
7. **Fail closed.** Unauthenticated and authenticated-without-`documents.upload` callers are
   refused exactly as today, for both available and unavailable documents, without revealing the
   document state (the guard runs before the document is read).
8. **Upload area.** In edit mode, the panel shows «Subir documento» with the drop area and no native
   file-input text. Selecting or dropping a file replaces the area with the card (name, size, ✕,
   checkbox checked, «Subir documento»); ✕ restores the area and clears the dirty state.
9. **Upload as primary unchanged.** When a file is uploaded with «Marcar como CV principal»
   checked, it is listed as primary with «En análisis» straight away.
10. **Dirty guard.** With a file selected and not uploaded, clicking «Editar» on another panel or
    leaving the page asks for confirmation (KTL-29 behaviour).
11. **Responsive.** At 1280px and 390px the page has no horizontal scroll; below 520px of panel
    width the chip and actions sit on a second line under the filename.
12. **Sibling panels.** In Experiencia and Formación, the position/degree chip is sized to its
    label and sits on the same line as the company/institution text.

### Tests

- **Unit (Vitest)**
  - `frontend/tests/unit/candidate-documents.logic.spec.ts` (new): chip per state, details with and
    without type, size formatting boundaries (bytes → kB → MB), `canMarkPrimary`.
  - `frontend/tests/unit/candidate-documents.spec.tsx`: update name matchers to the per-file labels
    (RTL matches names exactly); cover criteria 1–5, 8–9, the upload error inside the subsection,
    drop via `fireEvent.drop` with `dataTransfer.files`, dirty reporting, and the refreshed list
    after a `document.not_available` refusal. Keep the abort-on-unmount test.
  - `frontend/tests/unit/candidate-cv-preview.spec.tsx` and the row CV button: unchanged behaviour
    after moving `DownloadIcon`.
- **E2e (Playwright)**
  - `frontend/tests/e2e/candidate-documents.spec.ts`, `secure-access.spec.ts` and
    `security-ops.spec.ts`: replace `toHaveText('Disponible')` / `toHaveText('En análisis')` on
    `document-availability` with `toHaveAttribute('data-state', 'Available' | 'Pending')`. Button
    lookups by name «Descargar» keep working (Playwright matches substrings). Keep the 1280/390
    no-horizontal-scroll checks. New selectors use test ids, not Spanish text.
- **Backend (xUnit)**
  - `backend/Tests/IntegrationTests/CandidateApiTests.cs`: new test
    `Marking_an_unavailable_document_primary_is_refused` covering pending, refused, scan-failed and
    legacy-without-binary documents (409, code, primary unchanged, no `document.primary.changed`
    audit). Update `Marking_a_second_uploaded_document_primary_demotes_the_first`: the second
    upload is never scanned in tests, so mark it clean (`MarkClean`) through the test
    `DbContext` before the `PUT`. `Concurrent_primary_designations_…` writes through the context
    and needs no change.
  - Unauthorized/forbidden cases for `PUT …/primary` (criterion 7) if not already covered.

### Documentation and specs

- `openspec/specs/candidate-profile-pages/spec.md`, _Notes and documents act immediately in edit
  mode_: mark-primary offered only for available documents; state shown only for documents that
  are not available. Add scenarios for criteria 4 and 8.
- `openspec/specs/candidate-management/spec.md`, _Candidate document metadata_: new scenario «An
  unavailable document is marked primary» → refused with a stable code, primary unchanged; note
  that upload may still designate a pending document primary.
- `docs/ktl-9/documents.md`: add the 409 `document.not_available` result to the `PUT …/primary`
  row, and update the «Estados para la interfaz» table to the new chip texts (keep that file's
  existing language).
- `docs/CORPORATE_IDENTITY_Kepler.md`, _Badges y estados_: document chips follow the exception
  rule and their tones.

### Non-functional requirements

- **Security:** the API, not the hidden star, enforces the primary rule; guards run before the
  document is loaded. No storage key, path or scanner detail appears in the new 409 body.
- **Personal data:** filenames can contain candidate names. They appear only in the DOM (text,
  `title`, `aria-label`); they are never logged or added to API error messages.
- **Accessibility:** icon buttons have accessible names including the filename and a visible
  focus ring; the drop area is not the only way in (the button opens the picker); chips keep a text
  label, never colour alone; the list is a `<ul>`.
- **Responsive:** container query at 520px panel width; no `matchMedia` or `window.innerWidth`; the
  shell breakpoint in `primary-nav.css` is untouched.
- **Performance:** no extra requests; the list and polling calls stay as today.

### Out of scope

- Upload semantics: a pending file can still become primary on upload, and a primary file that the
  scan later refuses stays primary until the manager stars another one.
- Changing the default of «Marcar como CV principal», uploading on selection, or batch upload.
- The CV preview column and selecting a document in it from the list.
- Other `.item-row` users (Notas) and other chips outside Documentos, Experiencia and Formación.
