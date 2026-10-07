## MODIFIED Requirements

### Requirement: Search page applies presets only

The advanced search page SHALL let any actor with `candidates.read` choose a preset from the shared
library and apply it, replacing the current filters, running the search and recording the use. It
SHALL NOT offer creating, renaming, updating or deleting presets to any actor. It SHALL show a link
to the presets administration section only to actors holding `presets.manage`.

The page SHALL also accept a preset identifier in its address (`?preset=<id>`). When present on
load, the page SHALL apply that preset exactly as if it had been chosen in the picker, recording the
use, SHALL select it in the picker, and SHALL then remove the parameter from the address without
adding a history entry, so that a reload or going back does not apply it again. A value that is not
a valid identifier, or a preset that no longer exists, SHALL show the existing apply-failed error,
leave the filters empty and run the default search. The preset's filters SHALL NOT be placed in the
address.

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

#### Scenario: Opening the search page with a preset identifier

- **WHEN** a recruiter opens `/app/search?preset=<id>` for an existing preset
- **THEN** the form shows that preset's filters, its results are displayed, it is selected in the
  picker and its last-used time is updated
- **AND** the address no longer contains `preset`, and reloading does not apply it again

#### Scenario: Preset identifier is invalid or deleted

- **WHEN** a recruiter opens `/app/search?preset=abc` or the identifier of a deleted preset
- **THEN** the apply-failed error is shown, the filters are empty and the default results are
  displayed
