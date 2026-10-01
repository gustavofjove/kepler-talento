## MODIFIED Requirements

### Requirement: Minimal search result projection

Each search item SHALL contain only:

- the candidate identifier, first name, last name, phone, email and status;
- primary-CV presence;
- whether the primary CV can be previewed;
- whether the primary CV can be downloaded;
- update time.

It SHALL NOT return a full candidate aggregate, relation collections, tags, notes, note entries,
consent or retention metadata, document identifiers, document paths, storage keys, filenames,
content types, or scan internals.

A primary CV SHALL count as previewable only when the candidate's non-removed primary document:

- has a clean scan verdict;
- has the PDF content type;
- has its stored binary (a legacy record without a binary is not previewable).

A primary CV SHALL count as downloadable when that document has a clean scan verdict and its stored
binary, in any allowed format.

The previewable and downloadable values SHALL be `false` for every item when the actor does not
hold the document download permission. They SHALL grant no right to read the document. Primary-CV
presence keeps its existing meaning.

Tags SHALL be usable as a filter without becoming part of this projection: a candidate matched by
a tag SHALL be returned with the same fields as a candidate matched any other way.

#### Scenario: Search item is returned

- **WHEN** a candidate matches a search
- **THEN** its item contains exactly the documented search projection and no excluded personal
  or storage data

#### Scenario: Candidate is matched by a tag

- **WHEN** a candidate matches because it holds a tag the actor filtered on
- **THEN** its item carries the documented projection and does not carry the tag that matched

#### Scenario: Primary document identifier is returned

- **WHEN** a matching candidate has a non-removed primary document
- **THEN** its item does not carry the document's identifier: the identifier is no longer part of
  the projection, and document actions resolve documents through the candidate's permission-checked
  document list

#### Scenario: Primary CV can be previewed

- **WHEN** an actor holding the document download permission searches and a matching candidate's
  primary document is a clean PDF with its binary
- **THEN** the item reports that the primary CV can be previewed and carries no document
  identifier

#### Scenario: Primary CV cannot be previewed

- **WHEN** a matching candidate's primary document is pending, infected, rejected, unscannable, a
  legacy record without a binary, or not a PDF, or the candidate has no primary document
- **THEN** the item reports that the primary CV cannot be previewed, while primary-CV presence is
  reported as before

#### Scenario: Actor cannot download documents

- **WHEN** an actor holding `candidates.read` but not the document download permission searches
- **THEN** every item reports that the primary CV can be neither previewed nor downloaded,
  whatever the documents' state

#### Scenario: Primary CV can be downloaded but not previewed

- **WHEN** an actor holding the document download permission searches and a matching candidate's
  primary document is a clean `.docx` with its binary
- **THEN** the item reports that the primary CV can be downloaded and cannot be previewed
