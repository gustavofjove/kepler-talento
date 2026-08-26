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

#### Scenario: Slice ships without a consumer

- **WHEN** a change delivers backend endpoints for a feature whose frontend service still uses
  its legacy data path
- **THEN** the change is incomplete, because a delivered slice must demonstrate its own
  frontend integration

## ADDED Requirements

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
