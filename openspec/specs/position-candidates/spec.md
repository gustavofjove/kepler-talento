## Purpose

Records which candidates HR is actually considering for each position and the stage each one has
reached, independently of whether they match the position's requirements.

## Requirements

### Requirement: Position candidate link identity

The system SHALL represent a position candidate as a persistent link between one position and one
candidate, with a stage, the time it was added, the time it was last updated and a concurrency
version. At most one link SHALL exist for a given position and candidate. A new link SHALL start at
stage `new`. Adding a candidate SHALL NOT evaluate or require the position's requirements.

#### Scenario: Matching candidate is added

- **WHEN** an authorized manager adds a candidate to an open position
- **THEN** a link is stored at stage `new` with server-assigned timestamps and version

#### Scenario: Non-matching candidate is added

- **WHEN** an authorized manager adds an active candidate who does not satisfy the position's requirements
- **THEN** the link is created exactly as for a matching candidate

#### Scenario: Candidate is added twice

- **WHEN** a candidate already linked to a position is added to it again, including by two concurrent requests
- **THEN** exactly one link exists and every other request receives a stable already-linked conflict

### Requirement: Stage vocabulary

The stage SHALL be exactly one of `new`, `shortlisted`, `interview`, `hired` or `rejected`, presented
in that order and labelled Nuevo, Preseleccionado, Entrevista, Contratado and Descartado. An authorized
manager SHALL be able to change any stage to any other. A stage change SHALL require the link's
last-read version. Changing a stage SHALL NOT change the candidate's own status, and changing a
candidate's status SHALL NOT change any stage.

#### Scenario: Stage is changed

- **WHEN** an authorized manager changes a link from `new` to `interview` with its current version
- **THEN** the new stage is stored with a new version and the candidate's own status is unchanged

#### Scenario: Unknown stage is submitted

- **WHEN** a stage change contains a value outside the vocabulary
- **THEN** the request is rejected with a stable validation problem and the link is unchanged

#### Scenario: Stale stage change

- **WHEN** two managers hold the same link version and the second changes the stage after the first
- **THEN** the second receives a stable concurrency conflict and the first stage remains stored

#### Scenario: Candidate is rejected for a position

- **WHEN** a link's stage is set to `rejected`
- **THEN** the link remains and is still listed for the position and the candidate

### Requirement: Link removal

An authorized manager SHALL be able to remove a link, which SHALL delete it permanently so that the
candidate may be added again later. Removing a link SHALL NOT change the position or the candidate.

#### Scenario: Mistaken link is removed

- **WHEN** an authorized manager removes an existing link
- **THEN** the link no longer exists, it disappears from both the position's and the candidate's lists, and the candidate can be added again

#### Scenario: Missing link is removed

- **WHEN** a manager removes a link that does not exist
- **THEN** a stable not-found problem is returned

### Requirement: Link write preconditions

Adding, restaging and removing links SHALL be refused with a stable position-closed conflict while
the position is `closed`. Adding a logically removed candidate SHALL be refused with a stable
candidate-inactive conflict. Existing links to a removed candidate SHALL remain and SHALL still
accept stage changes and removal. A position SHALL hold at most 500 links, and an add beyond that
SHALL be refused with a stable limit conflict.

#### Scenario: Closed position is changed

- **WHEN** any add, stage change or removal targets a closed position
- **THEN** it is refused with the position-closed conflict and nothing changes

#### Scenario: Position is reopened

- **WHEN** a closed position is reopened
- **THEN** its links can again be added, restaged and removed

#### Scenario: Removed candidate is added

- **WHEN** a manager adds a logically removed candidate
- **THEN** it is refused with the candidate-inactive conflict

#### Scenario: Linked candidate is later removed

- **WHEN** a linked candidate is logically removed
- **THEN** the link stays listed with the candidate marked as removed and its stage can still change

#### Scenario: Link limit is reached

- **WHEN** a position already holding 500 links receives another add
- **THEN** it is refused with the limit conflict

### Requirement: Position candidate API

The system SHALL expose:

- `GET /api/positions/{id}/candidates`: the complete link list of a position;
- `POST /api/positions/{id}/candidates`: add a candidate by id;
- `PUT /api/positions/{id}/candidates/{candidateId}/stage`: change the stage with a version;
- `DELETE /api/positions/{id}/candidates/{candidateId}`: remove a link;
- `GET /api/candidates/{candidateId}/positions`: the complete link list of a candidate.

A position's list SHALL order links by stage vocabulary order, then by time added descending,
then by candidate id. Each of its items SHALL contain only the candidate id, first name, last
name, e-mail, phone, whether a primary CV exists, candidate active flag, stage, added and updated
times and version: the contact columns candidate search already shows to the same readers.

