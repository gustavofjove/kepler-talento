## Purpose

Defines the reproducible intranet development runtime, dependency health, operational
hardening, persistence, logging, backup, restore, and portability expected from the new
platform foundation.

## ADDED Requirements

### Requirement: Reproducible development stack

Developers SHALL be able to start one documented Docker stack containing the frontend,
reverse proxy, API, PostgreSQL, and malware scanner with no cloud service dependency.

#### Scenario: Clean development start

- **WHEN** a developer provides the documented local configuration and starts the stack
- **THEN** all required services become healthy and the SPA can reach the API through the
  reverse proxy

### Requirement: Same-origin routing

The development reverse proxy SHALL serve the SPA at `/` and route backend requests under
`/api` on the same origin.

#### Scenario: Browser calls reference API

- **WHEN** the SPA requests the reference operation under `/api`
- **THEN** the proxy routes it to the backend without requiring permissive cross-origin
  access

### Requirement: Persistent service data

PostgreSQL data, available documents, quarantined documents, and scanner signatures SHALL
survive replacement of their application containers.

#### Scenario: Containers are recreated

- **WHEN** disposable containers are removed and recreated while persistent volumes are
  retained
- **THEN** database records, stored documents, quarantine state, and scanner signatures
  remain available

### Requirement: Private scanner network

The scanner service SHALL be reachable by the backend on the private application network
and SHALL NOT publish its unauthenticated scanning protocol outside that network.

#### Scenario: Host network inspects published ports

- **WHEN** the development stack is running
- **THEN** no scanner daemon port is exposed on the host or intranet interface

### Requirement: Dependency-aware health

The runtime SHALL expose liveness and readiness states that distinguish process health
from the availability of PostgreSQL, writable storage, and malware scanning.

#### Scenario: PostgreSQL is unavailable

- **WHEN** the API process is alive but PostgreSQL cannot be reached
- **THEN** liveness remains observable while readiness reports the database dependency as
  unavailable

#### Scenario: Scanner is unavailable

- **WHEN** the scanner cannot accept new work
- **THEN** readiness/dependency health reports scanning as unavailable, new uploads fail
  closed, and previously clean downloads remain eligible

### Requirement: Fail-fast unsafe configuration

The runtime SHALL refuse production startup when required database, storage, environment,
or development-actor safeguards are missing or unsafe.

#### Scenario: Production storage root is missing

- **WHEN** production starts without a valid private writable storage root
- **THEN** startup fails before requests are accepted

### Requirement: Redacted correlated logging

Runtime logs SHALL be structured and correlated while excluding credentials, document
contents, storage paths, and unnecessary candidate personal data.

#### Scenario: Document scan fails

- **WHEN** scanning fails for a quarantined document
- **THEN** logs contain the operation/document identifiers, failure code, and correlation
  identifier but not the filename when sensitive, content, or storage path

### Requirement: Response hardening

The reverse proxy and API SHALL apply configured request/upload limits, trusted forwarded
headers, safe download exposure, and security headers to intranet responses.

#### Scenario: API response traverses proxy

- **WHEN** the proxy returns an API response
- **THEN** the response includes the required content-type, framing, referrer, and content
  security controls without trusting forwarded identity from an unconfigured proxy

### Requirement: Coordinated backup baseline

The operational baseline SHALL back up PostgreSQL and candidate-document storage daily as
one recovery set, retain recoverable backups for 30 days, target no more than 24 hours of
data loss, and target restoration within 4 hours.

#### Scenario: Daily backup completes

- **WHEN** the scheduled backup runs
- **THEN** it produces a timestamped database/document recovery set and records its
  validation result without copying credentials into the report

### Requirement: Restore reconciliation

A documented restore validation SHALL prove database availability and reconcile document
metadata, relative keys, and cryptographic hashes.

#### Scenario: Recovery set is restored

- **WHEN** an operator restores a selected recovery set into a clean validation
  environment
- **THEN** schema checks pass and every sampled/restored document record resolves to the
  expected binary and hash or is reported as a recovery error

### Requirement: Production portability

The platform SHALL keep final production host and reverse-proxy decisions configurable
and SHALL NOT require Docker-specific paths inside application contracts.

#### Scenario: Production topology remains undecided

- **WHEN** KTL-5 completes before production infrastructure is selected
- **THEN** the documented development topology works and production-specific choices
  remain isolated to configuration/deployment adapters
