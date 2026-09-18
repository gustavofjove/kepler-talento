## ADDED Requirements

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

The shared criteria editor SHALL present the tag family with the same field naming, test
identifiers and validation behavior as the other multi-value families, except that it SHALL
present no level control, because a tag has no level. The shared criteria summary SHALL present
the selected tags and their mode on every screen that renders it.

#### Scenario: Tag criteria are edited

- **WHEN** the criteria editor is rendered on the search page and on the preset edit page with
  identical filters containing tags
- **THEN** both show the same tag controls, with the same field names and test identifiers, and
  neither shows a level control for a tag

#### Scenario: Tag criteria are summarized

- **WHEN** the criteria summary is rendered for filters containing tags
- **THEN** it shows the selected tags and the tag mode, with the same content on every screen
  that renders it
