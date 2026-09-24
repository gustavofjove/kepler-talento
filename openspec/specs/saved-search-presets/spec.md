# Saved Search Presets Specification

## Purpose

Define a single, administrator-curated library of saved searches stored by the API, shared by
everyone who can search candidates and managed under a dedicated permission, while retaining only
last-used filters as a local browser preference.

## Requirements

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

### Requirement: Existing private presets are discarded

Moving to the shared library SHALL remove every preset stored under the former owner-scoped model
rather than promoting it into the library. No former owner identifier SHALL remain in storage.
Release documentation SHALL state that these presets must be recreated by an administrator.

#### Scenario: Database holds owner-scoped presets

- **WHEN** the shared-library migration is applied to a database containing presets from several
  owners
- **THEN** the preset library is empty afterwards and no owner identifier is stored

#### Scenario: Migration is applied again

- **WHEN** the migration step is repeated on an already migrated database
- **THEN** it makes no further change

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

### Requirement: Shared criteria editor and summary

The advanced search page and the preset create and edit pages SHALL render the same criteria
editor, and the advanced search page and the preset criteria dialog SHALL render the same read-only
criteria summary. The criteria dialog SHALL be a single component that other sections can host with
their own details and actions. For identical filters the editor SHALL present the same controls,
options, accessible names, test identifiers and validation behaviour, and the summary SHALL present
the same content, in every screen. Only the actions surrounding the editor MAY differ: searching and
clearing on the search page, saving and cancelling on the preset pages. The shared components SHALL
NOT change the existing search behaviour, including that editing a filter does not re-run the search.

Each multi-value family (skills, languages, programs and tags) SHALL be edited with the catalog value
picker, with an optional level. A newly added criterion SHALL carry an empty level, meaning any level,
and its level SHALL be settable afterwards from its chip. Because a value already present is not
offered again, a criterion's level SHALL be changed on its chip rather than by re-adding the value.
Each family SHALL show its `ANY`/`ALL` mode control, next to the family label, only while it holds
two or more criteria, since the mode has no effect on fewer. The editor SHALL show the criteria summary only while it is
collapsed, because the chips already present every criterion when it is expanded.

#### Scenario: Same editor in both screens

- **WHEN** the search page and the preset edit page are rendered with identical filters
- **THEN** both show the same criteria pickers with the same accessible names and test identifiers

#### Scenario: Same summary in both screens

- **WHEN** the search page and the preset criteria dialog are rendered with identical filters
- **THEN** both show the same criteria summary content

#### Scenario: Search behaviour is preserved

- **WHEN** a user changes a filter on the search page without running the search
- **THEN** the results are not refreshed until the user runs or clears the search

#### Scenario: Criterion is added without a level

- **WHEN** a user adds a skill in the criteria editor
- **THEN** the skill criterion carries an empty level and its chip reads `Cualquier nivel`

#### Scenario: Criterion level is set from its chip

- **WHEN** a user opens a language criterion's chip and chooses a level
- **THEN** the criterion carries that level and no second criterion for the same value is created

#### Scenario: Mode control appears from two criteria

- **WHEN** a family holds fewer than two criteria
- **THEN** its mode control is not shown, and it appears once a second criterion is added

#### Scenario: Summary follows the collapsed state

- **WHEN** the criteria editor is collapsed on the search page
- **THEN** the criteria summary is shown, and it is hidden again when the editor is expanded

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

Preset filters and names SHALL be stored only through the application API under least-privilege
database access. Responses SHALL NOT contain the identity of actors who created, changed or used a
preset. Logs and problem responses SHALL NOT contain preset filter payloads, preset names or search
terms, and SHALL NOT expose database details.

#### Scenario: Preset operation is logged

- **WHEN** a preset operation succeeds or fails
- **THEN** diagnostics contain safe operational identifiers and correlation data but no preset
  names, filters, search terms, or personal candidate data

#### Scenario: Runtime database role is used

- **WHEN** the API reads or changes presets
- **THEN** its database identity can perform only the documented runtime operations and browser
  callers have no direct database access

### Requirement: Presets predating a filter family remain usable

A preset stored before a filter family existed SHALL remain readable, listable and applicable.
The absent family SHALL be presented as empty with its documented default mode, and SHALL place
no restriction when the preset is applied. Reading such a preset SHALL NOT be rejected as an
invalid filter value and SHALL NOT fail the page that lists or applies it. Saving that preset
again SHALL store the complete filter value, including the family that was previously absent.

#### Scenario: Preset stored before tags existed is applied

- **WHEN** an actor applies a preset whose stored filters contain no tag family
- **THEN** the search runs with an empty tag family and an `ANY` mode, and the tag family places
  no restriction on the result

#### Scenario: Preset library is listed after a family is added

- **WHEN** the preset library is listed and some presets predate the tag family
- **THEN** every preset is listed and none is reported as invalid

#### Scenario: Older preset is edited and saved

- **WHEN** an actor opens a preset that predates the tag family, changes it and saves it with its
  current version
- **THEN** the stored filter value carries the complete family set, including the tag family

### Requirement: Tag criteria are edited and summarized like other criteria

The shared criteria editor SHALL present the tag family with the same picker, accessible naming, test
identifier scheme and validation behavior as the other multi-value families, except that tag chips
SHALL show no level and SHALL open no level editor, because a tag has no level. The shared criteria
summary SHALL present the selected tags and their mode on every screen that renders it.

#### Scenario: Tag criteria are edited

- **WHEN** the criteria editor is rendered on the search page and on the preset edit page with
  identical filters containing tags
- **THEN** both show the same tag picker, with the same accessible names and test identifiers, and
  neither offers a level for a tag

#### Scenario: Tag criteria are summarized

- **WHEN** the criteria summary is rendered for filters containing tags
- **THEN** it shows the selected tags and the tag mode, with the same content on every screen
  that renders it

### Requirement: Position editor consumes the shared preset library

The position editor SHALL use the existing shared preset library without creating a position-specific preset store. An actor holding `candidates.read` SHALL be able to list and apply a preset in that editor; applying SHALL record use under the existing rules and copy the normalized filter value into the position draft. No preset identifier, ownership value or synchronization relationship SHALL be persisted on the position.

An actor also holding `presets.manage` SHALL be offered a save-as-preset action that creates a new shared preset from the current draft under the existing name, validation, privacy and conflict rules. The action SHALL be absent without `presets.manage`. Position editing SHALL NOT add rename, update or delete controls for existing presets.

#### Scenario: Preset is applied in a position editor

- **WHEN** an actor holding `candidates.read` applies a shared preset
- **THEN** its normalized filters replace the draft requirements, its last-used time advances and no preset link is stored

#### Scenario: Applied preset changes later

- **WHEN** a preset is changed after its filters were copied into a position draft or saved position
- **THEN** the copied requirements do not change

#### Scenario: Position editor saves a new preset

- **WHEN** an actor holding `presets.manage` supplies a valid unique name for the current requirements
- **THEN** a new preset is created in the shared library and the position retains an independent copy

#### Scenario: Actor cannot manage presets

- **WHEN** an actor without `presets.manage` edits a position
- **THEN** no save-as-preset action is rendered and a direct preset create request remains forbidden

#### Scenario: Actor cannot read candidates

- **WHEN** an actor without `candidates.read` edits a position
- **THEN** the actor cannot list or apply presets and receives no preset data

#### Scenario: Applied preset was deleted

- **WHEN** a selected preset no longer exists when the actor applies it
- **THEN** the editor shows a Spanish error and leaves the current draft requirements unchanged
