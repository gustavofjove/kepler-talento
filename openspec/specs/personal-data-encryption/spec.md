# Personal Data Encryption Specification

## Purpose

Keeps candidate personal data unreadable in PostgreSQL, its dumps and its backups by storing it
only as ciphertext whose keys are held outside the database, while the application keeps reading
and searching it as before.

## Requirements

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

### Requirement: Encryption keys live outside the database

The encryption key and the blind-index key SHALL each be 256 randomly generated bits, SHALL NOT be
derived from a human-chosen password, and SHALL be distinct from each other. They SHALL be supplied
to the API from a key source outside PostgreSQL, outside the backup set, outside `.env` and outside
version control. Every encrypted value SHALL identify the key that encrypted it. Keys, key file
contents and key file locations SHALL NOT appear in logs, problem responses, audit events or the
database. The migration role and migration runner SHALL NOT hold the keys.

#### Scenario: Key material in diagnostics

- **WHEN** the API starts, encrypts, decrypts, or fails to load or use a key
- **THEN** no log line, problem response or audit event contains key material, the key file
  location, a plaintext value or a ciphertext

#### Scenario: Schema migration runs

- **WHEN** the migration runner applies schema migrations
- **THEN** it completes without access to the keys

### Requirement: The API fails closed without valid keys

The API SHALL refuse to start, in every environment, when a required key is missing, malformed, of
the wrong length, or when the configured active key is not present in the key set. There SHALL be
no configuration under which candidate personal data is written in clear.

#### Scenario: Key file is missing

- **WHEN** the API starts without its key file
- **THEN** startup fails with a stable diagnostic naming the missing key by purpose, and no request
  is served

#### Scenario: Key is malformed

- **WHEN** the key file contains a key that is not 256 bits, or the active key identifier is unknown
- **THEN** startup fails and no request is served

### Requirement: Keys can be rotated without downtime

A new encryption key SHALL be activatable while older keys remain available for reading. New writes
SHALL use the active key. An operator command SHALL re-encrypt every value not under the active key,
SHALL be idempotent and resumable, and SHALL report counts per key so an old key is retired only
after it protects no value. Rotating the blind-index key SHALL recompute every blind-index value
through the same command.

#### Scenario: Active key changes

- **WHEN** a new key becomes active while values encrypted under the previous key exist
- **THEN** all values remain readable and new or updated values are encrypted under the new key

#### Scenario: Rotation completes

- **WHEN** the re-encryption command finishes
- **THEN** its report shows zero values under the previous key, and removing that key from the key
  set leaves every value readable

#### Scenario: Rotation is interrupted

- **WHEN** the re-encryption command is stopped partway and run again
- **THEN** it completes the remaining values without corrupting or double-encrypting any value

### Requirement: E-mail equality uses a keyed blind index

Each candidate SHALL carry a blind index of its normalized e-mail address, computed with a keyed
hash under the blind-index key and indexed in the database. Every e-mail equality lookup, including
import duplicate detection, SHALL use the blind index. The blind index SHALL NOT appear in any API
response.

#### Scenario: Import meets an existing e-mail

- **WHEN** an import row's e-mail matches an existing candidate's e-mail, differing only in letter
  case or surrounding spaces
- **THEN** the documented import duplicate rule applies exactly as before encryption

#### Scenario: Candidate e-mail changes

- **WHEN** a candidate's e-mail is updated
- **THEN** its blind index is updated in the same write

#### Scenario: Candidate is returned

- **WHEN** any candidate response is serialized
- **THEN** it contains no blind-index value

### Requirement: Queries never filter or order on encrypted values

No database query SHALL filter, order, group or pattern-match on an encrypted column. An automated
check in the backend test suite SHALL fail when a query does so.

#### Scenario: Query filters on an encrypted property

- **WHEN** code introduces a query that filters or orders on an encrypted property
- **THEN** the backend test suite fails and names the offending property

### Requirement: Existing plaintext data is encrypted by a verifiable backfill

Existing rows SHALL be encrypted by an operator command that runs in bounded batches, is idempotent
and resumable, fills the blind index, and produces a reconciliation report proving row counts are
unchanged and every encrypted value decrypts. From the schema migration onward the database SHALL
reject any new or updated unencrypted value in an encrypted column, and the API SHALL refuse to
serve requests while any stored value in an encrypted column is not encrypted. The rollout SHALL
end by rewriting the affected tables so no superseded plaintext row versions remain in the database
files.

#### Scenario: Backfill runs on migrated data

- **WHEN** the backfill command runs against a database holding plaintext candidates
- **THEN** every candidate and related value is encrypted, the report shows equal row counts before
  and after, and every value decrypts

#### Scenario: Backfill is re-run

- **WHEN** the backfill command runs again after completing
- **THEN** it changes nothing and reports every value as already encrypted

#### Scenario: API starts before the backfill finished

- **WHEN** the API starts while any encrypted column still holds an unencrypted value
- **THEN** startup fails with a stable diagnostic that names the table but no value, and no request
  is served

#### Scenario: Unencrypted value is written directly

- **WHEN** a direct database write stores an unencrypted value in an encrypted column after the
  schema migration
- **THEN** the database rejects the write
