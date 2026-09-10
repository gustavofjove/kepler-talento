# Candidate Management Specification

## Purpose

Defines how the application creates, reads, lists, changes and logically removes candidate
records and their relation collections — who is permitted to do each, how conflicting edits are
resolved, what is recorded about a change, and what a caller observes when a request is refused.

## Requirements

### Requirement: Candidate creation

The system SHALL create a candidate from the field set the business form supplies, and SHALL
return the stored record including its generated identifier, timestamps and concurrency token.
Required identity fields SHALL be validated before storage, and a rejected creation SHALL store
nothing.

#### Scenario: Candidate is created

- **WHEN** an authorized actor submits a candidate with valid identity fields
- **THEN** the candidate is stored, and the response carries its identifier, creation and update
  timestamps, active state and concurrency token

#### Scenario: Required identity is missing

- **WHEN** a creation omits first name or last name
- **THEN** the request is rejected with a stable validation code and a Spanish message, and no
  candidate is stored

#### Scenario: Created candidate is immediately readable

- **WHEN** a candidate has just been created
- **THEN** reading it by identifier returns exactly the submitted values

### Requirement: Candidate update

The system SHALL update a candidate's stored field set as a whole record and SHALL advance its
update timestamp and concurrency token. An update SHALL NOT change a candidate's identifier or
creation timestamp.

#### Scenario: Candidate is updated

- **WHEN** an authorized actor submits changed fields for an existing candidate with the current
  concurrency token
- **THEN** the stored record reflects the submitted values, its update timestamp advances, and
  its identifier and creation timestamp are unchanged

#### Scenario: Update targets a candidate that does not exist

- **WHEN** an update names an identifier with no stored candidate
- **THEN** the request is refused as not found and nothing is stored

### Requirement: Candidate read and list

The system SHALL return a single candidate by identifier including its relation collections, and
SHALL list candidates for the list screen. Listing SHALL exclude logically deleted candidates
unless the caller explicitly asks for them.

#### Scenario: Candidate is read by identifier

- **WHEN** an authorized actor reads an existing candidate
- **THEN** the response carries the candidate's field set and its language, program, education,
  experience, skill and document collections

#### Scenario: Default listing excludes removed candidates

- **WHEN** an authorized actor lists candidates without asking for removed records
- **THEN** logically deleted candidates are absent from the result

#### Scenario: Removed candidates are listed on request

- **WHEN** an authorized actor lists candidates and explicitly asks to include removed records
- **THEN** logically deleted candidates appear, distinguishable by their inactive state

### Requirement: Logical removal only

A candidate SHALL be removed only logically, by clearing its active state and recording a
deletion timestamp. No operation SHALL physically delete a candidate in normal operation, and a
logically removed candidate SHALL remain retrievable by identifier and restorable.

#### Scenario: Candidate is removed

- **WHEN** an authorized actor removes a candidate
- **THEN** the candidate's active state is cleared, its deletion timestamp is recorded, and its
  stored data is preserved

#### Scenario: Removed candidate is retrieved

- **WHEN** a logically removed candidate is read by identifier
- **THEN** the record is returned with its data and history intact

#### Scenario: Removed candidate is restored

- **WHEN** an authorized actor restores a logically removed candidate
- **THEN** the candidate becomes active again and reappears in default listings

#### Scenario: Physical deletion is attempted

- **WHEN** any caller attempts to physically delete a candidate through the application
- **THEN** no such operation is available, and the stored row is not destroyed

### Requirement: Constrained candidate status

A candidate's status SHALL be one of `new`, `available`, `in_process`, `hired` or `rejected`. A
write carrying any other value SHALL be refused with a stable validation code before storage.

#### Scenario: Unknown status is submitted

- **WHEN** a create or update submits a status outside the permitted set
- **THEN** the request is rejected with a stable validation code and nothing is stored

#### Scenario: Permitted status is submitted

- **WHEN** a create or update submits any of the five permitted statuses
- **THEN** the write succeeds and the status reads back unchanged

### Requirement: Consent and retention metadata are carried unchanged

Candidate consent and retention metadata SHALL be stored exactly as supplied. The system SHALL
NOT substitute a default, a current date, or a derived value for an absent consent date,
received date or review due date, and SHALL NOT drop a supplied one.

