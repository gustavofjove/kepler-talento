## ADDED Requirements

### Requirement: Document upload acceptance

The system SHALL expose an operation that accepts a candidate document's content together with
its business type and primary flag. Acceptance SHALL validate size and format before any
permanent effect, write the content to private quarantine under an application-generated opaque
key, record the document with a pending availability state, and schedule scanning durably. A
rejected upload SHALL leave no document record and no stored object.

#### Scenario: Content is accepted

- **WHEN** an authorized actor uploads a non-empty file within the size limit whose extension and
  detected content agree and match the allowlist
- **THEN** the content is written to quarantine, a document record exists with a pending
  availability state, scanning is durably scheduled, and the response reports acceptance rather
  than availability

#### Scenario: Upload carries no content

- **WHEN** an upload names a document but carries no file content
- **THEN** it is rejected with a stable validation code and no document record is created

#### Scenario: Rejected upload leaves nothing behind

- **WHEN** an upload is rejected for size, empty content, or a disallowed or mismatched format
- **THEN** no document record exists, no object remains in quarantine or available storage, and
  the candidate's document collection is unchanged

#### Scenario: Stored content is retrievable as uploaded

- **WHEN** an accepted document later becomes available and is retrieved
- **THEN** its content is byte-for-byte what was uploaded, and its recorded size and content
  digest match that content

### Requirement: Observable availability state

Every candidate document SHALL carry an availability state that a permitted caller can read,
distinguishing "not yet scanned", "available", and "refused" along with a stable non-technical
reason for a refusal. The state SHALL be readable for a single document and for a candidate's
whole document collection, and SHALL never expose scanner internals, storage keys, or file
content.

#### Scenario: Freshly uploaded document is inspected

- **WHEN** a permitted caller reads a document that has been accepted but not yet scanned
- **THEN** the response reports a pending state, and the document is not downloadable

#### Scenario: Document becomes available

- **WHEN** a pending document receives a clean scan result and a permitted caller reads it again
- **THEN** the response reports an available state and the document is downloadable

#### Scenario: Refused document is inspected

- **WHEN** a permitted caller reads a document that was found infected, rejected, or unscannable
- **THEN** the response reports a refused state with a stable reason code, the document remains
  undownloadable, and no scanner signature detail, storage key, or file content is disclosed

#### Scenario: Refused document is not hidden

- **WHEN** a candidate's document collection contains a refused document
- **THEN** that document still appears in the collection with its refused state, rather than
  being omitted

### Requirement: Terminal refusal of unclean content

A document that has been recorded as infected, rejected, or unscannable SHALL NOT become
available by any later operation, including a scan retry, a restart, or reconciliation.
Availability SHALL follow only from a clean scan of the originally accepted content.

#### Scenario: Infected document is rescanned

- **WHEN** a scan operation runs again for a document already recorded as infected
- **THEN** the document remains unavailable and its recorded outcome is unchanged

#### Scenario: Refusal survives a restart

- **WHEN** the API and worker restart after a document was refused
- **THEN** the document is still refused and still undownloadable

### Requirement: Per-operation document authorization

Uploading and downloading a candidate document SHALL each require its own capability, distinct
from the capability to read candidate records. Both operations SHALL fail closed for an
unauthenticated actor and for an authenticated actor lacking the required capability, returning
no document content, filename, or existence signal. Possession of a document identifier SHALL
NOT confer access.

#### Scenario: Unauthenticated caller uploads

- **WHEN** an unauthenticated caller attempts an upload
- **THEN** the request is refused, nothing is stored, and no scanning work is scheduled

#### Scenario: Actor may read candidates but not download documents

- **WHEN** an actor permitted to read candidates requests a clean document without the download
  capability
- **THEN** the request is refused and no content or filename is returned

#### Scenario: Actor may download but not upload

- **WHEN** an actor permitted to download documents attempts an upload
- **THEN** the request is refused and no document record is created

#### Scenario: Refusal does not disclose existence

- **WHEN** an unauthorized caller names a document identifier
- **THEN** the refusal does not reveal whether a document with that identifier exists or what its
  state is

### Requirement: Document removal leaves no orphan

Removing a candidate document SHALL remove its metadata record and its stored object in every
availability state, including a pending object in quarantine and a refused one. Removal SHALL
NOT leave a stored object without a record, nor a record without its object.

#### Scenario: Available document is removed

- **WHEN** an authorized actor removes an available document
- **THEN** the record and the stored object are both gone, and reconciliation reports no orphan

#### Scenario: Pending document is removed

- **WHEN** an authorized actor removes a document that is still awaiting a scan result
- **THEN** the record and the quarantined object are both gone, and any pending scan work for it
  completes without recreating either

#### Scenario: Refused document is removed

- **WHEN** an authorized actor removes an infected or unscannable document
- **THEN** the record and its stored object are removed and the removal is audited

### Requirement: Document lifecycle auditing without content

Upload acceptance, scan outcome, primary designation change, download, and removal SHALL each
record an audit event carrying the acting identity, the document and candidate identifiers, the
kind of event, its outcome, and the request correlation identifier. Audit events SHALL NOT
record file content, the original filename, or any storage key or path.

#### Scenario: Upload and scan are audited

- **WHEN** a document is accepted and later scanned
- **THEN** separate audit events record acceptance and the scan outcome, each identifying actor,
  document, candidate, and correlation

#### Scenario: Infected upload is audited

- **WHEN** scanning identifies malware
- **THEN** an audit event records the detection without file content, filename, or storage key

#### Scenario: Download is audited

- **WHEN** a permitted caller downloads an available document
- **THEN** an audit event records the access, and the event contains no file content

#### Scenario: Audit event is inspected

- **WHEN** any document audit event is inspected
- **THEN** it contains no file bytes, no original filename, and no storage key, path, or root
