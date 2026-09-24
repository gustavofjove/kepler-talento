## MODIFIED Requirements

### Requirement: Multi-value criteria semantics

Each skill, language, program, and tag family SHALL independently support `ANY` and `ALL` modes.
`ANY` SHALL match when at least one criterion in that family matches; `ALL` SHALL match only when
every distinct criterion in that family matches, including when matches occur in different
relation rows. A criterion with an empty level SHALL match any level for the same value. A
non-empty level SHALL match the same value at that level or at any later position in the same
level family's configured catalog order, using case-insensitive normalized comparison to resolve
the named level. Active and inactive levels SHALL both retain their position in that ranking. A
non-empty level that does not resolve in the applicable family SHALL match no candidate.

A tag criterion SHALL carry no level; a tag criterion supplying a level SHALL be rejected with a
stable validation problem. Repeated criteria SHALL remain as supplied: in `ANY` mode a lower
minimum can make a higher minimum redundant, while in `ALL` mode every distinct minimum SHALL
still be satisfied. Applying a saved preset or matching a position SHALL use these same semantics
without rewriting the stored filters.

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

#### Scenario: Criterion matches its level and higher levels

- **WHEN** a language criterion names level B2 and candidates hold that language at B1, B2, and C1
- **THEN** the candidates at B2 and C1 satisfy the criterion and the candidate at B1 does not

#### Scenario: Highest level matches only itself

- **WHEN** a criterion names the final level in its family's configured order
- **THEN** only a relation at that level satisfies the criterion

#### Scenario: Reordered levels change the ranking

- **WHEN** an administrator moves a level before the selected minimum in the applicable family
- **THEN** a relation at that moved level no longer satisfies the criterion

#### Scenario: Inactive level retains its rank

- **WHEN** a candidate holds an inactive level positioned at or above the selected minimum
- **THEN** that relation satisfies the criterion

#### Scenario: Unknown level fails closed

- **WHEN** a criterion names a level that does not exist in its applicable level family
- **THEN** the criterion matches no candidate

#### Scenario: ALL mode applies independent minimums

- **WHEN** an `ALL` skill family requires Java at Medio and SQL at Alto
- **THEN** a candidate with Java at Experto and SQL at Alto satisfies the family exactly once, and
  a candidate with SQL at Medio does not

#### Scenario: Saved filters use minimum-level semantics

- **WHEN** a saved preset or position requirement contains a criterion at level B2
- **THEN** applying it matches B2 and higher levels without changing the stored filter

#### Scenario: Duplicate joins and criteria are present

- **WHEN** several relation rows or repeated normalized criteria can satisfy the same candidate
- **THEN** the candidate appears no more than once and repeated criteria do not change `ALL`
  semantics
