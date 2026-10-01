## MODIFIED Requirements

### Requirement: Candidate record field set

The candidate record SHALL persist identity (first name, last name), contact details
(phone, email), location (location, province, country), source, notes, the availability
check (value, check date, until date and checker), consent and retention metadata (received
date, consent date, review due date), active state, creation and update timestamps, and a
concurrency token. Identity, contact, location, country, source and notes SHALL be persisted
encrypted as defined by the personal-data-encryption capability. Because the database cannot
inspect an encrypted value, required identity SHALL be enforced by application validation before
any write, and the database SHALL still reject a missing value.

#### Scenario: Candidate is persisted and read back

- **WHEN** a candidate carrying every field is written and read back
- **THEN** every field round-trips with its stored value unchanged

#### Scenario: Required identity is missing

- **WHEN** a write omits first name or last name, or supplies only whitespace
- **THEN** the write is rejected with a stable validation problem and the previous valid
  state is preserved

#### Scenario: Required identity is missing in a direct database write

- **WHEN** a direct database write supplies no value for first name or last name
- **THEN** the database rejects the write

## ADDED Requirements

### Requirement: Constrained availability check

The availability check SHALL be constrained at the database level, not by application convention
alone:

- the value SHALL be `unknown`, `available` or `unavailable`, and SHALL default to `unknown`;
- an `unknown` check SHALL have no check date and no checker, and a known check SHALL have a check
  date;
- an until date SHALL exist only on an `unavailable` check and SHALL NOT be earlier than its check
  date;
- the checker SHALL reference an existing application user, and that user SHALL NOT be deletable
  while referenced.

The value, dates and checker SHALL be stored in clear, because they are not free text. The
candidate record SHALL hold no status and no free-text availability column.

#### Scenario: Unknown value is written

- **WHEN** a direct database write sets an availability value outside the permitted set
- **THEN** the database rejects the write

#### Scenario: Inconsistent check is written

- **WHEN** a direct database write stores an `unknown` check with a check date, a known check
  without one, an until date on a non-`unavailable` check, or an until date before the check date
- **THEN** the database rejects the write

#### Scenario: Candidate is written without availability

- **WHEN** a candidate row is inserted without availability values
- **THEN** it is stored as `unknown` with no dates and no checker

## REMOVED Requirements

### Requirement: Constrained candidate status

**Reason**: The candidate no longer has a status; the availability check and the position stages
replace it.
**Migration**: The column and its constraint are dropped by the migration that adds the
availability columns. Existing values are discarded; all current data is test data.
