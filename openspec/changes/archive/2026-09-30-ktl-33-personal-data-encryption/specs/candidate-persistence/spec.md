## MODIFIED Requirements

### Requirement: Candidate record field set

The candidate record SHALL persist identity (first name, last name), contact details
(phone, email), location (location, province, country), availability, status, source,
notes, consent and retention metadata (received date, consent date, review due date),
active state, creation and update timestamps, and a concurrency token. Identity, contact,
location, country, availability, source and notes SHALL be persisted encrypted as defined by
the personal-data-encryption capability. Because the database cannot inspect an encrypted
value, required identity SHALL be enforced by application validation before any write, and
the database SHALL still reject a missing value.

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
