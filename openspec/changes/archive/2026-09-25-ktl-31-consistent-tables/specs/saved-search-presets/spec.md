## MODIFIED Requirements

### Requirement: Preset administration section

The application SHALL provide an administration section for presets, reachable only by actors
holding `presets.manage`. It SHALL offer a list of all presets and a create page and an edit page,
each at its own route; there SHALL be no separate read-only page. The list SHALL show each preset's
name, its update time and its last-used time (shown as never used when absent), and SHALL support
filtering by name, sorting and pagination. Directly below each preset's values, the list SHALL show
that preset's read-only criteria summary on a line spanning the full table width, as one wrapping
row of family labels and chips drawn like the search form's chips, or the empty summary text when
it has no criteria. The list SHALL sit in a panel like the other record tables. A click on either of a preset's lines, outside its controls,
SHALL open that preset's edit page, and the preset's name SHALL be a keyboard-reachable link to the
same page. The list SHALL NOT offer a separate view control, view dialog or edit button. Deleting
from the list SHALL require explicit confirmation and SHALL NOT navigate. Saving or cancelling the
create and edit pages SHALL return to the list. The create and edit pages SHALL tell the
administrator that presets are visible to everyone with search access and must not contain
personal data. All copy SHALL be Spanish.

#### Scenario: Administrator opens the section

- **WHEN** an actor with `presets.manage` opens the presets section
- **THEN** every preset is listed with its name, update time and last-used time, and each preset's
  criteria summary is shown on a full-width line below its values

#### Scenario: Criteria are viewed in a dialog

- **WHEN** the administrator wants to read a preset's criteria from the list
- **THEN** the criteria are already shown inline on the preset's full-width line
- **AND** no view control or dialog exists; the former dialog's role is covered by the inline line
  and the edit page

#### Scenario: Preset without criteria

- **WHEN** a listed preset has no criteria
- **THEN** its criteria line shows the empty summary text

#### Scenario: Preset is opened from its row

- **WHEN** the administrator clicks a preset's values line or criteria line outside its controls
- **THEN** that preset's edit page opens
- **AND** no view dialog or separate edit button exists in the list

#### Scenario: Route reached without permission

- **WHEN** an actor without `presets.manage` navigates directly to any presets administration route
- **THEN** the route guard redirects the actor away and no preset page is rendered

#### Scenario: Preset is created from the section

- **WHEN** the administrator enters a unique name and criteria on the create page and saves
- **THEN** the preset is stored and the list is shown again with the new preset in it

#### Scenario: Deletion is cancelled

- **WHEN** the administrator starts deleting a preset and dismisses the confirmation
- **THEN** no request is sent, the preset remains listed and the page does not navigate

#### Scenario: Save conflicts are explained

- **WHEN** a save is refused because the name is taken or the preset changed since it was loaded
- **THEN** the page shows a distinct Spanish message for each case and keeps the entered values

#### Scenario: Narrow viewport

- **WHEN** the list is shown at 390 pixels wide
- **THEN** the table scrolls within its own container and the page does not scroll horizontally
