## MODIFIED Requirements

### Requirement: Default families are seeded at deployment

The nine families SHALL be populated with their default Spanish values by an explicit,
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
- **THEN** the nine families are created with their default Spanish values, accents intact, in
  their documented order, all active

#### Scenario: Seed runs again

- **WHEN** the deployment seed runs against a database that already holds catalog values
- **THEN** it makes no change, and administrator edits made since the previous seed are
  preserved

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
