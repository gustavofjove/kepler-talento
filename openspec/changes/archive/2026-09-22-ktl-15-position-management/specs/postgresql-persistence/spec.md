## ADDED Requirements

### Requirement: Position workflow persistence uses least privilege

Position workflow state SHALL be stored in the application-owned PostgreSQL database under the registered `OPS_` physical prefix, whose documented ownership SHALL include recruitment workflow state. The position table SHALL enforce title uniqueness under case- and accent-insensitive normalization, the `open`/`closed` status vocabulary, field bounds, JSON-object requirements, supported filter schema version and timestamp ordering. It SHALL provide indexes for the default bounded list and supported filters.

The migration role SHALL own schema changes. `ktl_runtime` SHALL receive only `SELECT`, `INSERT` and `UPDATE` on position state and SHALL NOT receive `DELETE`, DDL, ownership or role-management privileges. Browsers SHALL have no direct database access. Schema creation, constraints, indexes, grants and seeded permission updates SHALL ship in the same repeatable migration slice.

#### Scenario: Position schema is inspected

- **WHEN** the KTL-15 migration has been applied
- **THEN** the quoted `"OPS_Positions"` table has the required constraints, concurrency token and listing indexes

#### Scenario: Duplicate normalized titles race

- **WHEN** two concurrent writes attempt titles that normalize to the same value
- **THEN** the database accepts at most one and the other is surfaced as the stable title conflict

#### Scenario: Runtime role changes positions

- **WHEN** the API connects as `ktl_runtime`
- **THEN** it can select, insert and update positions through approved use cases

#### Scenario: Runtime role attempts position deletion

- **WHEN** `ktl_runtime` attempts to delete a position row
- **THEN** PostgreSQL refuses the operation

#### Scenario: Default listing is explained

- **WHEN** a representative default open-position page is analyzed
- **THEN** the query is bounded, uses an intended index and does not deserialize the requirements document for the list projection

#### Scenario: Browser attempts direct persistence

- **WHEN** the position frontend creates, reads or updates state
- **THEN** it does so only through the ASP.NET Core API and no Supabase or new local-storage path is used