#### Scenario: Metadata is supplied

- **WHEN** a candidate is written with received, consent and review due dates
- **THEN** all three read back with exactly the supplied values

#### Scenario: Consent date is absent

- **WHEN** a candidate is written without a consent date
- **THEN** the stored consent date is absent, and no date is substituted

#### Scenario: Update does not touch metadata

- **WHEN** an update changes only contact details
- **THEN** the stored consent and retention metadata are unchanged

### Requirement: Optimistic concurrency on candidate writes

Every write to a candidate — field update, removal, restoration, or a change to one of its
relation collections — SHALL be checked against the concurrency token the caller last read. A
write carrying a stale token SHALL be refused with a stable conflict code and SHALL apply no
part of the change.

#### Scenario: Concurrent update is refused

- **WHEN** two actors read the same candidate and both submit an update
- **THEN** the first succeeds and the second is refused with a stable conflict code, leaving the
  first actor's values stored

#### Scenario: Conflict leaves no partial change

- **WHEN** a write is refused for a stale concurrency token
- **THEN** no field, relation record or audit event from that write is stored

#### Scenario: Removal advances the token

- **WHEN** a candidate is logically removed and an editor holding the pre-removal token submits
  an update
- **THEN** the update is refused with a stable conflict code rather than resurrecting the
  candidate

### Requirement: Candidate relation collection writes

The system SHALL allow an authorized actor to change a candidate's language, program, education,
experience and skill collections. Each collection SHALL be written as a complete set against the
owning candidate's concurrency token, so that a partially stale collection cannot be interleaved
with another actor's change.

#### Scenario: Relation collection is replaced

- **WHEN** an authorized actor submits a candidate's complete language collection
- **THEN** the stored collection matches the submission exactly and the candidate's concurrency
  token advances

#### Scenario: Relation names an unknown catalog value

- **WHEN** a relation record references a value that is not a known catalog entry in the
  required family
- **THEN** the write is rejected with a stable validation code and the previous collection is
  preserved

#### Scenario: Relation write targets a removed candidate

- **WHEN** a relation collection is submitted for a logically removed candidate
- **THEN** the write is refused and the stored collection is unchanged

### Requirement: Relation collection uniqueness

Within a candidate's language, program and skill collections, the referenced value SHALL be
unique. Two entries whose values differ only by surrounding whitespace or letter case SHALL be
treated as the same value. A write introducing a duplicate SHALL be refused with a stable
validation code and a Spanish message, and SHALL leave the stored collection unchanged. This
rule SHALL be enforced where the data is stored, not only in the interface that submits it.

#### Scenario: Duplicate language is submitted

- **WHEN** a language collection is submitted containing a language the collection already holds
- **THEN** the write is refused with a stable validation code and a Spanish message, and the
  stored collection is unchanged

#### Scenario: Duplicate differs only by case or spacing

- **WHEN** a submitted value matches an existing one after trimming surrounding whitespace and
  ignoring letter case
- **THEN** it is treated as a duplicate and refused

#### Scenario: Concurrent writers submit the same value

- **WHEN** two actors concurrently add the same skill to one candidate
- **THEN** at most one entry for that value exists afterwards, and the losing write is refused
  rather than producing a duplicate

#### Scenario: Distinct values are accepted

- **WHEN** a collection is submitted whose values are all distinct under the same comparison
- **THEN** the write succeeds and every submitted entry is stored

### Requirement: Candidate document metadata

A candidate document SHALL exist only as the result of an accepted upload that carried content;
no operation SHALL create a document record from metadata alone. The system SHALL record
document metadata — type, original filename, media type, size, primary flag, upload timestamp
and availability state — against a candidate, and SHALL keep at most one document marked primary
per candidate as a stored invariant that holds under concurrent writes rather than as a writer
convention. Document metadata SHALL NOT expose an internal storage path or key.

#### Scenario: Document appears after an accepted upload

- **WHEN** an authorized actor uploads a document for a candidate and the upload is accepted
- **THEN** the metadata is stored against that candidate and appears in the candidate's document
  collection with its availability state

#### Scenario: Document metadata is attached

- **WHEN** an authorized actor attaches a document by completing an accepted content upload
- **THEN** the metadata is stored against that candidate and appears in the candidate's document
  collection with its availability state

