# Legacy Data Migration Specification

## Purpose

Defines the one-directional, re-runnable migration of the authoritative candidate dataset
from the legacy Access database into PostgreSQL, including its export contract, row
outcomes, reference-value resolution, reconciliation report, personal-data handling, and
rollback.

## Requirements

### Requirement: Operator-run migration boundary

The migration SHALL be executed as an explicit operator-run action against a chosen target
database and SHALL NOT be reachable as a production API request or triggered by
application startup.

#### Scenario: Operator runs the migration

- **WHEN** an operator runs the documented migration action against a target database with
  an export set
- **THEN** the migration executes and reports its outcome

#### Scenario: Application starts normally

- **WHEN** the API or worker starts
- **THEN** no migration of legacy data begins

### Requirement: Documented export contract

The migration SHALL consume a documented intermediate export produced from Access, and
SHALL NOT read the Access database directly. The export contract SHALL define the expected
files, their encoding, their columns, and a stable per-row source identifier for every
entity.

#### Scenario: Export conforms to the contract

- **WHEN** an export set matching the documented contract is supplied
- **THEN** the migration accepts it and proceeds to validation

#### Scenario: Export is missing a required file or column

- **WHEN** an export set is missing a required file, column, or source identifier
- **THEN** the migration stops before writing any business data and reports the structural
  problem

#### Scenario: Export encoding is not as documented

- **WHEN** an export file is not in the documented encoding
- **THEN** the migration rejects the export rather than loading corrupted text

### Requirement: Validate before writing business data

The migration SHALL validate the whole export set — structure and per-row rules — before
any candidate business data is written, and SHALL report the full set of problems rather
than stopping at the first one.

#### Scenario: Export contains invalid rows

- **WHEN** validation finds rows that cannot be loaded
- **THEN** every such row is reported with its reason, and the operator can see the
  complete picture before committing

### Requirement: Idempotent re-runnable load

Loading SHALL be keyed on each row's stable source identifier so that re-running the
migration over the same export corrects existing records rather than creating duplicates.

#### Scenario: Migration is run twice

- **WHEN** a successful migration is run a second time against the same export set
- **THEN** the resulting row counts per entity are unchanged and no duplicate candidate or
  relation record exists

#### Scenario: Corrected export is re-run

- **WHEN** an export is corrected and the migration is re-run
- **THEN** the affected records are updated in place and their source identifiers still
  resolve to exactly one target record

### Requirement: Re-runs do not overwrite application changes

A re-run SHALL NOT overwrite a record that has been written by the application since the
migration last loaded it. Such records SHALL be reported and skipped, and SHALL be
overwritten only when the operator explicitly asks for it.

#### Scenario: Newer export arrives after records were edited in the application

- **WHEN** a re-run would update a record the application has written since it was loaded
- **THEN** the record is left as the application left it, and is reported as skipped with
  an application-changed reason

#### Scenario: Operator chooses to overwrite

- **WHEN** the operator explicitly requests that application-changed records be overwritten
- **THEN** those records are updated from the source and the report states how many were
  overwritten

#### Scenario: Untouched records still update

- **WHEN** a re-run updates records that the application has not written since loading
- **THEN** those records update normally without requiring any explicit override

### Requirement: Records absent from the export are reported

A run SHALL report records in the target that carry a source identifier which the current
export does not contain. Such records SHALL NOT be modified or removed automatically.

#### Scenario: Source row was deleted from the legacy dataset

- **WHEN** the target holds a record whose source identifier is absent from the export
- **THEN** the run reports it as an unmatched target record and leaves it unchanged

#### Scenario: Report distinguishes unmatched records from rejections

- **WHEN** a run reports unmatched target records
- **THEN** they are counted separately from rejected and skipped source rows, so an
  incomplete export cannot be mistaken for a set of deletions

### Requirement: Per-candidate transactional load

A candidate and its relation records SHALL be committed as one unit. A failure while
loading any part of a candidate SHALL leave none of that candidate's records persisted.

#### Scenario: A relation row fails mid-candidate

- **WHEN** loading a candidate's relation records fails partway
- **THEN** that candidate contributes no partial record to the target database and is
  reported as rejected

### Requirement: Explicit row outcomes

Every source row SHALL end the migration in exactly one of three states — loaded,
rejected, or skipped — and every rejected or skipped row SHALL carry a reason.

#### Scenario: Migration completes

- **WHEN** a migration run finishes
- **THEN** the sum of loaded, rejected, and skipped rows equals the source row count for
  every entity, and no row is unaccounted for

#### Scenario: Row cannot be loaded

