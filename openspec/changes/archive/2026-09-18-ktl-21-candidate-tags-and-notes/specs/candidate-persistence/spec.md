## MODIFIED Requirements

### Requirement: Candidate relation collections

The schema SHALL persist the candidate's language, program, education, experience, skill, tag,
note, and document collections as separate records owned by exactly one candidate, each carrying
the fields its business form exposes.

#### Scenario: Relation is attached to a candidate

- **WHEN** a language, program, education, experience, skill, tag, note, or document record is
  written for an existing candidate
- **THEN** it is stored against that candidate and read back within that candidate's
  collection

#### Scenario: Relation references a missing candidate

- **WHEN** a relation record is written for a candidate identifier that does not exist
- **THEN** the database rejects the write

#### Scenario: Candidate with relations is logically deleted

- **WHEN** a candidate holding relation records is logically deleted
- **THEN** its relation records are preserved, not removed

### Requirement: Catalog-backed relation values

Relation fields that name a language, program, skill, proficiency level, education type,
education status, sector, or tag SHALL reference a business catalog entry rather than storing
an unvalidated free-text value.

#### Scenario: Relation references an unknown catalog value

- **WHEN** a relation record is written referencing a catalog entry that does not exist
- **THEN** the database rejects the write

#### Scenario: Relation references a known catalog value

- **WHEN** a relation record references an existing catalog entry in the correct family
- **THEN** the write succeeds and the referenced value resolves on read

#### Scenario: Relation references a catalog value of the wrong family

- **WHEN** a tag record is written referencing a catalog entry that exists but belongs to
  another family
- **THEN** the database rejects the write

## ADDED Requirements

### Requirement: Candidate tag assignment uniqueness in the database

The schema SHALL prevent a candidate from holding the same tag more than once, enforced by the
database rather than by the writer, so that concurrent writes cannot produce a duplicate
assignment.

#### Scenario: Duplicate assignment is written directly

- **WHEN** the same tag is written twice for one candidate
- **THEN** the database rejects the second write

#### Scenario: Concurrent assignments of the same tag

- **WHEN** two writers assign the same tag to one candidate at the same time
- **THEN** exactly one succeeds and afterwards the candidate holds that tag once

### Requirement: Candidate note persistence and logical-retirement grants

The schema SHALL persist a candidate note with its body, nullable internal-user author reference,
creation timestamp, update timestamp, active flag and concurrency token, under the candidate
physical naming convention. A non-null author SHALL reference an existing internal user without
cascade deletion. The runtime database role SHALL hold the privileges needed to read, insert and
update notes, and SHALL NOT hold `DELETE` on the notes table, because a note is retired logically.
The migration that creates the table SHALL ship these grants in the same deployment action.

#### Scenario: Runtime role attempts to delete a note

- **WHEN** the runtime database role attempts to delete a row from the candidate notes table
- **THEN** PostgreSQL denies the operation

#### Scenario: Runtime role retires a note

- **WHEN** the runtime database role marks a note inactive
- **THEN** the operation succeeds and the row remains present

#### Scenario: Note references an unknown author

- **WHEN** a note is written with a non-null author identifier that does not name an internal user
- **THEN** the database rejects the write

#### Scenario: Grants are inspected

- **WHEN** the database catalog is queried for the runtime role's privileges on the candidate
  notes table
- **THEN** it holds select, insert and update, and holds no delete

### Requirement: Tag and note access-path indexes

The schema SHALL provide indexes for the access paths tags and notes are read through:
resolving a candidate's tags by candidate identifier, matching candidates by tag when search
filters on tags, and listing a candidate's active notes by recency.

#### Scenario: Candidate tags are resolved

- **WHEN** a candidate's tags are queried by candidate identifier
- **THEN** the query is served by an index on the owning candidate reference rather than a full
  scan

#### Scenario: Candidates are matched by tag

- **WHEN** search matches candidates by a tag value
- **THEN** the query is served by an index on the tag reference rather than a full scan

#### Scenario: Candidate notes are listed

- **WHEN** a candidate's active notes are queried newest first
- **THEN** the query is served by an index covering the owning candidate and creation time
  rather than a full scan
