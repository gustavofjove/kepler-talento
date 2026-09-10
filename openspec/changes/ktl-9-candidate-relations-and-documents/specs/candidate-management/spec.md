## MODIFIED Requirements

### Requirement: Candidate document metadata

A candidate document SHALL exist only as the result of an accepted upload that carried content;
no operation SHALL create a document record from metadata alone. The system SHALL record
document metadata — type, original filename, media type, size, primary flag, upload timestamp
and availability state — against a candidate, and SHALL keep at most one document marked primary
per candidate as a stored invariant that holds under concurrent writes rather than as a writer
convention. Document metadata SHALL NOT expose an internal storage path or key.

#### Scenario: Document appears after an accepted upload

- **WHEN** an authorized actor uploads a document for a candidate and the upload is accepted
- **THEN** the metadata is stored against that candidate and appears in the candidate's document
  collection with its availability state

#### Scenario: Metadata-only attachment is refused

- **WHEN** a caller attempts to add a document to a candidate without supplying content
- **THEN** the request is refused and no document record is created

#### Scenario: A second document is marked primary

- **WHEN** a document is marked primary while another already is
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

## ADDED Requirements

### Requirement: Relation collection uniqueness

Within a candidate's language, program and skill collections, the referenced value SHALL be
unique. Two entries whose values differ only by surrounding whitespace or letter case SHALL be
treated as the same value. A write introducing a duplicate SHALL be refused with a stable
validation code and a Spanish message, and SHALL leave the stored collection unchanged. This
rule SHALL be enforced where the data is stored, not only in the interface that submits it.

#### Scenario: Duplicate language is submitted

- **WHEN** a language collection is submitted containing a language the collection already holds
- **THEN** the write is refused with a stable validation code and a Spanish message, and the
  stored collection is unchanged

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
