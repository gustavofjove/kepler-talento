## Purpose

Defines the per-candidate note thread: an individually authored, dated and non-destructive record
of what happened with a candidate, its lifecycle and concurrency rules, who may read and write
it, and the personal-data obligations that keep note text out of every surface that does not need
it.

## ADDED Requirements

### Requirement: Candidate note field set

A candidate note SHALL carry a stable identifier, the identifier of the candidate it belongs to,
a body, the internal user identifier of the actor who wrote it when one is available, a creation
timestamp, an update timestamp, an active flag and a concurrency token. A note created through
the candidate API SHALL record `ICurrentActor.UserId`. The stored author SHALL NOT be an email
address, a display name or an identity-provider subject.

#### Scenario: Note is created

- **WHEN** an authorized actor adds a note to a candidate
- **THEN** the stored note carries the submitted body, the candidate it belongs to, the acting
  actor's internal user identifier, equal creation and update timestamps, an active flag, and a
  concurrency token

#### Scenario: Author is stored without personal data

- **WHEN** a stored note is inspected
- **THEN** it holds the author's internal user identifier and no email address, display name or
  identity-provider subject

#### Scenario: System note has no author identifier

- **WHEN** a note originates from a system or historical process with no internal user identity
- **THEN** its author identifier is absent rather than replaced with an email, display name or
  fabricated user

### Requirement: Notes are independent of the candidate's field set

A candidate note SHALL be an addition to the candidate record, not a replacement for the
candidate's existing single free-text notes field. That field SHALL keep its meaning and its
behavior unchanged.

#### Scenario: Existing notes field is unaffected

- **WHEN** an actor adds, edits or retires a candidate note
- **THEN** the candidate's existing free-text notes field keeps its stored value

### Requirement: Note body validation

A note body SHALL be required and SHALL be rejected when it is empty or only whitespace. A body
SHALL be rejected when it exceeds the documented maximum length. A rejected write SHALL return a
stable validation code with a Spanish message and SHALL store nothing.

#### Scenario: Blank body is submitted

- **WHEN** an actor submits a note whose body is empty or only whitespace
- **THEN** the request is rejected with a stable validation code and a Spanish message, and no
  note is stored

#### Scenario: Body exceeds the maximum length

- **WHEN** an actor submits a note body longer than the documented maximum
- **THEN** the request is rejected with a stable validation code and no note is stored

### Requirement: Notes are written individually with their own concurrency token

Notes SHALL NOT be written as a replaced collection. Each note SHALL be created, edited and
retired on its own, and an edit SHALL supply the concurrency token the actor last read. A stale
token SHALL be rejected with a stable conflict code and SHALL leave the stored note unchanged. A
successful edit SHALL preserve the note's identifier, candidate, author and creation timestamp,
SHALL advance its update timestamp, and SHALL return a new token.

#### Scenario: Note is edited

- **WHEN** an authorized actor edits a note supplying its current concurrency token
- **THEN** the body is stored, the update timestamp advances, the identifier, candidate, author
  and creation timestamp are unchanged, and a new token is returned

#### Scenario: Two actors edit the same note

- **WHEN** two actors read the same note and the second saves after the first
- **THEN** the second write is rejected with a stable conflict code and the first actor's change
  remains

#### Scenario: Adding a note does not conflict with a candidate edit

- **WHEN** one actor adds a note to a candidate while another actor is editing that candidate's
  own fields
- **THEN** both operations succeed and neither is refused as a concurrency conflict

### Requirement: Notes are retired, never deleted

A note SHALL be removable from view only by being retired. The system SHALL NOT expose any
operation that physically deletes a note, and the stored row SHALL be preserved when a note is
retired. Retiring an already retired note SHALL succeed without further effect.

#### Scenario: Note is retired

- **WHEN** an authorized actor retires a note
- **THEN** the note stops appearing in the candidate's notes and the row is still present in the
  database

#### Scenario: Retirement is repeated