#### Scenario: Metadata-only attachment is refused

- **WHEN** a caller attempts to add a document to a candidate without supplying content
- **THEN** the request is refused and no document record is created

#### Scenario: A second document is marked primary

- **WHEN** a document is marked primary while another already is
- **THEN** the previously primary document is no longer primary, and exactly one document is
  primary

#### Scenario: Two documents are marked primary concurrently

- **WHEN** two actors concurrently mark different documents of the same candidate as primary
- **THEN** exactly one document ends up primary, the losing write is refused with a stable code,
  and no state exists in which the candidate has two primary documents

#### Scenario: Primary document is removed

- **WHEN** the primary document of a candidate is removed
- **THEN** the candidate is left with no primary document, and no other document is promoted
  automatically

#### Scenario: Document metadata is returned

- **WHEN** a candidate's document collection is read
- **THEN** no response field exposes a storage path, storage key or filesystem location

### Requirement: Per-operation candidate authorization

Reading, creating, updating and removing candidates SHALL each require its own capability. Every
candidate operation SHALL fail closed for an unauthenticated actor and for an authenticated
actor lacking the required capability, returning no candidate data in either case. Hiding a
control in the interface SHALL NOT be treated as the access control.

#### Scenario: Unauthenticated caller attempts an operation

- **WHEN** an unauthenticated caller invokes any candidate read or write operation
- **THEN** the request is refused and no candidate data is returned

#### Scenario: Actor holds read but not write capability

- **WHEN** an actor permitted to read candidates attempts to create, update or remove one
- **THEN** the request is refused and nothing is stored

#### Scenario: Actor holds update but not removal capability

- **WHEN** an actor permitted to update candidates attempts to remove one
- **THEN** the request is refused and the candidate remains active

#### Scenario: Refusal does not disclose existence

- **WHEN** an unauthorized caller names a candidate identifier
- **THEN** the refusal does not reveal whether a candidate with that identifier exists

### Requirement: Candidate change auditing without personal data

Every candidate create, update, status change, logical removal, restoration, relation change and
document metadata change SHALL record an audit event carrying the acting identity, the affected
candidate identifier, the kind of change, its outcome and the request correlation identifier.
Audit events SHALL NOT record candidate field values, which are personal data. An audit event
SHALL be recorded only for a change that was actually applied.

#### Scenario: Change is audited

- **WHEN** a candidate is created, updated, removed, restored, or has a relation or document
  collection changed
- **THEN** an audit event records the actor, the candidate identifier, the kind of change, the
  outcome and the correlation identifier

#### Scenario: Audit event carries no personal data

- **WHEN** an audit event for a candidate change is inspected
- **THEN** it contains no name, contact detail, note, or other candidate field value

#### Scenario: Refused change is not audited as applied

- **WHEN** a candidate write is refused for validation, authorization or concurrency
- **THEN** no audit event describing an applied change is stored

### Requirement: Candidate personal data is absent from logs

Application logs SHALL NOT contain candidate names, contact details, location, notes, or consent
and retention dates, including when a candidate request fails or raises an unhandled error.

#### Scenario: Candidate write is logged

- **WHEN** a candidate create or update is processed and logged
- **THEN** the log entries contain no candidate field values, and identify the record by
  identifier and correlation identifier only

#### Scenario: Candidate request fails

- **WHEN** a candidate request fails with a validation, authorization or unexpected error
- **THEN** the logged diagnostic contains no candidate field values

### Requirement: Stable candidate error contract

Refused candidate operations SHALL return documented problem responses carrying a stable machine
code, a Spanish user-facing message, the correlation identifier, and field-level errors where
the refusal is a field validation failure.

#### Scenario: Validation failure is returned

- **WHEN** a candidate write fails validation
- **THEN** the response carries a stable code, a Spanish message, the offending fields, and the
  correlation identifier

#### Scenario: Conflict is returned

- **WHEN** a candidate write is refused for a stale concurrency token
- **THEN** the response carries a stable conflict code and a Spanish message distinguishable
  from a validation failure

#### Scenario: Problem response leaks nothing

- **WHEN** any candidate problem response is returned
- **THEN** it exposes no stack trace, database detail, internal path, or candidate field value
  the caller did not submit
