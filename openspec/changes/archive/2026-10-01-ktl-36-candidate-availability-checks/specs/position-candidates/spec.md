## MODIFIED Requirements

### Requirement: Stage vocabulary

The stage SHALL be exactly one of `new`, `shortlisted`, `interview`, `hired` or `rejected`, presented
in that order and labelled Nuevo, Preseleccionado, Entrevista, Contratado and Descartado. An authorized
manager SHALL be able to change any stage to any other. A stage change SHALL require the link's
last-read version. The stage SHALL be the only pipeline state a candidate has: there is no
candidate-level status. Changing a stage SHALL NOT change the candidate's availability check, and
recording an availability check SHALL NOT change any stage.

#### Scenario: Stage is changed

- **WHEN** an authorized manager changes a link from `new` to `interview` with its current version
- **THEN** the new stage is stored with a new version and the candidate's availability check is
  unchanged

#### Scenario: Unknown stage is submitted

- **WHEN** a stage change contains a value outside the vocabulary
- **THEN** the request is rejected with a stable validation problem and the link is unchanged

#### Scenario: Stale stage change

- **WHEN** two managers hold the same link version and the second changes the stage after the first
- **THEN** the second receives a stable concurrency conflict and the first stage remains stored

#### Scenario: Candidate is rejected for a position

- **WHEN** a link's stage is set to `rejected`
- **THEN** the link remains and is still listed for the position and the candidate

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
conflict SHALL be reported and the list reloaded. Managers on an open position SHALL have «Añadir
candidato», which opens a picker searching candidates by text through the existing candidate search
and shows already-linked candidates as not selectable. The panel SHALL show localized loading,
empty and error states. Without `candidates.read`, no link request SHALL be sent.

#### Scenario: Candidate is added through the picker

- **WHEN** a manager searches the picker for a non-matching candidate and selects them
- **THEN** the candidate appears in «Candidatos de la posición» at stage Nuevo

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