- **WHEN** an actor retires a note that is already retired
- **THEN** the request succeeds and nothing further changes

#### Scenario: No physical delete exists

- **WHEN** the published API contract is inspected
- **THEN** it exposes no operation that physically removes a note

### Requirement: Note listing order and scope

Reading a candidate's notes SHALL return that candidate's active notes, most recently created
first, and SHALL NOT return retired notes or notes belonging to another candidate. A note
addressed through a candidate it does not belong to SHALL be reported as not found.

#### Scenario: Notes are listed

- **WHEN** an authorized actor reads a candidate's notes
- **THEN** the active notes of that candidate are returned, newest first, and no retired note
  appears

#### Scenario: Note is addressed through the wrong candidate

- **WHEN** an operation names a note identifier that belongs to a different candidate
- **THEN** the request is reported as not found and nothing is stored

### Requirement: Unknown authorship is explicit

A note whose author cannot be resolved — because it was written before an actor identity
existed, or by a system process rather than a person — SHALL be presented with an explicit
unknown author. It SHALL NOT be presented with a blank author or an author invented for it.
For a known author, the candidate-note response SHALL resolve and return only the internal user
identifier and display name needed by the note thread; clients SHALL NOT require access to the
admin-only user-management API to resolve it.

#### Scenario: Note has no recorded author

- **WHEN** a note whose author identifier is absent is presented
- **THEN** the author is shown as explicitly unknown rather than as blank or fabricated

#### Scenario: Known note author is presented

- **WHEN** a permitted actor reads a note whose internal author identifier resolves to a user
- **THEN** the response carries that identifier and display name without exposing the user's
  email, role, provider subject or other administration fields

### Requirement: Note authorization fails closed

Reading a candidate's notes SHALL require an authenticated actor holding the candidate read
capability. Adding, editing and retiring a note SHALL require an authenticated actor holding the
candidate update capability. Authorization SHALL be enforced by the API before the request is
validated or dispatched; hiding or disabling user-interface controls is not the control.

#### Scenario: Unauthenticated caller

- **WHEN** a caller with no resolved actor invokes any note operation
- **THEN** the request is denied, no note data is returned, and nothing is stored

#### Scenario: Reader attempts to write

- **WHEN** an authenticated actor holding only the candidate read capability adds, edits or
  retires a note
- **THEN** the request is denied before validation runs and nothing is stored

#### Scenario: Permitted actor writes

- **WHEN** an authenticated actor holding the candidate update capability submits a valid note
- **THEN** the note is stored

### Requirement: Notes on a removed candidate

A note SHALL NOT be added to, edited on, or retired from a logically removed candidate. The
notes of a logically removed candidate SHALL remain stored and SHALL remain readable to an
authorized actor that reads the candidate.

#### Scenario: Note is written for a removed candidate

- **WHEN** a note operation targets a logically removed candidate
- **THEN** the write is refused and the stored notes are unchanged

#### Scenario: Removed candidate keeps its notes

- **WHEN** a candidate holding notes is logically removed
- **THEN** its notes are preserved and still resolve when the candidate is read

### Requirement: Note text is personal data

A note body SHALL be treated as personal data about the candidate. It SHALL NOT appear in
application, request, audit or diagnostic logs, SHALL NOT appear in search results or candidate
listings, and SHALL NOT be included in exports. An audit record of a note operation SHALL carry
identifiers and codes only.

#### Scenario: Note write reaches diagnostics

- **WHEN** a note operation succeeds or fails
- **THEN** the logs identify the request only by safe operational metadata such as a correlation
  identifier, and contain no note body and no author personal data

#### Scenario: Note operation is audited

- **WHEN** a note is added, edited or retired
- **THEN** an audit record is written naming the acting actor, the candidate, the note and the
  kind of change, and containing no note body

#### Scenario: Notes are absent from result projections

- **WHEN** a candidate appears in a search result or a candidate listing
- **THEN** no note body is present in that response