A candidate's list SHALL order links to open positions before links to closed positions, each by
time added descending. Each of its items SHALL contain only the position id, title, position
status, stage, added and updated times and version.

No list item SHALL contain documents, notes, other candidate fields, position description or
position requirements, and a candidate's list SHALL contain no candidate contact details.

#### Scenario: Position list is read

- **WHEN** an authorized reader requests an existing position's candidates
- **THEN** every link is returned in the specified order with only the specified fields

#### Scenario: Candidate list is read

- **WHEN** an authorized reader requests an existing candidate's positions
- **THEN** open-position links precede closed-position links and only the specified fields are returned

#### Scenario: Unknown position or candidate

- **WHEN** a list, add, stage change or removal names a position or candidate that does not exist
- **THEN** a stable not-found problem is returned and nothing changes

### Requirement: Position candidate authorization fails closed

Reading either link list SHALL require both `positions.read` and `candidates.read`. Adding,
restaging and removing SHALL require both `positions.manage` and `candidates.read`. Every link
request SHALL check authentication and both permissions before validating input or looking up a
position, candidate or link. Application handlers SHALL repeat the same guards. No new permission
is introduced.

#### Scenario: Unauthenticated caller

- **WHEN** an unauthenticated caller sends valid or malformed input to any link endpoint
- **THEN** it receives the same refusal with no validation detail and nothing changes

#### Scenario: Caller lacks candidate permission

- **WHEN** an actor holding `positions.read` and `positions.manage` but not `candidates.read` calls any link endpoint
- **THEN** the request is refused before lookup and no candidate data is disclosed

#### Scenario: Reader attempts a write

- **WHEN** an actor with `positions.read` and `candidates.read` but without `positions.manage` adds, restages or removes a link
- **THEN** the request is refused and nothing changes

#### Scenario: Existing and missing ids are indistinguishable

- **WHEN** an unauthorized actor requests links for an existing and a missing position
- **THEN** both refusals are identical

### Requirement: Link changes are audited

Adding a link, changing its stage and removing it SHALL each record an audit event
(`position.candidate_added`, `position.candidate_stage_changed`, `position.candidate_removed`)
in the same transaction as the change. Each event SHALL carry the actor, the position id and the
candidate id, and SHALL NOT carry the stage value or any candidate field value. Candidate names
and stages SHALL NOT appear in logs.

#### Scenario: Link is removed

- **WHEN** a manager removes a link
- **THEN** a `position.candidate_removed` event names the actor, position and candidate even though the link no longer exists

#### Scenario: Stage change event is inspected

- **WHEN** a `position.candidate_stage_changed` event is inspected
- **THEN** it contains no stage value, name or other candidate field value

### Requirement: Position page lists its candidates

When the actor holds `candidates.read`, the position detail page SHALL show a «Candidatos de la
posición» panel above «Candidatos que encajan», listing each link with the same columns as the matches
(name with e-mail, phone, CV availability) except that the stage replaces the candidate status and
the date added replaces the update date, and, for managers on an open position, a labelled stage
selector and «Quitar de la posición». Both tables open the candidate when a row is clicked outside
its links and controls; the name is the row's keyboard-reachable link, and phones are plain text.

Removal SHALL ask for confirmation. A stage change SHALL save immediately, and a concurrency
conflict SHALL be reported and the list reloaded. Managers on an open position SHALL have
«Añadir candidato», which opens a picker searching candidates by text through the existing
candidate search and shows already-linked candidates as not selectable. The panel SHALL show
localized loading, empty and error states. Without `candidates.read`, no link request SHALL be
sent.

#### Scenario: Candidate is added through the picker

- **WHEN** a manager searches the picker for a non-matching candidate and selects them
- **THEN** the candidate appears in «Candidatos de la posición» at stage Nuevo

#### Scenario: Closed position is viewed

- **WHEN** a manager opens a closed position
- **THEN** its links are listed read-only with no stage selector, removal or add control, and a hint explains that reopening allows changes

#### Scenario: Removal is cancelled

- **WHEN** a manager activates «Quitar de la posición» and cancels the confirmation
- **THEN** no request is sent and the link remains

#### Scenario: Reader without candidate permission

- **WHEN** an actor with `positions.read` but without `candidates.read` opens a position
- **THEN** no link request is made and no candidate name or count is shown

#### Scenario: Controls are accessible

- **WHEN** the panel and picker are used by keyboard at 390 pixels wide
- **THEN** every selector and button has a programmatic label, the dialog returns focus on close, and the page does not scroll horizontally
