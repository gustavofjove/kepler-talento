# Candidate Import Specification

## Purpose

Defines how a file of candidate rows is uploaded, scanned, validated as a dry run and then committed
into real candidates on the server — with per-row outcomes, idempotent re-runnable commits, and none
of the imported personal data escaping into reports or logs.

## Requirements

### Requirement: Import is a two-step operation

Importing SHALL consist of two explicit steps. First the caller uploads a file and the system
validates it as a **dry run**, producing a per-row outcome report and changing no candidate data.
Second, the caller commits that validated batch in a separate call, which creates candidates.

A commit SHALL be refused unless the named batch has completed validation. Validation SHALL never
create, update or deactivate a candidate.

#### Scenario: File is validated as a dry run

- **WHEN** a permitted actor uploads a file and validation completes
- **THEN** a row outcome report is returned and no candidate has been created, changed or removed

#### Scenario: Validated batch is committed

- **WHEN** a permitted actor commits a batch that completed validation without row failures
- **THEN** the candidates described by its rows exist, and the batch reports how many were loaded

#### Scenario: Commit precedes validation

- **WHEN** a caller commits a batch that has not completed validation
- **THEN** the request is refused with a stable problem and no candidate is created

#### Scenario: Batch is committed twice

- **WHEN** a caller commits a batch that is already committed
- **THEN** the request is refused or answers with the original outcome, and no candidate is created
  a second time

### Requirement: Uploaded import files are private, scanned and quarantined

An uploaded import file SHALL be stored outside the webroot under an opaque key, SHALL be scanned
for malware, and SHALL remain quarantined until the scanner reports it clean. No component SHALL
parse, read or validate the file's content before that verdict.

A file the scanner reports as infected SHALL be refused terminally: its batch SHALL NOT become
validatable by any retry, and the content SHALL NOT be parsed. A file that cannot be scanned SHALL
be refused rather than admitted.

No response SHALL contain the file's storage key, path or any internal storage location. The system
SHALL accept only the documented import file types and SHALL refuse a file whose actual content does
not match its declared type.

#### Scenario: File is uploaded

- **WHEN** a permitted actor uploads an import file
- **THEN** it is stored under an opaque key outside the webroot, quarantined, and queued for
  scanning, and the response reveals no storage location

#### Scenario: Validation is requested before the scan finishes

- **WHEN** validation is requested for a batch whose file is still quarantined
- **THEN** the file is not parsed and the caller is told the batch is not yet ready

#### Scenario: File is infected

- **WHEN** the scanner reports an uploaded file infected
- **THEN** the batch is terminally refused, the content is never parsed, and no retry admits it

#### Scenario: File cannot be scanned

- **WHEN** the scanner cannot produce a verdict for an uploaded file
- **THEN** the batch is refused rather than admitted, and the content is never parsed

#### Scenario: Declared type does not match content

- **WHEN** a file is uploaded whose actual content is not the documented import type its name or
  declared type claims
- **THEN** it is refused with a stable problem and is not parsed

### Requirement: Documented import file contract

The system SHALL document the import file contract: the accepted file types, the required and
optional columns, the value format of each column, the maximum row count and the maximum file size.
A file that breaks the contract SHALL be refused with a structural problem that names the file and
the offending column, and SHALL NOT be reported as a set of per-row failures.

A structural problem MAY carry detail, because it describes files and columns. It SHALL NOT carry a
value from any data row.

#### Scenario: Required column is missing

- **WHEN** an uploaded file lacks a required column
- **THEN** validation reports a structural problem naming the missing column, and no rows are
  evaluated

#### Scenario: Row count exceeds the maximum

- **WHEN** an uploaded file carries more data rows than the documented maximum
- **THEN** it is refused with a stable problem naming the limit, before any row is evaluated

#### Scenario: File is empty or header-only

- **WHEN** an uploaded file contains no data rows
- **THEN** validation completes reporting zero rows, and a commit of that batch creates nothing

#### Scenario: Structural problem is inspected

- **WHEN** a structural problem is returned or recorded
- **THEN** it names the file and the column and contains no value from any data row

### Requirement: Every row ends in exactly one outcome

Every data row in a validated file SHALL end in exactly one of three outcomes — **loaded**,
**rejected** or **skipped** — and every rejected or skipped row SHALL carry a stable reason code
identifying the failing rule and the field it failed on. The sum of the three SHALL equal the file's
data row count, so that no row is unaccounted for.

