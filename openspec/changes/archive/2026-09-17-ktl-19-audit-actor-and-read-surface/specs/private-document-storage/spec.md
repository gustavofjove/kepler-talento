## MODIFIED Requirements

### Requirement: Document lifecycle auditing without content

Upload acceptance, scan outcome, primary designation change, download, and removal SHALL each
record an audit event carrying the acting identity, the document and candidate identifiers, the
kind of event, its outcome, and the request correlation identifier. Audit events SHALL NOT
record file content, the original filename, or any storage key or path.

The acting identity SHALL be the actor's **internal user identifier**, never an email address, a
display name or the external subject issued by the identity provider. An event produced by a
background process — a scan verdict arriving after the request that uploaded the file, or a storage
reconciliation — SHALL record the system actor rather than an absent one.

A download SHALL be audited as a read of personal data: it identifies a specific person and is one
of the two reads the trail exists to answer for.

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

#### Scenario: Acting identity is inspected

- **WHEN** the acting identity on a document audit event is inspected
- **THEN** it is an internal user identifier or the system actor, and it is not an email address, a
  display name or an external subject identifier

#### Scenario: Background process produces an event

- **WHEN** a scan verdict or a storage reconciliation records an audit event with no person behind it
- **THEN** the event records the system actor, distinguishable from an absent actor
