## MODIFIED Requirements

### Requirement: Shared preset listing and retrieval

Saved search presets SHALL form a single library shared by every permitted actor. An authenticated
actor with `candidates.read` SHALL list all presets and SHALL retrieve any single preset by its
identifier. Each preset SHALL contain an opaque identifier, trimmed name, complete normalized search
filters, creation time, update time, optional last-used time, and a concurrency version. Responses
SHALL NOT contain the identity of any actor who created, changed or used a preset.

#### Scenario: Presets are listed

- **WHEN** a permitted actor lists saved searches
- **THEN** every preset in the library is returned, ordered by name without regard to case

#### Scenario: Preset created by another actor is visible

- **WHEN** one actor creates a preset and a different permitted actor lists or retrieves presets
- **THEN** the second actor receives that preset with the same name and filters

#### Scenario: Single preset is retrieved

- **WHEN** a permitted actor requests an existing preset by identifier
- **THEN** that preset is returned with its version and no actor identity

#### Scenario: Missing preset is retrieved

- **WHEN** a permitted actor requests an identifier that does not exist
- **THEN** a stable not-found problem is returned and no preset data is disclosed

#### Scenario: Caller cannot view candidates

- **WHEN** an unauthenticated actor or an actor without `candidates.read` lists, retrieves or
  applies presets
- **THEN** the request fails closed before validation and returns no preset data

### Requirement: Preset management permission

Creating, updating and deleting presets SHALL require an authenticated actor holding
`presets.manage`. The API SHALL check authentication and this permission before validating the
request or revealing whether the named preset exists. Holding `candidates.read` alone SHALL NOT
permit any preset write, and holding `presets.manage` alone SHALL NOT permit listing, retrieving or
applying presets.

#### Scenario: Reader attempts a write

- **WHEN** an actor with `candidates.read` but without `presets.manage` creates, updates or deletes
  a preset
- **THEN** the request is refused as forbidden and no preset changes

#### Scenario: Unauthenticated write with an invalid body

- **WHEN** an unauthenticated caller sends a malformed preset write
- **THEN** the refusal is the same authorization problem as for a valid body, and no validation
  detail is returned

#### Scenario: Manager without read permission

- **WHEN** an actor holding `presets.manage` but not `candidates.read` lists or applies presets
- **THEN** the request is refused as forbidden

#### Scenario: Default administrative roles

- **WHEN** the seeded role definitions are inspected
- **THEN** `rrhh_admin` and `system_admin` include `presets.manage` and no other seeded role does

### Requirement: Preset create and update

An actor holding `presets.manage` SHALL create a preset and SHALL update its name or complete filter
value. Names SHALL be non-blank after trimming, at most 120 characters, and unique across the whole
library under comparison that ignores case and accents. Creating or renaming to a conflicting name
SHALL be rejected without changing either preset. An update SHALL supply the version the actor last
read; a stale version SHALL be rejected with a stable conflict problem that is distinguishable from
a name conflict, and nothing SHALL be stored. A successful update SHALL preserve the identifier and
creation time, SHALL advance the update time and SHALL return a new version.

#### Scenario: Preset is created

- **WHEN** a permitted actor supplies a unique non-blank name and valid filters
- **THEN** the system stores and returns the normalized preset with server-assigned identity,
  timestamps and version

#### Scenario: Name differs only by case

- **WHEN** an actor creates or renames a preset to a name that already exists under comparison
  ignoring case and accents, such as `ingles b2` when `Inglés B2` exists
- **THEN** the operation is rejected with a stable name-conflict problem and neither preset changes

#### Scenario: Preset filters are updated

- **WHEN** a permitted actor replaces a preset's filters with a valid complete filter value and its
  current version
- **THEN** subsequent reads return the new normalized filters, an advanced update time and a new
  version

#### Scenario: Stale version is written

- **WHEN** two actors load the same preset and the second saves after the first
- **THEN** the second write is rejected with a stable concurrency-conflict problem and the first
  actor's change remains

#### Scenario: Invalid filter is stored

- **WHEN** a preset write contains an unsupported status, mode, CV selection, or malformed
  criterion
- **THEN** it is rejected with a stable validation problem and no partial preset change is stored

### Requirement: Preset deletion and last-used tracking

An actor holding `presets.manage` SHALL delete a preset by supplying the version it last read.
Deletion SHALL physically remove only the named preset and SHALL NOT alter candidate data or any
other preset; a stale version SHALL be rejected with the concurrency-conflict problem. An actor
holding `candidates.read` SHALL apply a preset; applying SHALL return its normalized filters and
advance only `lastUsedAt`, leaving the update time and version unchanged, so that applying never
invalidates an edit in progress. A stored filter value that can no longer be understood SHALL be
refused without recording a use.

