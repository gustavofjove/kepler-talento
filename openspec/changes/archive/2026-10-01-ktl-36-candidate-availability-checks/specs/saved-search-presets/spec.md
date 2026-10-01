## MODIFIED Requirements

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

- **WHEN** a preset write contains an unsupported availability value or checked-from date, mode, CV
  selection, or malformed criterion
- **THEN** it is rejected with a stable validation problem and no partial preset change is stored

## REMOVED Requirements

### Requirement: Shared criteria editor and summary

**Reason**: Its basic-filter row and status disclosure described the candidate status, which no
longer exists. It is replaced by "Shared criteria editor with availability", which keeps every other
rule of the editor and summary unchanged and edits availability and «Comprobado desde» instead.
**Migration**: None for users; the editor shows the availability disclosure where the status
disclosure was, with the same interaction rules.

## ADDED Requirements

### Requirement: Shared criteria editor with availability

The advanced search page, preset create and edit pages, and position create and edit pages SHALL
render the same criteria editor. The advanced search page and the preset criteria dialog SHALL
render the same read-only criteria summary. The criteria dialog SHALL be a single component that
other sections can host with their own details and actions. For identical filters the editor SHALL
present the same controls, options, accessible names, test identifiers and validation behaviour,
and the summary SHALL present the same content, in every screen. Only the actions surrounding the
editor MAY differ: searching and clearing on the search page, saving and cancelling on the preset
pages, or saving position requirements on the position pages. The shared components SHALL NOT
change the existing search behaviour, including that editing a filter does not re-run the search.

The editor SHALL present text, CV presence, availability and «Comprobado desde» together on one row
when space permits and SHALL wrap them without horizontal overflow as space narrows. CV presence
SHALL be edited with `Con CV` and `Sin CV` checkboxes, both selected by default. Both selected SHALL
mean unrestricted CV presence; only `Con CV` SHALL mean primary CV present; only `Sin CV` SHALL mean
no primary CV. The editor SHALL keep at least one CV choice selected.

Availability SHALL appear as a compact, initially closed disclosure whose closed label states
whether every availability value, one named value, or a count of several values is selected. When
opened, it SHALL expose a vertical popover anchored to the disclosure, over the content without
moving the fields below it. The popover SHALL accommodate different option counts and narrow
viewports without overlap or horizontal overflow; a long list SHALL scroll within it. Every
availability value (Sin comprobar, Disponible, No disponible) SHALL be selected by default. Each
option SHALL have a checkbox for multi-selection and a separately named, radio-shaped button that
selects only that value. The button SHALL retain button semantics. A partial selection SHALL offer
an action at the end to select all values. The editor SHALL keep at least one value selected.
Opening or closing the disclosure SHALL NOT change the filter values or run a search. Clicking
outside the disclosure and its options SHALL close the options without changing the selection.
Escape SHALL close the options and return focus to the disclosure control. These controls and
actions SHALL be operable by keyboard and have distinct Spanish accessible names.

«Comprobado desde» SHALL be a labelled, initially empty date input. Empty SHALL mean no restriction.
The criteria summary SHALL state a set date.

Each multi-value family (skills, languages, programs and tags) SHALL be edited with the catalog
value picker, with an optional level. A newly added criterion SHALL carry an empty level, meaning
any level, and its level SHALL be settable afterwards from its chip. Because a value already
present is not offered again, a criterion's level SHALL be changed on its chip rather than by
re-adding the value. Each family SHALL show its `ANY`/`ALL` mode control, next to the family label,
only while it holds two or more criteria, since the mode has no effect on fewer. The editor SHALL
show the criteria summary only while it is collapsed, because the chips already present every
criterion when it is expanded.

#### Scenario: Same editor in both screens

- **WHEN** the search page, preset edit page and position edit page are rendered with identical filters
- **THEN** they show the same criteria controls with the same accessible names and test identifiers

#### Scenario: Same summary in both screens

- **WHEN** the search page and the preset criteria dialog are rendered with identical filters
- **THEN** both show the same criteria summary content

#### Scenario: Search behaviour is preserved

- **WHEN** a user changes a filter on the search page without running the search
- **THEN** the results are not refreshed until the user runs or clears the search

#### Scenario: Default CV and availability choices

- **WHEN** the editor opens with unrestricted filters
- **THEN** both CV choices and every availability value are selected, the availability disclosure is
  closed and says that every value is selected, «Comprobado desde» is empty, and none of them
  restricts the query

#### Scenario: CV presence maps to the existing filter

- **WHEN** a user selects only `Con CV`, only `Sin CV`, or both
- **THEN** the editor respectively supplies `yes`, `no`, or an unset CV filter without changing
  primary-CV presence semantics

#### Scenario: CV cannot appear empty

- **WHEN** a user tries to unselect the last checked CV choice
- **THEN** one choice remains selected and the visible choices agree with the filter supplied

#### Scenario: Select one availability value and restore all

- **WHEN** a user activates an availability value's select-only button and then the select-all
  action
- **THEN** the checkboxes select only that value, the action selects every value, and the closed
  disclosure summarizes each resulting selection

#### Scenario: Availability cannot appear empty

- **WHEN** a user tries to unselect the last checked availability value
- **THEN** one value remains selected and the visible choices agree with the filter supplied

#### Scenario: Availability disclosure is presentational

- **WHEN** a user opens or closes the availability disclosure without changing a checkbox
- **THEN** the filter values stay the same and no search or save action runs

#### Scenario: Dismiss availability options

- **WHEN** a user clicks outside the open availability options or presses Escape while focused
  within them
- **THEN** the options close, the selected values remain unchanged, and Escape returns focus to the
  disclosure control

#### Scenario: Checked-from date is set

- **WHEN** a user enters a «Comprobado desde» date
- **THEN** the editor supplies that date as the checked-from filter and the collapsed summary states
  it, and clearing the input removes the restriction

#### Scenario: Variable option count and viewport

- **WHEN** the editor is viewed at desktop or narrow width
- **THEN** the availability popover stays within the viewport, scrolls if needed, and does not move
  the fields below it, while the basic filters share a row wherever space permits and wrap when they
  do not

#### Scenario: Keyboard access to availability actions

- **WHEN** a keyboard user opens the availability disclosure, selects only one value, changes a
  checkbox and restores all
- **THEN** each action is reachable, its purpose is announced by its accessible name, and the
  resulting selection is visible

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

### Requirement: Stored filters move to schema version 2

Stored filter documents, in saved presets and in position requirements, SHALL use filter schema
version 2. Version 2 SHALL carry the availability values and the checked-from date, and SHALL NOT
carry candidate statuses. The change that introduces version 2 SHALL rewrite every stored document
in place: it removes the status selection, adds an unrestricted availability family and sets the
version to 2. It SHALL NOT read or rewrite the encrypted free-text member. After the rewrite, a
stored document with any other version SHALL be refused and SHALL never be replaced by empty
filters. Release notes SHALL state that a stored status selection was dropped.

#### Scenario: Stored preset is read after the rewrite

- **WHEN** a preset saved before this change with a status selection is listed or applied
- **THEN** it is returned at version 2 with an unrestricted availability family and its other
  filters unchanged

#### Scenario: Version 1 document remains

- **WHEN** a stored filter document still carries version 1
- **THEN** reading or applying it fails with the stable invalid-filters problem and no empty search
  runs
