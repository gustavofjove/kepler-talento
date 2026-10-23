## MODIFIED Requirements

### Requirement: Position page lists its candidates

When the actor holds `candidates.read`, the position detail page SHALL show a «Candidatos de la
posición» panel above «Candidatos que encajan». It SHALL list each link with the same columns as
the matches, except that the stage replaces the candidate's availability and the date added
replaces the update date:

- name with e-mail;
- phone;
- the stage;
- the date added;
- for managers on an open position, a labelled stage selector and «Quitar de la posición»;
- last, the «CV» column with the download and «Ver» buttons, as the data-tables row CV preview
  specifies.

Both tables open the candidate when a row is clicked outside its links and controls. The name is
the row's keyboard-reachable link, and phones are plain text.

Removal SHALL ask for confirmation. A stage change SHALL save immediately, and a concurrency
conflict SHALL be reported and the list reloaded. A saved stage change SHALL keep the link's row
where it stands, so no row moves under the user; the list SHALL return to the API order on its next
load. Managers on an open position SHALL have «Añadir candidato», which opens a picker searching
candidates by text through the existing candidate search and shows already-linked candidates as not
selectable. The panel SHALL show localized loading, empty and error states. Without
`candidates.read`, no link request SHALL be sent.

#### Scenario: Candidate is added through the picker

- **WHEN** a manager searches the picker for a non-matching candidate and selects them
- **THEN** the candidate appears in «Candidatos de la posición» at stage Nuevo

#### Scenario: Restaged candidate keeps its row

- **WHEN** a manager changes the first of two «Nuevo» candidates to «Descartado»
- **THEN** after the change is saved that candidate is still the first row, and reloading the page
  lists it after the «Nuevo» candidate

#### Scenario: Closed position is viewed

- **WHEN** a manager opens a closed position
- **THEN** its links are listed read-only with no stage selector, removal or add control, and a hint explains that reopening allows changes

#### Scenario: Removal is cancelled

- **WHEN** a manager activates «Quitar de la posición» and cancels the confirmation
- **THEN** no request is sent and the link remains

#### Scenario: Linked candidate's CV is shown

- **WHEN** a reader holding the document download permission activates «Ver» on a linked
  candidate whose primary CV can be previewed
- **THEN** the CV is shown on the position page and any CV open in either table closes

#### Scenario: Reader without candidate permission

- **WHEN** an actor with `positions.read` but without `candidates.read` opens a position
- **THEN** no link request is made and no candidate name or count is shown

#### Scenario: Controls are accessible

- **WHEN** the panel and picker are used by keyboard at 390 pixels wide
- **THEN** every selector and button has a programmatic label, the dialog returns focus on close, and the page does not scroll horizontally
