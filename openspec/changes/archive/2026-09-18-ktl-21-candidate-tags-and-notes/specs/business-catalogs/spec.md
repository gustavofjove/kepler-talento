## MODIFIED Requirements

### Requirement: Catalog families and item shape

The system SHALL organize catalog values into the ten business families used by candidate
records and search: languages, programs, skills, language levels, program levels, skill
levels, education types, education statuses, sectors, and tags. Each catalog item SHALL carry a
stable identifier, a code, a Spanish name, an optional English name, a position within its
family, and an active flag.

The tags family SHALL have the same item shape as every other family. A tag SHALL NOT carry a
level, a status, or any attribute the other families do not have.

#### Scenario: Family is listed

- **WHEN** an authorized user requests the values of a family
- **THEN** the API returns that family's items in their configured order, each with its
  identifier, code, Spanish name, optional English name, position, and active flag

#### Scenario: Unknown family is requested

- **WHEN** a caller requests a family that is not one of the ten business families
- **THEN** the API rejects the request with a stable validation code and returns no items

#### Scenario: Tags family is administered like any other

- **WHEN** an authorized administrator creates, renames, reorders, deactivates or reactivates a
  value of the tags family
- **THEN** the operation behaves exactly as it does for any other family, through the same
  operations, with no additional field to supply

### Requirement: Default families are seeded at deployment

The ten families SHALL be populated with their default Spanish values by an explicit,
idempotent deployment action. The application SHALL NOT create catalog values implicitly at
runtime when a family is found empty.

A seeded value's code SHALL be derived from its Spanish name by the same normalization the
catalog uses for its own uniqueness. Where that derivation cannot distinguish two names within
one family, the seed SHALL carry an explicit code for those values rather than relying on
derivation. Seeded codes SHALL be unique within their family, and seeded Spanish names SHALL be
unique within their family once trimmed, case-folded, and compared without accents — the same
comparison the family's stored uniqueness uses.

Unlike a value created through the API, a seeded value SHALL NOT have its code silently
uniquified: the seed is authored, so a collision is an authoring error to be surfaced, not a
condition to be resolved automatically.

#### Scenario: Seed runs on an empty deployment

- **WHEN** the deployment seed runs against a database with no catalog values
- **THEN** the ten families are created with their default Spanish values, accents intact, in
  their documented order, all active

#### Scenario: Seed runs again

- **WHEN** the deployment seed runs against a database that already holds catalog values
- **THEN** it makes no change, and administrator edits made since the previous seed are
  preserved

#### Scenario: Tags family is seeded on an existing deployment

- **WHEN** the deployment seed runs against a database that already holds the other families but
  no tags
- **THEN** the default tags are created and no value of any other family is altered

#### Scenario: Family is emptied by deactivation

- **WHEN** every value of a family has been deactivated
- **THEN** selection screens present that family as having no options, and no default values
  reappear

#### Scenario: Two seeded names derive the same code

- **WHEN** a family's default vocabulary holds two Spanish names that derive the same code,
  because they differ only in characters the derivation folds away
- **THEN** each carries an explicit distinct code in the seed, both values are created, and
  neither displaces the other

#### Scenario: Default vocabulary contains a collision

- **WHEN** the default vocabulary of a family holds two values with the same code, or two
  Spanish names equal under trimmed, case-folded, accent-insensitive comparison
- **THEN** the collision is detected before deployment rather than as a failed insert against
  the database
