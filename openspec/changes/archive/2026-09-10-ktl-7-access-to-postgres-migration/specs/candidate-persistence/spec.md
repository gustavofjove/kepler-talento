## Purpose

Defines the PostgreSQL persistence contract for the candidate aggregate and its relation
collections — the field set, constrained values, consent and retention metadata, logical
deletion state, invariants, and access-path indexes that any writer of candidate data
relies on.

## ADDED Requirements

### Requirement: Candidate record field set

The candidate record SHALL persist identity (first name, last name), contact details
(phone, email), location (location, province, country), availability, status, source,
notes, consent and retention metadata (received date, consent date, review due date),
active state, creation and update timestamps, and a concurrency token.

#### Scenario: Candidate is persisted and read back

- **WHEN** a candidate carrying every field is written and read back
- **THEN** every field round-trips with its stored value unchanged

#### Scenario: Required identity is missing

- **WHEN** a write omits first name or last name
- **THEN** the database rejects the write and the previous valid state is preserved

### Requirement: Constrained candidate status

Candidate status SHALL be restricted at the database level to `new`, `available`,
`in_process`, `hired`, or `rejected`. The restriction SHALL be enforced by a constraint
or lookup relationship, not by application convention alone.

#### Scenario: Unknown status is written

- **WHEN** a write sets a status outside the permitted set
- **THEN** the database rejects the write

#### Scenario: Permitted status is written

- **WHEN** a write sets any of the five permitted statuses
- **THEN** the write succeeds and the status reads back unchanged

### Requirement: Consent and retention metadata integrity

Consent and retention metadata SHALL be stored as explicit date values with no permissive
default. A candidate record SHALL NOT acquire a consent date, received date, or review due
date that was not supplied by its writer.

#### Scenario: Consent metadata is supplied

- **WHEN** a candidate is written with received, consent, and review due dates
- **THEN** all three read back with exactly the supplied values

#### Scenario: Consent metadata is absent

- **WHEN** a candidate is written without a consent date
- **THEN** the stored consent date is absent, and no default date is substituted

### Requirement: Logical deletion state

Candidates SHALL carry an active flag and a deletion timestamp representing logical
deletion. The schema SHALL NOT require physical deletion for a candidate to be treated as
removed, and a logically deleted candidate SHALL remain retrievable by identifier.

#### Scenario: Candidate is logically deleted

- **WHEN** a candidate's active flag is cleared and its deletion timestamp is set
- **THEN** the row still exists and can be retrieved by its identifier with its history
  intact

### Requirement: Candidate relation collections

The schema SHALL persist the candidate's language, program, education, experience, skill,
and document collections as separate records owned by exactly one candidate, each carrying
the fields its business form exposes.

#### Scenario: Relation is attached to a candidate

- **WHEN** a language, program, education, experience, skill, or document record is
  written for an existing candidate
- **THEN** it is stored against that candidate and read back within that candidate's
  collection

#### Scenario: Relation references a missing candidate

- **WHEN** a relation record is written for a candidate identifier that does not exist
- **THEN** the database rejects the write

#### Scenario: Candidate with relations is logically deleted

- **WHEN** a candidate holding relation records is logically deleted
- **THEN** its relation records are preserved, not removed

### Requirement: Migration provenance

A record loaded from the legacy dataset SHALL carry both a stable source identifier and
the moment the migration last wrote it. A record that carries a load moment without a
source identifier SHALL be rejected by the database. Records the application created SHALL
carry neither.

#### Scenario: Migrated record carries its provenance

- **WHEN** the migration writes a record
- **THEN** the record carries its source identifier and the moment it was loaded

#### Scenario: Load moment without a source identifier

- **WHEN** a write sets a load moment on a record that has no source identifier
- **THEN** the database rejects the write

#### Scenario: Application-created record

- **WHEN** the application creates a record
- **THEN** it carries neither a source identifier nor a load moment

### Requirement: Application changes after loading are detectable

It SHALL be possible to determine, for any record loaded from the legacy dataset, whether
anything other than the migration has written it since the migration last did.

#### Scenario: Record untouched since loading

- **WHEN** a loaded record has not been written since the migration wrote it
- **THEN** it does not report application changes

#### Scenario: Record edited through the application

- **WHEN** a loaded record is subsequently written by the application
- **THEN** it reports application changes since loading

### Requirement: At most one primary document per candidate

The schema SHALL permit at most one document per candidate to be marked primary, enforced as
a database invariant rather than by writer discipline.

#### Scenario: A second document is marked primary

- **WHEN** a write marks a second document primary for a candidate that already has one
- **THEN** the database rejects the write

#### Scenario: Candidate has no primary document

- **WHEN** a candidate's documents are all non-primary
- **THEN** the writes succeed and no primary document is required to exist

### Requirement: Catalog-backed relation values

Relation fields that name a language, program, skill, proficiency level, education type,
education status, or sector SHALL reference a business catalog entry rather than storing
an unvalidated free-text value.

#### Scenario: Relation references an unknown catalog value

- **WHEN** a relation record is written referencing a catalog entry that does not exist
- **THEN** the database rejects the write

#### Scenario: Relation references a known catalog value

- **WHEN** a relation record references an existing catalog entry in the correct family
- **THEN** the write succeeds and the referenced value resolves on read

### Requirement: Candidate physical naming and invariants

Every candidate and candidate-relation table SHALL use an explicit quoted physical name
prefixed `CND_`, and SHALL declare its keys, required fields, uniqueness, and relationship
behavior in the schema.

#### Scenario: Naming validation runs

- **WHEN** automated naming validation inspects the candidate mappings
- **THEN** every physical name begins with `CND_` and preserves its configured case

#### Scenario: Application names are inspected

- **WHEN** the candidate entity and its relations are inspected in application code
- **THEN** their application-facing names carry no physical prefix

### Requirement: Candidate access-path indexes

The schema SHALL provide indexes for the access paths candidate reads use: lookup by
identifier, listing active candidates by update recency, and resolving a candidate's
relation collections.

#### Scenario: Relation collection is resolved

- **WHEN** a candidate's relation collections are queried by candidate identifier
- **THEN** the query is served by an index on the owning candidate reference rather than a
  full scan

### Requirement: Candidate least-privilege runtime grants

The migration that creates the candidate schema SHALL ship the runtime role's grants in
the same deployment action, and the runtime role SHALL NOT hold schema-modification
privileges on candidate tables.

#### Scenario: Runtime role attempts schema modification

- **WHEN** the runtime database role attempts to alter or drop a candidate table
- **THEN** PostgreSQL denies the operation

#### Scenario: Runtime role reads and writes candidate data

- **WHEN** the runtime database role performs permitted candidate reads and writes
- **THEN** the operations succeed
