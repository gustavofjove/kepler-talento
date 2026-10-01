## MODIFIED Requirements

### Requirement: Position requirements use the candidate-search contract

Each position SHALL store a complete normalized copy of the current candidate-search filter contract, including its schema version, which is version 2. Create and update SHALL apply the same availability, criterion, mode, CV-state, length and normalization rules as candidate search. Empty requirements SHALL be valid and SHALL mean that every active candidate is eligible under the existing search semantics. An unreadable stored filter document SHALL be refused and SHALL never be replaced by empty requirements.

#### Scenario: Valid requirements are saved

- **WHEN** a manager saves a position with valid multi-family ANY and ALL criteria
- **THEN** later position reads return the normalized complete filter value with the same meaning

#### Scenario: Invalid requirements are submitted

- **WHEN** a position write contains an unsupported availability value or checked-from date, mode, CV selection or malformed criterion
- **THEN** the write is rejected with stable validation details and no partial position change is stored

#### Scenario: Stored filter version is unsupported

- **WHEN** the persisted requirements cannot be parsed under a supported filter schema
- **THEN** reading or evaluating them fails with a stable problem and does not run an empty search

#### Scenario: Requirements stored before version 2

- **WHEN** a position whose requirements selected candidate statuses before this change is read
- **THEN** its requirements are returned at version 2 with an unrestricted availability family and every other criterion unchanged
