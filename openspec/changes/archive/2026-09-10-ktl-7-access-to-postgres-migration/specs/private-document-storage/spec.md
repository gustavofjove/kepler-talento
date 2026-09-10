## MODIFIED Requirements

### Requirement: Quarantine before availability

Every accepted document SHALL remain in a private quarantine state until automated malware
scanning reports a clean result, whether it entered the system through an interactive
upload or through a data migration. Migrated content SHALL NOT be treated as trusted by
virtue of its origin.

#### Scenario: Clean scan

- **WHEN** a quarantined document receives a clean scan result
- **THEN** it is atomically promoted to private available storage and its metadata records
  the scan result

#### Scenario: Malware is detected

- **WHEN** scanning identifies malware
- **THEN** the document never becomes available, the detection is audited without file
  content, and the quarantine item follows the configured rejection policy

#### Scenario: Scanner times out or errors

- **WHEN** scanning times out, fails, or is unavailable
- **THEN** the new document remains unavailable and the failure is recorded for retry or
  operator review

#### Scenario: Document arrives through a data migration

- **WHEN** a document is ingested by a data migration rather than an interactive upload
- **THEN** it passes the same allowlist, size, quarantine, and scanning gates, and becomes
  available only on a clean scan result

#### Scenario: Migrated document fails scanning

- **WHEN** a migrated document is infected or unscannable
- **THEN** it never becomes available, and the migration reports it as rejected without
  including its content or storage key

## ADDED Requirements

### Requirement: Ingested document content verification

When a document is ingested from an external source that supplies or permits computing a
content hash, the system SHALL record a hash of the stored content and SHALL support
verifying it against the source. A mismatch SHALL be reported and the document SHALL NOT
be treated as successfully ingested.

#### Scenario: Stored content matches the source

- **WHEN** an ingested document's stored content is verified against its source hash
- **THEN** the hashes match and the document is reported as successfully ingested

#### Scenario: Stored content does not match the source

- **WHEN** an ingested document's stored content hash differs from its source hash
- **THEN** the mismatch is reported against the document identifier, without exposing the
  content, the storage key, or any host path, and the document is not counted as
  successfully ingested
