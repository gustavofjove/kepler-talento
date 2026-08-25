## Purpose

Defines durable, restart-safe background operation behavior for document scanning and
future long-running Kepler Talento workloads without relying on process memory as the
source of truth.

## ADDED Requirements

### Requirement: Durable operation record

Every background operation SHALL persist its identifier, type, status, timestamps,
correlation identifier, attempt information, and non-sensitive outcome before work is
considered queued.

#### Scenario: Work is accepted

- **WHEN** the system accepts background work
- **THEN** a durable queued record exists before the request reports acceptance

### Requirement: Controlled state transitions

Operations SHALL use only the transitions `queued -> running -> completed|failed|cancelled`
and SHALL reject invalid or conflicting transitions.

#### Scenario: Worker completes an operation

- **WHEN** a worker owns a queued operation, performs it successfully, and records the
  outcome
- **THEN** the durable status transitions through running to completed exactly once

#### Scenario: Completed operation is started again

- **WHEN** a worker attempts to claim an already completed operation
- **THEN** the claim is rejected and the completed outcome remains unchanged

### Requirement: Restart recovery

The system SHALL recover queued operations and eligible stale-running operations after an
API/worker restart.

#### Scenario: Process stops after claiming work

- **WHEN** a worker stops before completing an operation and its lease becomes stale
- **THEN** a later worker can safely reclaim or fail the operation according to its retry
  policy

#### Scenario: Queued work survives restart

- **WHEN** the application restarts with queued operations in PostgreSQL
- **THEN** those operations are discovered without requiring the original in-memory queue

### Requirement: Idempotent execution

Retrying or recovering an operation SHALL NOT duplicate a completed business effect or
create a second artifact for the same idempotency identity.

#### Scenario: Completion response is lost

- **WHEN** work completes but the worker restarts before acknowledging completion to an
  in-memory signal
- **THEN** recovery observes the durable completion and does not repeat the effect

### Requirement: Lease-safe single ownership

At most one active worker SHALL own an operation lease at a time, and ownership changes
SHALL use an atomic persisted comparison.

#### Scenario: Two workers claim the same operation

- **WHEN** two workers concurrently attempt to claim one queued operation
- **THEN** only one claim succeeds and only that owner may transition the running record

### Requirement: Non-sensitive operational visibility

Operation status SHALL be queryable by identifier and correlation identifier without
returning document contents, storage paths, credentials, or unnecessary candidate data.

#### Scenario: Operator inspects failed scan work

- **WHEN** an operator retrieves a failed scanning operation
- **THEN** the response identifies the failure code, timestamps, attempts, and correlation
  identifier without exposing the candidate document content or path
