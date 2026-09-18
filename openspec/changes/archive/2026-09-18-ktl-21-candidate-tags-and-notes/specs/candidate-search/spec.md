## MODIFIED Requirements

### Requirement: Full candidate search filter contract

The system SHALL search active candidates using free text, candidate statuses, skill criteria,
language criteria, program criteria, tag criteria, and primary-CV presence. Blank text and
criteria values, an empty or complete status selection, empty criterion families, and an unset CV
filter SHALL place no restriction on the result. Free text SHALL preserve current behavior across
candidate identity, contact, and notes fields using case-insensitive substring matching; it SHALL
NOT be extended to candidate note entries.

#### Scenario: Empty filters are ignored

- **WHEN** an authorized actor searches with blank text, every status selected, no criteria,
  and no CV selection
- **THEN** every non-deleted candidate within the actor's visibility scope is eligible to appear

#### Scenario: Empty tag family is ignored

- **WHEN** an authorized actor searches supplying no tag criteria
- **THEN** the tag family places no restriction on the result

#### Scenario: Filter families combine

- **WHEN** an authorized actor supplies non-empty text, status, skill, language, program, tag,
  and CV filters
- **THEN** a candidate appears only when it satisfies every non-empty filter family

#### Scenario: Text matching preserves parity

- **WHEN** text occurs as a case-insensitive substring in a candidate identity, contact, or notes
  field
- **THEN** the candidate satisfies the text family without the response exposing the field that
  matched

#### Scenario: Note entries are not searched by text

- **WHEN** a search term occurs only in a candidate's note entries and nowhere in its identity,
  contact or notes field
- **THEN** the candidate does not satisfy the text family

#### Scenario: Unknown criterion value is supplied

- **WHEN** a non-empty skill, language, program, or tag value matches no known candidate relation
- **THEN** that criterion matches no candidate rather than broadening the result or failing open

### Requirement: Multi-value criteria semantics

Each skill, language, program, and tag family SHALL independently support `ANY` and `ALL` modes.
`ANY` SHALL match when at least one criterion in that family matches; `ALL` SHALL match only when
every distinct criterion in that family matches, including when matches occur in different
relation rows. A criterion with an empty level SHALL match any level for the same value, while a
non-empty level SHALL require the same value and level using case-insensitive normalized
comparison. A tag criterion SHALL carry no level; a tag criterion supplying a level SHALL be
rejected with a stable validation problem.

#### Scenario: ANY mode matches one criterion

- **WHEN** a candidate satisfies one but not all criteria in a family whose mode is `ANY`
- **THEN** the candidate satisfies that family

#### Scenario: ALL mode spans relation rows

- **WHEN** a candidate satisfies every distinct criterion in an `ALL` family through separate
  relation rows
- **THEN** the candidate satisfies that family exactly once

#### Scenario: ALL mode is incomplete

- **WHEN** a candidate is missing one distinct criterion in an `ALL` family
- **THEN** the candidate does not satisfy that family

#### Scenario: Tags are matched in ANY mode

- **WHEN** an authorized actor supplies several tags with mode `ANY`
- **THEN** a candidate holding at least one of them satisfies the tag family

#### Scenario: Tags are matched in ALL mode

- **WHEN** an authorized actor supplies several tags with mode `ALL`
- **THEN** only a candidate holding every one of them satisfies the tag family, and it appears
  once

#### Scenario: Tag criterion supplies a level

- **WHEN** a search request contains a tag criterion carrying a level
- **THEN** the request is rejected with a stable validation problem and no results are returned

#### Scenario: Criterion omits its level

- **WHEN** a criterion names a value and has an empty level
- **THEN** a candidate relation with that value satisfies it regardless of the stored level

#### Scenario: Duplicate joins and criteria are present

- **WHEN** several relation rows or repeated normalized criteria can satisfy the same candidate
- **THEN** the candidate appears no more than once and repeated criteria do not change `ALL`
  semantics

### Requirement: Minimal search result projection

Each search item SHALL contain only candidate identifier, first name, last name, phone, email,
status, primary-CV presence, the primary document identifier when one exists, and update time.
It SHALL NOT return a full candidate aggregate, relation collections, tags, notes, note entries,
consent or retention metadata, document paths, storage keys, filenames, or scan internals.

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
- **THEN** its identifier may be present for subsequent permission-checked document actions but
  reveals no storage location and grants no download capability
