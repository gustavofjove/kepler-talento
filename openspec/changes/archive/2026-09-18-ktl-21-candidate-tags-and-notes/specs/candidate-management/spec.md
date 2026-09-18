## MODIFIED Requirements

### Requirement: Candidate read and list

The system SHALL return a single candidate by identifier including its relation collections and
active custom notes.

Listing candidates for the list screen SHALL be paged, SHALL be ordered and filtered by the server,
and SHALL return the minimal list projection rather than candidate aggregates. Listing SHALL exclude
logically deleted candidates unless the caller explicitly asks for them and holds the permission
that governs them. The system SHALL NOT offer an operation that returns every candidate in one
unbounded response.

Reading one candidate remains the way to obtain that candidate's relation collections and active
custom notes; a list SHALL NOT be a means of obtaining them in bulk.

#### Scenario: Candidate is read by identifier

- **WHEN** an authorized actor reads an existing candidate
- **THEN** the response carries the candidate's field set and its language, program, education,
  experience, skill, tag, note and document collections

#### Scenario: Default listing excludes removed candidates

- **WHEN** an authorized actor lists candidates without asking for removed records
- **THEN** logically deleted candidates are absent from the result

#### Scenario: Removed candidates are listed on request

- **WHEN** an authorized actor lists candidates and explicitly asks to include removed records
- **THEN** logically deleted candidates appear, distinguishable by their inactive state

#### Scenario: List response is inspected

- **WHEN** an authorized actor lists candidates
- **THEN** each item carries the minimal list projection, and the response contains no relation
  collections, tags, custom notes, legacy notes, consent metadata or retention metadata

#### Scenario: Whole table is requested

- **WHEN** a caller attempts to obtain every candidate in a single response
- **THEN** no such operation exists: the request is paged within the documented bounds like any
  other listing

#### Scenario: Listing is ordered and filtered

- **WHEN** an authorized actor lists candidates with a filter and a sort
- **THEN** the filtering and ordering are applied across the whole matching set by the server, and
  the returned page reflects that set rather than an ordering of the page alone

### Requirement: Candidate relation collection writes

The system SHALL allow an authorized actor to change a candidate's language, program, education,
experience, skill and tag collections. Each collection SHALL be written as a complete set against
the owning candidate's concurrency token, so that a partially stale collection cannot be
interleaved with another actor's change.

A tag entry SHALL reference a value of the tags catalog family and SHALL carry no level and no
further field. A tag entry referencing a value of any other family SHALL be rejected.

#### Scenario: Relation collection is replaced

- **WHEN** an authorized actor submits a candidate's complete language collection
- **THEN** the stored collection matches the submission exactly and the candidate's concurrency
  token advances

#### Scenario: Tag collection is replaced

- **WHEN** an authorized actor submits a candidate's complete tag collection
- **THEN** the stored tags match the submission exactly, the candidate's concurrency token
  advances, and the tags omitted from the submission are no longer assigned

#### Scenario: Relation names an unknown catalog value

- **WHEN** a relation record references a value that is not a known catalog entry in the
  required family
- **THEN** the write is rejected with a stable validation code and the previous collection is
  preserved

#### Scenario: Tag names a value of another family

- **WHEN** a tag entry references a catalog value belonging to a family other than tags
- **THEN** the write is rejected with a stable validation code and the previous tags are
  preserved

#### Scenario: Relation write targets a removed candidate

- **WHEN** a relation collection is submitted for a logically removed candidate
- **THEN** the write is refused and the stored collection is unchanged

### Requirement: Relation collection uniqueness

Within a candidate's language, program, skill and tag collections, the referenced value SHALL be
unique. Two entries whose values differ only by surrounding whitespace or letter case SHALL be
treated as the same value. A write introducing a duplicate SHALL be refused with a stable
validation code and a Spanish message, and SHALL leave the stored collection unchanged. This
rule SHALL be enforced where the data is stored, not only in the interface that submits it.

#### Scenario: Duplicate language is submitted

- **WHEN** a language collection is submitted containing a language the collection already holds
- **THEN** the write is refused with a stable validation code and a Spanish message, and the
  stored collection is unchanged

#### Scenario: Duplicate tag is submitted

- **WHEN** a tag collection is submitted containing the same tag twice
- **THEN** the write is refused with a stable validation code and a Spanish message, and the
  stored tags are unchanged

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

## ADDED Requirements

### Requirement: Deactivated tags stay assigned but are not offered

A tag that has been deactivated in the catalog SHALL remain assigned to the candidates that
already hold it and SHALL still resolve when those candidates are read. It SHALL NOT be offered
for a new assignment. Resubmitting a candidate's existing tags SHALL NOT be refused because one
of them has since been deactivated.

#### Scenario: Assigned tag is deactivated

- **WHEN** an administrator deactivates a tag that candidates already hold
- **THEN** those candidates keep the tag, it still resolves and displays on them, and it is no
  longer offered when tags are assigned

#### Scenario: Candidate with a deactivated tag is saved again

- **WHEN** an authorized actor resubmits a candidate's tag collection that still contains a tag
  deactivated since it was assigned
- **THEN** the write succeeds and the tag remains assigned
