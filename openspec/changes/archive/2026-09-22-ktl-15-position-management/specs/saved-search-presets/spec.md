## ADDED Requirements

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