#### Scenario: Owner applies a preset

- **WHEN** a permitted actor applies an existing preset
- **THEN** its normalized filters are returned, its last-used time advances, and its update time and
  version are unchanged

#### Scenario: Apply does not break a pending edit

- **WHEN** an administrator loads a preset, a recruiter then applies it, and the administrator saves
  with the version originally loaded
- **THEN** the administrator's save succeeds

#### Scenario: Owner deletes a preset

- **WHEN** a permitted actor deletes an existing preset with its current version
- **THEN** it disappears from later lists and retrievals and no unrelated data changes

#### Scenario: Missing preset is changed

- **WHEN** an actor updates, applies, or deletes a preset that does not exist
- **THEN** the operation returns the same stable not-found response and stores no change

#### Scenario: Unreadable stored filters are applied

- **WHEN** an actor applies a preset whose stored filter value is no longer understood
- **THEN** the request is refused with a stable problem and no last-used time is recorded

### Requirement: Preset administration section

The application SHALL provide an administration section for presets, reachable only by actors
holding `presets.manage`. It SHALL offer a list of all presets and a create page and an edit page,
each at its own route; there SHALL be no separate read-only page. The list SHALL show each preset's
name, its update time and its last-used time (shown as never used when absent), and SHALL support
filtering by name, sorting and pagination. Beside each name the list SHALL offer a view control,
with an accessible name identifying the preset, that opens a modal dialog showing the preset's
read-only criteria summary, its creation, update and last-used times, and a way to edit it. The
dialog SHALL close with Escape, a click outside it, or its close control, SHALL keep keyboard focus
inside while open, and SHALL return focus to the control that opened it. Deleting from the list
SHALL require explicit confirmation. Saving or cancelling the create and edit pages SHALL return to
the list. The create and edit pages SHALL tell the administrator that presets are visible to
everyone with search access and must not contain personal data. All copy SHALL be Spanish.

#### Scenario: Administrator opens the section

- **WHEN** an actor with `presets.manage` opens the presets section
- **THEN** every preset is listed with its name, update time and last-used time, and no criteria
  are rendered in the rows

#### Scenario: Criteria are viewed in a dialog

- **WHEN** the administrator activates the view control beside a preset's name
- **THEN** a modal dialog titled with the preset's name shows its criteria summary, its timestamps
  and an edit link
- **AND** pressing Escape closes it and returns focus to the view control

#### Scenario: Route reached without permission

- **WHEN** an actor without `presets.manage` navigates directly to any presets administration route
- **THEN** the route guard redirects the actor away and no preset page is rendered

#### Scenario: Preset is created from the section

- **WHEN** the administrator enters a unique name and criteria on the create page and saves
- **THEN** the preset is stored and the list is shown again with the new preset in it

#### Scenario: Deletion is cancelled

- **WHEN** the administrator starts deleting a preset and dismisses the confirmation
- **THEN** no request is sent and the preset remains listed

#### Scenario: Save conflicts are explained

- **WHEN** a save is refused because the name is taken or the preset changed since it was loaded
- **THEN** the page shows a distinct Spanish message for each case and keeps the entered values

#### Scenario: Narrow viewport

- **WHEN** the list is shown at 390 pixels wide
- **THEN** the table scrolls within its own container and the page does not scroll horizontally

### Requirement: Search page applies presets only

The advanced search page SHALL let any actor with `candidates.read` choose a preset from the shared
library and apply it, replacing the current filters, running the search and recording the use. It
SHALL NOT offer creating, renaming, updating or deleting presets to any actor. It SHALL show a link
to the presets administration section only to actors holding `presets.manage`.

#### Scenario: Recruiter applies a preset

- **WHEN** a recruiter selects a preset on the search page
- **THEN** the form shows that preset's filters and results for those filters are displayed

#### Scenario: No management controls on the search page

- **WHEN** any actor opens the search page
- **THEN** no control to save, rename or delete a preset is present

#### Scenario: Link to administration

- **WHEN** an actor holding `presets.manage` opens the search page
- **THEN** a link to the presets administration section is shown, and it is absent for actors
  without that permission

#### Scenario: Selected preset was deleted meanwhile

- **WHEN** a recruiter selects a preset that an administrator has since deleted
- **THEN** a Spanish error is shown, the current filters are unchanged and the picker selection is
  cleared
