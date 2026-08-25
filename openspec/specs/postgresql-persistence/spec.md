# PostgreSQL Persistence Specification

## Purpose

Defines the PostgreSQL persistence contract, naming rules, migration safety, and database
validation required before Kepler Talento moves business data away from browser and
Supabase-specific persistence.

## Requirements

### Requirement: Application-owned PostgreSQL boundary

The backend SHALL persist reference and infrastructure records in the application's own
PostgreSQL database without requiring Supabase schemas, helpers, RLS policies, Storage,
or Edge Functions.

#### Scenario: Reference data is persisted

- **WHEN** the reference dataset is initialized and queried
- **THEN** its records are stored in and returned from the application PostgreSQL schema

#### Scenario: Supabase is unavailable

- **WHEN** no Supabase endpoint or credentials are configured
- **THEN** the reference API, migrations, and infrastructure tests continue to operate
  against PostgreSQL

### Requirement: Explicit prefixed table names

Every mapped application table SHALL have an explicit, quoted physical name whose prefix
matches its approved data category: `CND_`, `CAT_`, `OPS_`, `AUD_`, or reserved `ADM_`.

#### Scenario: Migration creates candidate table

- **WHEN** database migrations create the candidate reference table
- **THEN** the physical PostgreSQL name begins with `CND_` and preserves its configured
  case

#### Scenario: Mapping lacks an approved prefix

- **WHEN** a mapped application entity uses a convention name or an unapproved prefix
- **THEN** automated naming validation fails

### Requirement: Clean application names

Database prefixes SHALL NOT be required in application entity, request, response,
namespace, or collection names.

#### Scenario: Candidate mapping is inspected

- **WHEN** the candidate entity maps to a `CND_` table
- **THEN** its application-facing name remains `Candidate` without the physical prefix

### Requirement: Controlled migration execution

Database migrations SHALL be executable as an explicit deployment action and SHALL NOT
run implicitly during production API startup.

#### Scenario: Production API starts with pending migration

- **WHEN** the production API starts before an approved pending migration is applied
- **THEN** it reports an unhealthy/not-ready database state and does not mutate the
  schema automatically

#### Scenario: Deployment applies migrations

- **WHEN** an operator runs the documented migration action against a compatible database
- **THEN** all pending migrations are applied once and the resulting schema passes its
  validation checks

### Requirement: Database invariants

The PostgreSQL schema SHALL enforce keys, required fields, uniqueness, relationship
behavior, and business-critical constraints needed by each mapped record.

#### Scenario: Invalid reference record bypasses application validation

- **WHEN** a database write violates a declared key, required field, uniqueness, or
  relationship constraint
- **THEN** PostgreSQL rejects the write and preserves the previous valid state

### Requirement: PostgreSQL-specific integration evidence

Persistence integration validation SHALL run against a disposable PostgreSQL instance
and SHALL verify migrations, quoted names, constraints, transactions, and configured
data types.

#### Scenario: Persistence integration suite runs

- **WHEN** the backend integration suite executes
- **THEN** it creates a clean PostgreSQL database, applies migrations, verifies the
  mapped behavior, and removes the disposable test state

### Requirement: Synthetic foundation data

KTL-5 SHALL use synthetic reference records only and SHALL NOT import the Access
production dataset or treat browser/Supabase demo records as authoritative.

#### Scenario: Foundation database is seeded

- **WHEN** development or integration seed data is created
- **THEN** all candidate-like values are synthetic and no Access production row is copied
