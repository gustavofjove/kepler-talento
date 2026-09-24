## MODIFIED Requirements

### Requirement: Catalog family reordering

An authorized administrator SHALL be able to change the order in which a family's values are
presented. Reordering SHALL apply to the family as a whole in one operation and SHALL leave the
family with consecutive positions and no duplicates or gaps.

For the `language_level`, `program_level`, and `skill_level` families, the configured order SHALL
also define search rank: the first value is the lowest level and each later value is higher. Active
and inactive values SHALL both retain their rank. The catalog administration interface SHALL
explain this search consequence for those three level families and SHALL NOT show that explanation
for other families.

#### Scenario: Value is moved within its family

- **WHEN** an authorized administrator reorders a family
- **THEN** subsequent listings return the family in the new order with consecutive positions

#### Scenario: Reorder does not cover the family

- **WHEN** a reorder request omits a value of the family or names a value from another family
- **THEN** the request is rejected with a stable validation code and the existing order is
  unchanged

#### Scenario: Concurrent reorders

- **WHEN** two reorder requests for the same family are submitted concurrently
- **THEN** the resulting order is one of the two submitted orders in full, never an interleaved
  mixture of them

#### Scenario: Level family order defines search rank

- **WHEN** an administrator reorders a level family
- **THEN** subsequent candidate searches use the new order to decide which levels are at or above
  a selected minimum

#### Scenario: Inactive level remains ranked

- **WHEN** a level value is deactivated
- **THEN** it retains its configured position for searches involving existing candidate relations

#### Scenario: Level family displays the order explanation

- **WHEN** an administrator views `language_level`, `program_level`, or `skill_level`
- **THEN** the interface explains that the first value is the lowest and order defines which levels
  are higher in search

#### Scenario: Non-level family omits the order explanation

- **WHEN** an administrator views any other catalog family
- **THEN** the interface does not describe its order as a search-level ranking
