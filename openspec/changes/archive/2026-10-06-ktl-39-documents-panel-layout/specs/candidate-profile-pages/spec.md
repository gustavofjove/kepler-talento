## MODIFIED Requirements

### Requirement: Notes and documents act immediately in edit mode

Notas and Documentos SHALL show their add, edit, retire, upload, mark-primary and remove controls
only in edit mode. Each of those actions SHALL persist as soon as it is performed, as before, with
its existing confirmations. «Hecho» SHALL return the panel to read-only. Document download and the
CV preview SHALL be available in both modes to holders of the document download permission.
Mark-primary SHALL be offered only for a document that is available and not already primary.

#### Scenario: Note added in edit mode

- **WHEN** an editor activates «Editar» on Notas, adds a note and activates «Hecho»
- **THEN** the note persisted when it was added, and the panel shows it read-only

#### Scenario: Document uploaded in edit mode

- **WHEN** a document manager activates «Editar» on Documentos and uploads a file
- **THEN** the upload is sent at once and the document is listed with its scan state

#### Scenario: Download while read-only

- **WHEN** a user holding the document download permission views Documentos read-only
- **THEN** a clean document can be downloaded and previewed

#### Scenario: Mark-primary only on available documents

- **WHEN** a document manager opens Documentos in edit mode for a candidate with an available
  primary document, an available non-primary document, and documents that are pending, refused,
  failed or legacy without a file
- **THEN** mark-primary is offered only on the available non-primary document

#### Scenario: Mark-primary refused by the API

- **WHEN** a document manager marks a document primary and the API refuses it because the document
  is no longer available
- **THEN** the API's Spanish message is shown, the primary document is unchanged, and the list is
  refreshed

## ADDED Requirements

### Requirement: Documentos lists each document as one aligned row

The Documentos panel SHALL list each document as one row showing its filename on a single line
(truncated with the full name available on hover), the «Principal» chip when it is the primary
document, and a details line with its format, size and upload date formatted for Spanish. A
document type other than CV SHALL be shown in the details line, not as a chip. The row's actions
SHALL be icon buttons whose accessible names include the filename, placed in fixed positions so
that each action lines up across rows whatever actions a row offers: download in both modes, and
mark-primary and remove in edit mode only.

#### Scenario: Read-only row of an available primary document

- **WHEN** a user holding the document download permission views Documentos for a candidate whose
  primary document is an available PDF
- **THEN** its row shows the filename, «Principal», a details line with «PDF», the size and the
  upload date, no state chip, and one download button named after the file

#### Scenario: No download without the permission

- **WHEN** a user without the document download permission views Documentos
- **THEN** no row offers a download

#### Scenario: Actions line up in edit mode

- **WHEN** a document manager opens Documentos in edit mode with an available primary document,
  an available non-primary document and a pending document
- **THEN** every row reserves the same action positions, so each download, mark-primary and remove
  button sits at the same horizontal position as the same action in the other rows

#### Scenario: Narrow panel

- **WHEN** the Documentos panel is narrower than 520px, including on a 390px-wide screen
- **THEN** each row's state chip and actions move to a line below the filename, and the page has no
  horizontal scroll

### Requirement: Documentos shows a state only for documents that are not available

A document that is available SHALL show no state chip. A document that is not available SHALL show
a chip in the semantic tone of its state — «En análisis» (neutral) while pending, «Error de
análisis» (danger) when the scan failed, «Rechazado» (danger) when refused, and «Sin archivo»
(neutral) for a legacy record without a file — and, except while pending, its Spanish explanation
in place of the details line. When observation of a pending document gives up, the notice that the
analysis is still running and its refresh action SHALL be shown in that document's row.

#### Scenario: Chip per state

- **WHEN** Documentos lists one document in each availability state
- **THEN** the pending, failed, refused and legacy documents show «En análisis», «Error de
  análisis», «Rechazado» and «Sin archivo» respectively, and the available document shows no chip

#### Scenario: Refused document explains itself

- **WHEN** Documentos lists a refused document
- **THEN** its row shows «Rechazado» and the explanation that the file did not pass the security
  analysis, and offers no download

#### Scenario: Analysis still running

- **WHEN** observation of a pending document stops before the scan settles
- **THEN** the pending document's row shows that the analysis is still running and a refresh
  action

### Requirement: Documentos upload is a «Subir documento» subsection

In edit mode, Documentos SHALL offer upload in a «Subir documento» subsection below the list. With
no file selected it SHALL show an area that accepts a dropped file, a button to choose a file, and
a hint with the accepted formats and the 20 MB limit, and SHALL NOT show the browser's own file
input text. With a file selected it SHALL show the file's name and size, an action to clear the
selection, the «Marcar como CV principal» option (selected by default) and the «Subir documento»
action. The panel footer SHALL keep only «Hecho».

#### Scenario: Choosing a file

- **WHEN** a document manager in edit mode chooses a file with the file button
- **THEN** the drop area is replaced by the selected file's name and size, the primary option,
  selected, and «Subir documento», and the panel reports unsaved changes

#### Scenario: Dropping a file

- **WHEN** a document manager drops one or more files on the upload area
- **THEN** the first file is selected exactly as if it had been chosen with the file button

#### Scenario: Clearing the selection

- **WHEN** a document manager clears the selected file
- **THEN** the drop area returns and the panel no longer reports unsaved changes

#### Scenario: Upload as primary

- **WHEN** a document manager uploads a selected file with «Marcar como CV principal» selected
- **THEN** the file is listed at once as the primary document in its «En análisis» state, and the
  drop area returns

### Requirement: Entry chips in Educación and Experiencia fit their label

In Educación and Experiencia, the chip that names each entry's degree or position SHALL be sized
to its label and SHALL sit on the same line as the rest of the entry's text when it fits.

#### Scenario: Experience entry

- **WHEN** Experiencia lists an entry with a position, company and sector
- **THEN** the position chip is as wide as its label and is followed on the same line by the
  company and sector
