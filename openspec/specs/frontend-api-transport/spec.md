# Frontend API Transport Specification

## Purpose

Defines one consistent frontend boundary for API requests, problem responses, correlation,
cancellation, and controlled downloads while preserving Kepler Talento's established
React service and test-substitution conventions.

## Requirements

### Requirement: Shared API request behavior

All frontend service calls introduced for the new backend SHALL use one shared transport
behavior for API base URL resolution, JSON handling, timeouts, and correlation headers.

#### Scenario: Reference service sends a request

- **WHEN** the reference candidate service calls the backend
- **THEN** the request uses the configured API base URL and shared transport behavior

### Requirement: Problem-details translation

The frontend transport SHALL translate documented API problem responses into the
application's stable error model while preserving the backend error code, user-facing
Spanish message, and correlation identifier.

#### Scenario: Reference request returns validation problem

- **WHEN** the backend returns a documented validation problem
- **THEN** the feature service receives the stable error code, Spanish message, field
  errors, and correlation identifier without parsing ad hoc response shapes

#### Scenario: Response is not a documented problem

- **WHEN** an unsuccessful response is malformed or has an unexpected content type
- **THEN** the transport returns a safe generic error with the available correlation
  identifier and does not expose raw server content to the UI

### Requirement: Request cancellation and timeout

Frontend API calls SHALL support caller cancellation and a documented finite timeout
without automatically retrying unsafe operations.

#### Scenario: Component abandons a request

- **WHEN** the caller cancels an in-flight reference request
- **THEN** the request is aborted and no stale successful/error state is applied to the
  abandoned consumer

#### Scenario: Request exceeds timeout

- **WHEN** the backend does not respond within the configured timeout
- **THEN** the caller receives a stable timeout error and a non-idempotent request is not
  retried automatically

### Requirement: Controlled download filename

The frontend transport SHALL download files only from the API and SHALL derive the user
filename from a valid safe `Content-Disposition` value or a safe application fallback.

#### Scenario: API returns a valid filename

- **WHEN** a permitted download response includes a valid safe filename
- **THEN** the browser download uses that filename without exposing the backend URL or
  storage key

#### Scenario: API returns an unsafe filename

- **WHEN** the response filename contains path separators, control characters, or other
  unsafe content
- **THEN** the transport replaces it with a safe application-generated filename

### Requirement: Service boundary preservation

Components SHALL consume backend capabilities through test-substitutable feature
services rather than issuing direct network calls.

#### Scenario: Reference UI integration is tested

- **WHEN** the reference React integration is rendered under the service provider used by
  tests
- **THEN** a test double can replace the API-backed feature service without real network
  access

### Requirement: Incremental persistence cutover

Each delivered backend slice SHALL cut its own frontend feature service over to the API within
the same change, rather than deferring frontend integration to a single terminal cutover.
Feature data paths whose backend slice has not yet been delivered SHALL retain their existing
behavior until their own migration change.

#### Scenario: Foundation is deployed in development

- **WHEN** the KTL-5 frontend changes are enabled
- **THEN** the reference integration uses the API while unrelated candidate, catalog, search,
  admin, import, and export services retain their existing behavior

#### Scenario: Catalog slice is deployed

- **WHEN** the catalog backend slice is enabled
- **THEN** the catalog feature service reaches the API, no catalog data is read from or written
  to browser storage, and candidate, search, admin, import, and export services retain their
  existing behavior

#### Scenario: Slice ships without a consumer

- **WHEN** a change delivers backend endpoints for a feature whose frontend service still uses
  its legacy data path
- **THEN** the change is incomplete, because a delivered slice must demonstrate its own
  frontend integration

### Requirement: Asynchronous feature data loading

A frontend feature service backed by the API SHALL expose the loading, loaded, and failed
states of its data to consuming components, so that a component never renders a partially
loaded collection as though it were complete.

#### Scenario: Data is loading

- **WHEN** a component renders while its feature service is still loading API-backed data
- **THEN** the component communicates the loading state and does not present an empty
  collection as a complete result

#### Scenario: Data fails to load

- **WHEN** the API-backed load fails
- **THEN** the component presents the failure with the Spanish message from the application
  error model and offers no stale or fabricated data

#### Scenario: Data is reloaded after a change

- **WHEN** a component submits a change through an API-backed feature service and the change
  succeeds
- **THEN** the data the component presents afterwards reflects the stored server state

### Requirement: Same-origin browser evidence

The frontend and API foundation SHALL be validated through a browser flow using the
configured same-origin `/api` route.

#### Scenario: Browser exercises reference integration

- **WHEN** the targeted end-to-end smoke flow runs through Nginx
- **THEN** the SPA reaches the reference API and renders/observes the expected result
  without permissive CORS configuration
