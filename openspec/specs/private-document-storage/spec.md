# Private Document Storage Specification

## Purpose

Defines how candidate documents are accepted, quarantined, scanned, stored, and returned
without exposing internal locations or making unsafe content available to users.

## Requirements

### Requirement: Private opaque storage

The system SHALL store candidate documents outside the webroot under
application-generated opaque relative keys and SHALL store the original filename only as
metadata.

#### Scenario: Document is stored

- **WHEN** a candidate document completes validation and scanning
- **THEN** its binary uses an application-generated relative key unrelated to the
  original filename or host path

#### Scenario: Storage metadata is returned

- **WHEN** a caller retrieves document metadata
- **THEN** the response contains functional metadata but no drive letter, UNC path, mount
  point, host directory, storage root, or permanent URL

### Requirement: Upload size limit

The system SHALL reject any candidate document whose uploaded size exceeds 20 MB, with
the same effective limit at every request and scanning boundary.

#### Scenario: Document is within the limit

- **WHEN** a permitted document is non-empty and no larger than 20 MB
- **THEN** it may proceed to validation and quarantine

#### Scenario: Document exceeds the limit

- **WHEN** an upload exceeds 20 MB
- **THEN** it is rejected before permanent storage with a stable validation error

### Requirement: Business file allowlist

The initial allowlist SHALL accept PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF, and BMP
when their extension and detected content agree. All other formats SHALL be denied unless
a later reviewed change extends the allowlist.

#### Scenario: Allowed document type matches content

- **WHEN** an uploaded file has an allowed extension and matching detected content
- **THEN** it proceeds to quarantine and scanning

#### Scenario: Extension and content disagree

- **WHEN** an uploaded filename claims an allowed extension but detected content does not
  match
- **THEN** the file is rejected and never becomes available

#### Scenario: Unsupported format is uploaded

- **WHEN** an uploaded file is an executable, script, HTML, SVG, archive submitted as a
  CV, macro-enabled Office document, or another unlisted format
- **THEN** the file is rejected with a stable unsupported-format error

### Requirement: Unscannable content denial

Encrypted, password-protected, malformed, empty, or otherwise unscannable documents SHALL
fail closed.

#### Scenario: Encrypted document cannot be inspected

- **WHEN** the scanner or content detector cannot inspect an encrypted or
  password-protected document
- **THEN** the system marks it rejected/unscannable and never permits download

### Requirement: Quarantine before availability

Every accepted upload SHALL remain in a private quarantine state until automated malware
scanning reports a clean result.

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

### Requirement: Bounded scanning work

Document scanning SHALL enforce limits on duration, expanded content size,
archive/container recursion, and contained file count.

#### Scenario: Compressed container expands excessively

- **WHEN** an allowed container document would exceed a configured expanded-size,
  recursion, file-count, or scan-time limit
- **THEN** scanning stops and the document remains unavailable with a bounded-resource
  error

### Requirement: Controlled clean download

Only a document with an available and clean scan state SHALL be downloadable, and the
download SHALL use safe content type, attachment/filename handling, `nosniff`, and
non-cacheable private response behavior.

#### Scenario: Clean document is downloaded

- **WHEN** a permitted caller requests a clean available document
- **THEN** the API streams it with safe download headers and without exposing its storage
  key or path

#### Scenario: Existing clean document while scanner is unavailable

- **WHEN** the scanner is temporarily unavailable and a permitted caller requests a
  previously clean document
- **THEN** the existing clean document remains downloadable

#### Scenario: Unclean document is requested

- **WHEN** a caller requests a pending, infected, rejected, missing, or unscannable
  document
- **THEN** the API denies the download without revealing storage details

### Requirement: Storage reconciliation

The system SHALL detect and report missing, orphaned, and stale-quarantined objects
without automatically deleting candidate history outside an explicit retention state.

#### Scenario: Metadata points to a missing object

- **WHEN** reconciliation finds a document record whose binary is missing
- **THEN** it records an operational failure associated with the document identifier

#### Scenario: Orphan binary is found

- **WHEN** reconciliation finds a binary with no matching metadata
- **THEN** it quarantines/reports the object for controlled cleanup rather than exposing
  it
