## MODIFIED Requirements

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

#### Scenario: Slice ships without a consumer

- **WHEN** a change delivers backend endpoints for a feature whose frontend service still uses
  its legacy data path
- **THEN** the change is incomplete, because a delivered slice must demonstrate its own
  frontend integration

## ADDED Requirements

### Requirement: Eviction of superseded browser storage

When a feature's data moves from browser storage to the API, the delivering change SHALL
actively remove the superseded browser storage entry on application startup, not merely stop
reading and writing it. Removal SHALL be attempted regardless of whether the API is reachable,
and SHALL NOT prevent the application from starting if storage is unavailable.

#### Scenario: Browser holds a superseded entry

- **WHEN** the new build starts in a browser that still holds the superseded storage entry
- **THEN** the entry is removed from that browser without requiring a user action

#### Scenario: Entry holds personal data

- **WHEN** the superseded entry held candidate personal data
- **THEN** after first run of the new build no candidate personal data remains in browser
  storage, and none is written back by any later interaction

#### Scenario: Backend is unreachable at startup

- **WHEN** the new build starts and the API cannot be reached
- **THEN** the superseded entry is still removed, and the application reports the API failure
  rather than falling back to the removed data

#### Scenario: Storage access fails

- **WHEN** browser storage cannot be read or written at startup
- **THEN** the application starts and continues to operate against the API

### Requirement: Feature service cache for synchronous consumers

A feature service backed by the API MAY expose a synchronous read of data it has already
loaded, so that existing consumers need not be rewritten around promises. Such a read SHALL
return only data the API has supplied — never seeded, fabricated, or browser-persisted data —
and the service SHALL expose its load state so that a consumer can distinguish "not loaded yet"
from "no such record".

#### Scenario: Consumer reads a loaded record synchronously

- **WHEN** a consumer synchronously reads a record the service has already loaded from the API
- **THEN** it receives the loaded record without issuing a new request

#### Scenario: Consumer reads a record that is not loaded

- **WHEN** a consumer synchronously reads a record the service has not loaded
- **THEN** it receives no record, and the exposed load state distinguishes an incomplete load
  from a confirmed absence

#### Scenario: Cached record is changed

- **WHEN** a change to a record succeeds through the service
- **THEN** subsequent synchronous reads reflect the stored server state rather than a locally
  applied guess

#### Scenario: Cache survives no longer than the session

- **WHEN** the application is reloaded
- **THEN** the service holds no records until it loads them from the API again
