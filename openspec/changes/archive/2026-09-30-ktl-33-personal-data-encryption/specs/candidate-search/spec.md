## ADDED Requirements

### Requirement: Search semantics are preserved over encrypted fields

Free-text matching and last-name ordering SHALL keep their existing observable behavior although
the fields they read are stored encrypted: the same case-insensitive literal substring match over
the same identity, contact and notes fields, the same treatment of `%`, `_` and accented characters,
the same ordering by last name then first name then identifier, the same total count, and no
duplicate candidates. Every other filter family SHALL continue to be applied by the database. Search
SHALL meet a documented latency budget of p95 at or below 300 ms for a text search or last-name sort
over 12 000 active candidates; that candidate count is the documented ceiling of this approach.

#### Scenario: Existing search cases run against encrypted data

- **WHEN** the existing search test cases run against a database whose candidate fields are
  encrypted
- **THEN** each returns the same candidates, order, pages and total count as before encryption

#### Scenario: Text search combined with other families

- **WHEN** an actor searches with free text together with status, ANY or ALL criteria, tag and CV
  filters
- **THEN** a candidate appears only when it satisfies every non-empty family, exactly once, and the
  total count matches the returned population

#### Scenario: Text contains wildcard or accented characters

- **WHEN** the search text contains `%`, `_` or accented letters
- **THEN** they match literally and with the same case and accent behavior as before encryption

#### Scenario: Sorted by last name across pages

- **WHEN** an actor pages through results sorted by last name in either direction
- **THEN** pages follow the same order as before encryption, are non-overlapping and skip no
  candidate

#### Scenario: Search at the documented ceiling

- **WHEN** the performance evidence runs a text search and a last-name sort over 12 000 active
  candidates
- **THEN** each meets the p95 budget of 300 ms, and the measured values are recorded in the change
  documentation
