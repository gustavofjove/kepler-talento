## Purpose

Defines the stable backend application boundary that later Kepler Talento business slices
use for consistent requests, errors, contracts, identity context, and architectural
separation.

## ADDED Requirements

### Requirement: Reference vertical slice

The platform SHALL expose one documented, read-only candidate reference operation that
travels through the production HTTP, application, and persistence boundaries without
reading browser storage or Supabase.

#### Scenario: Reference candidate is returned

- **WHEN** a permitted caller requests a seeded reference candidate
- **THEN** the API returns the candidate through the documented JSON contract with a
  successful HTTP status

#### Scenario: Reference candidate is absent

- **WHEN** a permitted caller requests a candidate identifier that does not exist
- **THEN** the API returns a not-found problem with a stable error code and correlation
  identifier

### Requirement: Consistent validation failures

The platform SHALL validate every registered request before its business operation and
SHALL return machine-readable validation errors with stable codes.

#### Scenario: Invalid request is rejected before processing

- **WHEN** a request violates one or more declared validation rules
- **THEN** the API returns a client-error problem containing each failed field/rule code
  and does not execute the operation

### Requirement: Consistent unhandled error contract

The platform SHALL map unhandled failures to a problem-details response without exposing
stack traces, credentials, database details, filesystem paths, or personal data.

#### Scenario: Unexpected backend failure

- **WHEN** an unexpected failure occurs while processing an API request
- **THEN** the caller receives a generic server-error problem with a correlation
  identifier and no internal implementation details

### Requirement: Correlation propagation

The platform SHALL associate each request with one correlation identifier that can be
used across the HTTP response, logs, audit records, and background work created by that
request.

#### Scenario: Caller supplies a valid correlation identifier

- **WHEN** a caller sends a valid correlation identifier
- **THEN** the API preserves it in the response and downstream operational records

#### Scenario: Caller omits a correlation identifier

- **WHEN** a caller sends no valid correlation identifier
- **THEN** the API creates one and returns it to the caller

### Requirement: Production identity fails closed

The platform SHALL accept a replaceable current-actor context for application operations,
but SHALL NOT allow a development or test actor to be enabled in a production
environment.

#### Scenario: Production starts with development actor configured

- **WHEN** the application starts in production with the development actor enabled
- **THEN** startup fails before the API accepts requests

#### Scenario: Protected operation has no actor

- **WHEN** an operation requiring a caller is invoked without a resolved actor
- **THEN** the operation is denied and no personal or operational data is returned

### Requirement: Discoverable API contract

The backend SHALL publish a machine-readable API contract for the reference operation,
health endpoints, error shapes, and correlation headers in development and test
environments.

#### Scenario: Contract is generated

- **WHEN** the API contract is generated during validation
- **THEN** it describes the implemented routes, response statuses, payloads, and problem
  shapes without exposing secrets

### Requirement: Enforced dependency direction

The backend build SHALL fail when a production project introduces a reference that
violates the approved inward dependency direction.

#### Scenario: Application references infrastructure

- **WHEN** application code adds a direct dependency on an infrastructure or web project
- **THEN** the architecture validation fails the build
