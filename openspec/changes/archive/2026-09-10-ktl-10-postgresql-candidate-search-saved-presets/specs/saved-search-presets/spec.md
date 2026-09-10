## Purpose

Define private, owner-scoped saved searches stored by the API while retaining only last-used filters as a local browser preference.

## ADDED Requirements

### Requirement: Owner-scoped preset listing and retrieval

An authenticated actor with `view_candidates` SHALL list and retrieve only that actor's saved
search presets. Each preset SHALL contain an opaque identifier, trimmed name, complete normalized
search filters, creation time, update time, and optional last-used time. Presets SHALL NOT be
shared between owners.

#### Scenario: Owner lists presets

- **WHEN** a permitted actor lists saved searches
- **THEN** only presets owned by that actor are returned, ordered by name without regard to case

#### Scenario: Actor names another owner's preset

- **WHEN** a permitted actor requests a preset owned by someone else
- **THEN** the request is indistinguishable from a missing preset and discloses neither its
  filters nor its existence

#### Scenario: Caller cannot view candidates

- **WHEN** an unauthenticated actor or an actor without `view_candidates` accesses presets
- **THEN** the request fails closed and returns no preset data

### Requirement: Preset create and update

A permitted actor SHALL create a preset and SHALL update its name or complete filter value. Names
SHALL be non-blank after trimming and unique per owner under case-insensitive comparison. Creating
or renaming to a conflicting name SHALL be rejected without changing either preset. An update
SHALL preserve the identifier, owner and creation time and SHALL advance the update time.

#### Scenario: Preset is created

- **WHEN** a permitted actor supplies a unique non-blank name and valid filters
- **THEN** the system stores and returns the normalized preset with server-assigned identity and
  timestamps

#### Scenario: Name differs only by case

- **WHEN** an owner creates or renames a preset to a name already owned under case-insensitive
  comparison
- **THEN** the operation is rejected with a stable conflict problem and neither preset changes

#### Scenario: Preset filters are updated

- **WHEN** the owner replaces a preset's filters with a valid complete filter value
- **THEN** subsequent reads return the new normalized filters and an advanced update time

#### Scenario: Invalid filter is stored

- **WHEN** a preset write contains an unsupported status, mode, CV selection, or malformed
  criterion
- **THEN** it is rejected with a stable validation problem and no partial preset change is stored

### Requirement: Preset deletion and last-used tracking

The owner SHALL delete a preset and SHALL mark it used when applying it. Applying a preset SHALL
return its normalized filters and advance `lastUsedAt` and `updatedAt`. Deletion SHALL remove only
the named owner's preset and SHALL NOT alter candidate data or another preset.

#### Scenario: Owner applies a preset

- **WHEN** the owner applies an existing preset
- **THEN** its normalized filters are returned and its last-used and update times advance

#### Scenario: Owner deletes a preset

- **WHEN** the owner deletes an existing preset
- **THEN** it disappears from later lists and no unrelated data changes

#### Scenario: Missing preset is changed

- **WHEN** an owner updates, applies, or deletes a preset that is missing or belongs to another
  owner
- **THEN** the operation returns the same stable not-found response and stores no change

### Requirement: Legacy local presets are explicitly unsupported

The API-backed preset service SHALL NOT read, upload, or retain the former
`rrhh.search.presets.v1` browser data. This includes the current local shape and older shapes with
plain `languageValues` or `programValues` arrays. Release and user documentation SHALL state that
these presets must be recreated. The legacy key SHALL be absent from frontend source and SHALL
never again be written by the application.

#### Scenario: Browser still contains legacy presets

- **WHEN** the API-backed search experience starts in a browser that retains old local preset
  data
- **THEN** the service ignores that data, shows only the current owner's API presets, and does not
  upload or modify the old value

#### Scenario: Legacy shape exists

- **WHEN** retained browser data uses `languageValues` or `programValues`
- **THEN** it remains explicitly unsupported and cannot affect API state or current search

#### Scenario: Source is inspected after cutover

- **WHEN** the delivered frontend source is checked for the former preset storage key
- **THEN** the key and all code that reads or writes it are absent

### Requirement: Last filters remain local

The most recently used filter set SHALL continue to use `rrhh.search.last-filters.v1` as a
per-browser convenience. It SHALL be normalized on read, SHALL NOT be uploaded as a preset, and
SHALL NOT be treated as shared or authoritative state.

#### Scenario: User returns in the same browser

- **WHEN** valid last-used filters exist locally
- **THEN** the search form restores their normalized values without an API preset write

#### Scenario: Local last filters are malformed

- **WHEN** the stored last-filter value cannot be parsed or normalized
- **THEN** the form uses empty default filters without changing server state

#### Scenario: User opens another browser

- **WHEN** the same actor opens search in a browser without local last filters
- **THEN** no last filters are restored even though the actor's API presets remain available

### Requirement: Preset privacy and safe diagnostics

Preset filters, names, and owner identity SHALL be stored only through the application API under
least-privilege database access. Logs and problem responses SHALL NOT contain preset filter
payloads or search terms, and SHALL NOT expose database details or another owner's identifiers.

#### Scenario: Preset operation is logged

- **WHEN** a preset operation succeeds or fails
- **THEN** diagnostics contain safe operational identifiers and correlation data but no preset
  filters, search terms, or personal candidate data

#### Scenario: Runtime database role is used

- **WHEN** the API reads or changes presets
- **THEN** its database identity can perform only the documented runtime operations and browser
  callers have no direct database access
