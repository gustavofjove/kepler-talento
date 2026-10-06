## MODIFIED Requirements

### Requirement: Candidate document metadata

A candidate document SHALL exist only as the result of an accepted upload that carried content;
no operation SHALL create a document record from metadata alone. The system SHALL record
document metadata — type, original filename, media type, size, primary flag, upload timestamp
and availability state — against a candidate, and SHALL keep at most one document marked primary
per candidate as a stored invariant that holds under concurrent writes rather than as a writer
convention. Document metadata SHALL NOT expose an internal storage path or key.

Designating an existing document as primary SHALL be refused, with a stable code and without
changing which document is primary, when that document is not available (its scan is not clean,
or it is a legacy record without a stored binary). The refusal SHALL be decided only after the
caller's authentication and document-management permission have been checked. An upload MAY
still designate the uploaded document primary while its scan is pending.

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

- **WHEN** an available document is marked primary while another already is
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

#### Scenario: An unavailable document is marked primary

- **WHEN** an actor holding the document-management permission marks primary a document that is
  pending scan, infected, rejected, unscannable, or a legacy record without a stored binary
- **THEN** the request is refused as a conflict with the stable code `document.not_available`,
  the candidate's primary document is unchanged, no primary-change audit event is recorded, and
  the response discloses no storage key, path or scanner detail

#### Scenario: Unauthorized caller marks an unavailable document primary

- **WHEN** an unauthenticated caller, or one without the document-management permission, marks
  any document primary
- **THEN** the request is refused for authorization exactly as for an available document, without
  revealing the document's availability

#### Scenario: Upload designates a pending document primary

- **WHEN** an authorized actor uploads a document with the primary option selected
- **THEN** the uploaded document is primary while its scan is still pending, and any previously
  primary document is no longer primary
