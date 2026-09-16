## MODIFIED Requirements

### Requirement: Shared API request behavior

All frontend service calls introduced for the new backend SHALL use one shared transport
behavior for API base URL resolution, JSON handling, timeouts, and correlation headers.

That shared transport SHALL attach the current bearer token to every API request, obtained from a
single session source rather than from each calling service. A request made while no session is
held SHALL be refused locally as unauthenticated rather than sent without credentials.

A response refusing a request as unauthenticated SHALL clear the local session and return the user
to sign-in, rather than surfacing as a generic failure that leaves a signed-out user looking at a
broken screen. The bearer token SHALL NOT be written to logs or to browser storage that outlives the
session.

#### Scenario: Reference service sends a request

- **WHEN** the reference candidate service calls the backend
- **THEN** the request uses the configured API base URL and shared transport behavior

#### Scenario: Authenticated request carries the token

- **WHEN** any feature service calls the API while a session is held
- **THEN** the request carries the current bearer token, attached by the shared transport

#### Scenario: Request is made with no session

- **WHEN** a feature service calls the API while no session is held
- **THEN** the call fails as unauthenticated without sending a credential-less request to the API

#### Scenario: API refuses the request as unauthenticated

- **WHEN** the API answers a request with the unauthenticated status
- **THEN** the local session is cleared and the user is returned to sign-in

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

#### Scenario: Candidate slice is deployed

- **WHEN** the candidate backend slice is enabled
- **THEN** the candidate feature service and the candidate relation and document services reach
  the API, no candidate data is read from or written to browser storage, and search, admin,
  import, and export services retain their existing behavior

#### Scenario: Identity slice is deployed

- **WHEN** the identity and access-control slice is enabled
- **THEN** the authentication, user and role services reach the API, no profile, user or role data
  is read from or written to browser storage, and the import service retains its existing behavior
  until its own migration change

#### Scenario: Slice ships without a consumer

- **WHEN** a change delivers backend endpoints for a feature whose frontend service still uses
  its legacy data path
- **THEN** the change is incomplete, because a delivered slice must demonstrate its own
  frontend integration

### Requirement: Eviction of superseded browser storage

When a feature's data moves from browser storage to the API, the delivering change SHALL
actively remove the superseded browser storage entry on application startup, not merely stop
reading and writing it. Removal SHALL be attempted regardless of whether the API is reachable,
and SHALL NOT prevent the application from starting if storage is unavailable.

An entry that carried a user's identity, role or permission set SHALL be treated the same way as one
that carried candidate data: leaving it behind would let a stale local copy decide what the
application renders after the authority for that decision has moved to the API.

#### Scenario: Browser holds a superseded entry

- **WHEN** the new build starts in a browser that still holds the superseded storage entry
- **THEN** the entry is removed from that browser without requiring a user action

#### Scenario: Entry holds personal data

- **WHEN** the superseded entry held candidate personal data
- **THEN** after first run of the new build no candidate personal data remains in browser
  storage, and none is written back by any later interaction

#### Scenario: Entry holds identity or authorization data

- **WHEN** the superseded entry held a signed-in profile, a user list or a role and permission
  definition
- **THEN** after first run of the new build none of it remains in browser storage, and the
  application's rendering decisions come from the caller profile endpoint instead

#### Scenario: Backend is unreachable at startup

- **WHEN** the new build starts and the API cannot be reached
- **THEN** the superseded entry is still removed, and the application reports the API failure
  rather than falling back to the removed data

#### Scenario: Storage access fails

- **WHEN** browser storage cannot be read or written at startup
- **THEN** the application starts and continues to operate against the API
