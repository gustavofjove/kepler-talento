## MODIFIED Requirements

### Requirement: Position candidate API

The system SHALL expose:

- `GET /api/positions/{id}/candidates`: the complete link list of a position;
- `POST /api/positions/{id}/candidates`: add a candidate by id;
- `PUT /api/positions/{id}/candidates/{candidateId}/stage`: change the stage with a version;
- `DELETE /api/positions/{id}/candidates/{candidateId}`: remove a link;
- `GET /api/candidates/{candidateId}/positions`: the complete link list of a candidate.

A position's list SHALL order links by stage vocabulary order, then by time added descending, then
by candidate id. Each of its items SHALL contain only the following fields, which are the contact
and CV fields that candidate search already shows to the same readers:

- the candidate id, first name, last name, e-mail and phone;
- whether a primary CV exists;
- whether the primary CV can be previewed, and whether it can be downloaded, under the same rules
  and the same download-permission masking as candidate search;
- the candidate active flag;
- the stage, the added and updated times, and the version.

A candidate's list SHALL order links to open positions before links to closed positions, each by
time added descending. Each of its items SHALL contain only the position id, title, position
status, stage, added and updated times and version.

No list item SHALL contain documents, document identifiers, notes, other candidate fields, position
description or position requirements, and a candidate's list SHALL contain no candidate contact
details.

#### Scenario: Position list is read

- **WHEN** an authorized reader requests an existing position's candidates
- **THEN** every link is returned in the specified order with only the specified fields

#### Scenario: Previewable flag follows the download permission

- **WHEN** an authorized reader without the document download permission requests a position's
  candidates, one of whom has a clean PDF primary CV
- **THEN** every item reports that the primary CV can be neither previewed nor downloaded

#### Scenario: Candidate list is read

- **WHEN** an authorized reader requests an existing candidate's positions
- **THEN** open-position links precede closed-position links and only the specified fields are returned

#### Scenario: Unknown position or candidate

- **WHEN** a list, add, stage change or removal names a position or candidate that does not exist
- **THEN** a stable not-found problem is returned and nothing changes

### Requirement: Position page lists its candidates

When the actor holds `candidates.read`, the position detail page SHALL show a «Candidatos de la
posición» panel above «Candidatos que encajan». It SHALL list each link with the same columns as
the matches, except that the stage replaces the candidate status and the date added replaces the
update date:

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
