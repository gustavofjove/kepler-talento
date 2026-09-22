## ADDED Requirements

### Requirement: Position permission vocabulary and seeded assignments

The shared API and SPA permission vocabulary SHALL include exactly `positions.read` and `positions.manage` for the position capability. `positions.read` SHALL govern listing and reading position data. `positions.manage` SHALL govern creating, editing, closing and reopening positions. Neither permission SHALL imply the other, `candidates.read`, `presets.manage`, catalog access or document access.

The seeded roles `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly` and `system_admin` SHALL hold `positions.read`. Only `rrhh_admin` and `rrhh_user` SHALL receive `positions.manage` by default. Permission seed changes SHALL be idempotent and SHALL NOT overwrite custom-role assignments.

#### Scenario: Shared vocabulary is inspected

- **WHEN** the API and SPA permission catalogues are compared
- **THEN** both contain `positions.read` and `positions.manage` with no alias or underscore variant

#### Scenario: Seeded read roles are inspected

- **WHEN** the five system roles are read after migration
- **THEN** each contains `positions.read`

#### Scenario: Seeded management roles are inspected

- **WHEN** the system roles are read after migration
- **THEN** `rrhh_admin` and `rrhh_user` contain `positions.manage` and the other seeded roles do not

#### Scenario: Manager lacks candidate permission

- **WHEN** a custom actor holds `positions.manage` but not `candidates.read`
- **THEN** the actor may perform authorized position writes but gains no candidate data or match count

#### Scenario: Permission migration is repeated

- **WHEN** the role-seed update is applied to an already updated database
- **THEN** seeded permissions are not duplicated and custom roles retain their configured permissions