A row outcome SHALL identify the row by its source row number and the failing field name. It SHALL
NOT contain the offending value, nor any other value from the row.

#### Scenario: Validation completes

- **WHEN** validation of a file finishes
- **THEN** the loaded, rejected and skipped counts sum to the file's data row count

#### Scenario: Row fails a rule

- **WHEN** a row fails a validation or resolution rule
- **THEN** it is recorded as rejected with a stable reason code naming the rule and the field

#### Scenario: Row outcome is inspected for personal data

- **WHEN** a row outcome is returned or stored
- **THEN** it carries a row number, a field name and a reason code, and no value from the row

#### Scenario: Two rows describe the same person

- **WHEN** a file contains two rows the contract considers the same person
- **THEN** each row receives its own explicit outcome and the batch does not create two candidates
  for them

### Requirement: Reference values are resolved, never created

Catalog-backed values in an import file SHALL be resolved against existing business catalog entries
under the documented matching rules. An unresolved value SHALL be reported and SHALL NOT be silently
auto-created, and SHALL NOT be silently dropped: the owning row SHALL NOT load without it.

No import run SHALL create, rename or reactivate a catalog entry.

#### Scenario: Value resolves to a catalog entry

- **WHEN** a row's value matches an existing catalog entry under the documented matching rules
- **THEN** the created candidate's relation references that entry

#### Scenario: Value resolves to nothing

- **WHEN** a row's value matches no catalog entry
- **THEN** no catalog entry is created, the row does not load without the value, and the unresolved
  value is reported for an explicit decision

#### Scenario: Catalog is inspected after an import

- **WHEN** the catalog is compared before and after any import run
- **THEN** no entry has been created, renamed or reactivated by the import

#### Scenario: Unresolved value is reported

- **WHEN** an unresolved value is reported
- **THEN** it names the catalog family and the value, because catalog vocabulary is not personal
  data, and it names no candidate

### Requirement: Validation and commit are durable, idempotent operations

Validation and commit SHALL each run as a durable operation whose record exists before the request
reports acceptance, so that neither is lost to a restart. A commit SHALL be idempotent: committing
the same batch more than once, or resuming one after a restart, SHALL produce the same candidates as
committing it exactly once.

A commit interrupted partway SHALL leave no partial batch: on resumption it SHALL complete the
remaining rows without duplicating the rows already written, and a batch SHALL never be left in a
state where some of its rows are permanently unaccounted for.

#### Scenario: Commit is re-run

- **WHEN** a batch of N rows is committed and then committed again
- **THEN** the same candidates exist as after the first commit, and none is duplicated

#### Scenario: Process restarts mid-commit

- **WHEN** the API restarts while a commit is partway through its rows
- **THEN** no partial batch remains, the commit resumes, and the resulting candidates match a single
  uninterrupted commit

#### Scenario: Operation record precedes acceptance

- **WHEN** a validation or commit is accepted
- **THEN** its durable record already exists, so the work survives an immediate restart

#### Scenario: Same file is uploaded twice deliberately

- **WHEN** an actor uploads the same file a second time as a new batch and commits it
- **THEN** the documented duplicate rule decides each row's outcome explicitly, rather than silently
  creating a second set of candidates or silently doing nothing

### Requirement: Imported candidates are ordinary candidates

A candidate created by a commit SHALL be subject to the same domain rules as one created through the
candidate endpoints: the same field validation, the same status constraints, the same consent and
retention handling, and the same audit event. A commit SHALL NOT bypass a rule that a single
candidate write enforces, and SHALL NOT set a field no candidate write can set.

A row that would produce a candidate the domain refuses SHALL be rejected with its reason code
rather than loaded in a weakened form.

#### Scenario: Imported candidate is read back

- **WHEN** a candidate created by a commit is read through the candidate endpoint
- **THEN** it carries the same shape and obeys the same rules as a candidate created directly

#### Scenario: Row would break a domain rule

- **WHEN** a row would produce a candidate that the domain refuses
- **THEN** the row is rejected with a stable reason code and no weakened candidate is created

#### Scenario: Commit is audited

- **WHEN** a commit creates candidates
- **THEN** each creation produces the same audit event a direct creation produces

