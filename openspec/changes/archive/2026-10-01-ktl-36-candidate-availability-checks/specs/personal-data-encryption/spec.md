## MODIFIED Requirements

### Requirement: Candidate personal data is stored only as ciphertext

Every human-entered free-text value on candidate records and their relations SHALL be persisted in
PostgreSQL only in encrypted form: candidate identity, contact details, location, country, source
and notes; note bodies; experience company, position and functions; education degree, specialty and
institution; language certification; relation notes; and document original file names. The
free-text member of stored search filter documents (saved presets and position requirements) SHALL
also be persisted encrypted. Identifiers, catalog references, status values, availability values
and dates, flags, timestamps, source keys, storage keys, content hashes and scan result codes SHALL
remain in clear. Encryption SHALL be authenticated, SHALL use a fresh random nonce for every value,
and SHALL bind each value to its table and column so that it cannot be decrypted in any other
column.

#### Scenario: Candidate is written through the API

- **WHEN** a candidate with known identity, contact and notes values is created through the API
- **THEN** reading the stored row through a plain database connection returns none of those values
  in any encrypted column

#### Scenario: Same value is stored twice

- **WHEN** two candidates are stored with the same e-mail address
- **THEN** their stored ciphertexts differ

#### Scenario: Stored value is read back

- **WHEN** an encrypted candidate is read through any API endpoint
- **THEN** every field returns exactly the value that was written

#### Scenario: Ciphertext is moved or tampered with

- **WHEN** a stored ciphertext is altered, or copied into another encrypted column or table
- **THEN** reading it fails with a stable server error, the failure is diagnosed without the value,
  and no altered or foreign plaintext is returned

#### Scenario: Database is dumped

- **WHEN** an operator takes a `pg_dump` of the database after rollout
- **THEN** the dump contains none of a seeded set of candidate names, e-mail addresses and phone
  numbers, and none of a seeded saved search term
