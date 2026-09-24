## MODIFIED Requirements

### Requirement: Shared criteria editor and summary

The advanced search page, preset create and edit pages, and position create and edit pages SHALL
render the same criteria editor. The advanced search page and the preset criteria dialog SHALL
render the same read-only criteria summary. The criteria dialog SHALL be a single component that
other sections can host with their own details and actions. For identical filters the editor SHALL
present the same controls, options, accessible names, test identifiers and validation behaviour,
and the summary SHALL present the same content, in every screen. Only the actions surrounding the
editor MAY differ: searching and clearing on the search page, saving and cancelling on the preset
pages, or saving position requirements on the position pages. The shared components SHALL NOT
change the existing search behaviour, including that editing a filter does not re-run the search.

The editor SHALL present text, CV presence and candidate status together on one row when space
permits and SHALL wrap them without horizontal overflow as space narrows. CV presence SHALL be
edited with `Con CV` and `Sin CV` checkboxes, both selected by default. Both selected SHALL mean
unrestricted CV presence; only `Con CV` SHALL mean primary CV present; only `Sin CV` SHALL mean no
primary CV. The editor SHALL keep at least one CV choice selected.

Candidate status SHALL appear as a compact, initially closed disclosure whose closed label states
whether every status, one named status, or a count of several statuses is selected. When opened, it
SHALL expose a vertical popover anchored to the disclosure, over the content without moving the
fields below it. The popover SHALL accommodate different option counts and narrow viewports without
overlap or horizontal overflow; a long list SHALL scroll within it. Every current status SHALL be
selected by default. Each option SHALL have a checkbox for multi-selection and a separately named,
radio-shaped button that selects only that status. The button SHALL retain button semantics. A partial
selection SHALL offer an action at the end to select all statuses. The editor SHALL keep at least one
status selected. Opening
or closing the disclosure SHALL NOT change the filter values or run a search. Clicking outside the
disclosure and its options SHALL close the options without changing the selection. Escape SHALL
close the options and return focus to the disclosure control. These controls and actions SHALL be
operable by keyboard and have distinct Spanish accessible names.

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

#### Scenario: Default CV and status choices

- **WHEN** the editor opens with unrestricted filters
- **THEN** both CV choices and every current status are selected, the status disclosure is closed
  and says that all statuses are selected, and neither family restricts the query

#### Scenario: CV presence maps to the existing filter

- **WHEN** a user selects only `Con CV`, only `Sin CV`, or both
- **THEN** the editor respectively supplies `yes`, `no`, or an unset CV filter without changing
  primary-CV presence semantics

#### Scenario: CV cannot appear empty

- **WHEN** a user tries to unselect the last checked CV choice
- **THEN** one choice remains selected and the visible choices agree with the filter supplied

#### Scenario: Select one status and restore all

- **WHEN** a user activates a status's select-only button and then the select-all action
- **THEN** the checkboxes select only that status, the action selects every status, and the closed
  disclosure summarizes each resulting selection

#### Scenario: Status cannot appear empty

- **WHEN** a user tries to unselect the last checked status
- **THEN** one status remains selected and the visible choices agree with the filter supplied

#### Scenario: Status disclosure is presentational

- **WHEN** a user opens or closes the status disclosure without changing a checkbox
- **THEN** the filter values stay the same and no search or save action runs

#### Scenario: Dismiss status options

- **WHEN** a user clicks outside the open status options or presses Escape while focused within them
- **THEN** the options close, the selected statuses remain unchanged, and Escape returns focus to
  the disclosure control

#### Scenario: Variable option count and viewport

- **WHEN** the status option set contains two, three or seven options and the editor is viewed at
  desktop or narrow width
- **THEN** the vertical popover stays within the viewport, scrolls if needed, and does not move the
  fields below it, while the basic filters share a row wherever space permits and wrap when they do not

#### Scenario: Keyboard access to status actions

- **WHEN** a keyboard user opens the status disclosure, selects only one status, changes a checkbox
  and restores all
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