### Requirement: Batch history is server-owned

The system SHALL store each batch with its lifecycle state, its counts, its timestamps and the actor
who created it, and SHALL expose the batch list and a single batch's row report to callers holding
the import permission. Batch history SHALL NOT be stored in browser storage.

A batch identifier SHALL be server-assigned and unguessable. The batch list SHALL be paged and SHALL
be ordered most recent first.

#### Scenario: Batches are listed

- **WHEN** a permitted actor lists batches
- **THEN** a page of batches is returned most recent first, with their state, counts and timestamps

#### Scenario: Row report is read

- **WHEN** a permitted actor reads a batch's row report
- **THEN** the per-row outcomes are returned with their reason codes and no row values

#### Scenario: Browser storage is inspected

- **WHEN** browser storage is inspected after an import
- **THEN** it holds no batch record, no row outcome and no imported candidate data

### Requirement: Uploaded files are purged on a documented schedule

The uploaded file backing a closed batch SHALL be purged on a documented schedule after the batch
reaches a terminal state, so that a bulk collection of candidate personal data is not retained
indefinitely. Purging the file SHALL NOT remove the batch's counts and row outcomes, which carry no
personal data.

A batch whose file has been purged SHALL report that state rather than failing obscurely when its
file is requested.

#### Scenario: Closed batch passes its retention window

- **WHEN** a batch has been in a terminal state for longer than the documented window
- **THEN** its uploaded file is removed from storage and its counts and row outcomes remain readable

#### Scenario: Purged batch is revisited

- **WHEN** an actor opens a batch whose file has been purged
- **THEN** the batch reports that its file is no longer retained, and its report still renders

#### Scenario: Purge leaves no orphan

- **WHEN** a purge runs
- **THEN** no stored import file remains without a batch, and no batch references a file that is
  gone without saying so

### Requirement: Import authorization fails closed

Every import endpoint — upload, validate, commit, list batches and read a row report — SHALL check
authentication and then the `candidates.import` permission before validating the request, reading
the file, or revealing whether a named batch exists. A caller without a valid token SHALL receive
the unauthenticated refusal; an authenticated caller without the permission SHALL receive the
forbidden refusal.

Holding `candidates.create` SHALL NOT by itself permit an import, and holding `candidates.import`
SHALL NOT permit reading candidates.

#### Scenario: Unauthenticated upload

- **WHEN** an unauthenticated caller uploads a file
- **THEN** the request is refused as unauthenticated, the file is not stored, and no batch is created

#### Scenario: Authenticated caller lacks the permission

- **WHEN** an authenticated actor without `candidates.import` invokes any import endpoint
- **THEN** the request is refused as forbidden and no batch data is returned

#### Scenario: Refusal precedes validation

- **WHEN** an unauthorized caller sends a malformed import request
- **THEN** the refusal is the authorization problem, identical to the refusal for a well-formed
  request, and no validation detail is returned

#### Scenario: Forbidden caller probes for a batch

- **WHEN** an actor without the permission requests a batch identifier that does not exist
- **THEN** the refusal is the forbidden problem, indistinguishable from the refusal for an existing
  batch

#### Scenario: Candidate creation permission is not an import permission

- **WHEN** an actor holding `candidates.create` but not `candidates.import` invokes an import
  endpoint
- **THEN** the request is refused as forbidden

### Requirement: Imported personal data stays out of diagnostics

No value from an imported file SHALL reach an application, request, operation or diagnostic log, at
any outcome. An import SHALL be diagnosable from safe operational metadata only: the batch
identifier, the correlation identifier, the row number, the field name, the reason code and the
counts.

The original filename SHALL be treated as caller-supplied text: it MAY be shown back to the actor
who uploaded it, and SHALL NOT be logged, because a filename routinely names a person.

#### Scenario: Row fails during validation

- **WHEN** a row is rejected
- **THEN** the log entry carries the batch identifier, row number, field and reason code, and no
  value from the row

#### Scenario: Commit fails partway

- **WHEN** a commit fails partway through a file
- **THEN** the failure is diagnosable from batch and operation metadata, and no candidate data
  appears in the logs

#### Scenario: Upload is logged

- **WHEN** a file is uploaded
- **THEN** the log entry contains no original filename and no file content