- **WHEN** a row fails a validation or resolution rule
- **THEN** it is recorded as rejected with a stable reason code identifying the failing
  rule and field

### Requirement: Reference-value resolution without silent creation

Free-text language, program, skill, level, education type, education status, and sector
values from the source SHALL be resolved against existing business catalog entries. An
unresolved value SHALL be reported and SHALL NOT be silently auto-created, and SHALL NOT
be silently dropped.

#### Scenario: Value resolves to a catalog entry

- **WHEN** a source value matches an existing catalog entry under the documented matching
  rules
- **THEN** the relation record references that catalog entry

#### Scenario: Value resolves to nothing

- **WHEN** a source value matches no catalog entry
- **THEN** no catalog entry is created, the owning row is not silently loaded without the
  value, and the unresolved value is reported for an explicit decision

#### Scenario: Operator supplies an explicit mapping

- **WHEN** an operator supplies an explicit mapping for a previously unresolved value and
  re-runs the migration
- **THEN** the affected rows resolve and load, and the value no longer appears as
  unresolved

### Requirement: Consent and retention metadata fidelity

Received date, consent date, and review due date SHALL be carried across exactly as
recorded in the source. A row whose consent metadata cannot be established SHALL be
rejected and reported, and SHALL NOT be loaded with a substituted or permissive value.

#### Scenario: Consent metadata is present

- **WHEN** a source row carries received, consent, and review due dates
- **THEN** the loaded candidate carries exactly those dates

#### Scenario: Consent metadata is missing

- **WHEN** a source row has no establishable consent date
- **THEN** the row is rejected with a consent-metadata reason and no candidate record is
  created for it

### Requirement: Logical state fidelity

Deletion, inactivity, or equivalent logical-removal state in the source SHALL be carried
across as logical state on the target record. Such rows SHALL NOT be dropped from the
migration nor loaded as active.

#### Scenario: Source row is logically removed

- **WHEN** a source row is marked removed or inactive
- **THEN** a target record exists in the corresponding logical state and is counted as
  loaded, not skipped

### Requirement: Protected staging representation

Intermediate staging data SHALL be held in a protected location for the duration of the
run and SHALL be destroyed after a successful load. Staging data SHALL NOT remain on disk
in an unprotected location after the migration ends.

#### Scenario: Migration succeeds

- **WHEN** a migration run completes successfully
- **THEN** its staging representation no longer exists

#### Scenario: Migration fails

- **WHEN** a migration run fails and staging data is retained for diagnosis
- **THEN** it is retained only in the protected location and the operator is told it
  exists and must be removed

### Requirement: Reconciliation report

The migration SHALL produce a reconciliation report containing per-entity source and
loaded row counts, rejected rows with reasons, skipped rows with reasons, unresolved
reference values with occurrence counts, and document hash verification results.

#### Scenario: Report is produced

- **WHEN** a migration run finishes, whether successfully or with rejections
- **THEN** a reconciliation report is produced covering all of the above

#### Scenario: Counts do not reconcile

- **WHEN** loaded target counts do not match the accounted-for source rows
- **THEN** the report states the discrepancy explicitly and the run is reported as not
  reconciled

### Requirement: No personal data in migration output

Migration logs and the reconciliation report SHALL identify a row by its source identifier
and the name of the failing field only. They SHALL NOT contain candidate names, contact
details, notes, document contents, storage keys, or host paths.

#### Scenario: Row is rejected on a contact field

- **WHEN** a row is rejected because its email is malformed
- **THEN** the report names the row identifier and the field, and does not contain the
  email value

#### Scenario: Report is inspected for personal data

- **WHEN** a completed report and the run's logs are scanned
- **THEN** no candidate personal data, document content, storage key, or host path is
  present

### Requirement: Synthetic fixtures outside production runs

Development, test, and continuous-integration runs SHALL use synthetic fixtures, and the
production Access dataset SHALL NOT be used outside an operator-run production migration.
The complete migration behavior — including reconciliation and rollback — SHALL be
demonstrable against synthetic fixtures alone.

#### Scenario: Automated test suite runs

- **WHEN** the migration test suite executes
- **THEN** it runs end to end against a disposable PostgreSQL instance using synthetic
  fixtures, and no production source data is required

### Requirement: Documented rollback

A rollback procedure returning the target database to its pre-migration state SHALL be
documented and SHALL be exercisable.

#### Scenario: Rollback is performed

- **WHEN** the documented rollback is applied after a migration run
- **THEN** the target database returns to its pre-migration state and a subsequent
  migration run can proceed from that state

#### Scenario: Rollback is verified

- **WHEN** rollback is exercised in automated tests against synthetic fixtures
- **THEN** the resulting state matches the pre-migration state
