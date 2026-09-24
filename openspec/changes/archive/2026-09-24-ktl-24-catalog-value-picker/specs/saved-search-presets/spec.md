## MODIFIED Requirements

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
