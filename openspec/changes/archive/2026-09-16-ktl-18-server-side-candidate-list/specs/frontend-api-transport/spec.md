## MODIFIED Requirements

### Requirement: Feature service cache for synchronous consumers

A feature service backed by the API MAY expose a synchronous read of data it has already
loaded, so that existing consumers need not be rewritten around promises. Such a read SHALL
return only data the API has supplied — never seeded, fabricated, or browser-persisted data —
and the service SHALL expose its load state so that a consumer can distinguish "not loaded yet"
from "no such record".

Such a cache SHALL be scoped to the records a screen actually needs. A feature service SHALL NOT
load a whole table, and SHALL NOT pre-load the full aggregate of every record in a list in order to
serve a later detail view. A detail view SHALL load the one record it opens.

A cache SHALL NOT be the means by which a screen filters, sorts or pages. Those SHALL be requested
from the API, so that what the user sees reflects the whole matching set and not merely the subset
the browser happens to hold.

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

#### Scenario: List screen is opened

- **WHEN** a list screen loads
- **THEN** the service requests one page of the minimal list projection, and does not load the
  aggregate of every listed record

#### Scenario: Detail screen is opened from a list

- **WHEN** a user opens one record from a list
- **THEN** that record's aggregate is loaded on demand, and no other record's aggregate is loaded
  with it

#### Scenario: User sorts or filters a list

- **WHEN** a user changes a list's sort or filter
- **THEN** the service requests the corresponding page from the API, rather than reordering or
  filtering data it already holds
