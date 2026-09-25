# catalog-value-picker Specification

## Purpose

Defines the single interaction the application uses to choose business-catalog values (skills,
languages, programs and tags), with an optional or required level and family-specific details,
wherever those values are edited: search criteria, saved presets, position requirements and
candidate relations.

## Requirements

### Requirement: Type-to-filter adding

At rest, a catalog value picker SHALL present its label, its items and an add control on a single
line, wrapping only when they do not fit, and SHALL NOT show a text input. Activating the add
control SHALL replace it with a focused text input that immediately lists every active value of the
family not yet held. As the user types, the list SHALL narrow to the values whose name contains the
typed text, ignoring case and accents, in catalog order. Values already present in the picker SHALL
NOT be offered. Choosing an offered value, by pointer or by pressing Enter on the highlighted
option, SHALL add it as an item, clear the input and keep it open for the next value. When no value
matches, the picker SHALL say so in Spanish. Inactive catalog values SHALL NOT be offered. Pressing
Escape with the list closed, or moving focus away from the input, SHALL return it to the add
control.

#### Scenario: Add control reveals the list

- **WHEN** a user activates a picker's add control
- **THEN** a focused input replaces it and every active value not yet held is listed without typing

#### Scenario: Picker at rest

- **WHEN** a picker is not being used to add a value
- **THEN** it shows its label, chips and add control on one line, and no text input

#### Scenario: Value is added by typing

- **WHEN** a user types part of a skill's name in the skill picker and presses Enter on the
  highlighted option
- **THEN** that skill appears as an item, the input is cleared, and the skill is no longer offered

#### Scenario: Nothing matches

- **WHEN** the typed text matches no active value of the family
- **THEN** the picker shows a Spanish "no matches" message and adds nothing

#### Scenario: Inactive value is not offered

- **WHEN** a catalog value is deactivated
- **THEN** the picker does not offer it, whatever the user types

### Requirement: Items are chips with value, level and removal

Each item SHALL be presented as a chip showing its value and, for families with levels, its level
or, where the level is optional and empty, the Spanish label `Cualquier nivel`. Each chip SHALL offer
a remove control whose accessible name includes the value. A focused chip SHALL also be removable
with the Delete or Backspace key. Values the item holds that are no longer active in the catalog
SHALL still be shown.

#### Scenario: Chip is removed

- **WHEN** a user activates a chip's remove control, or presses Delete on a focused chip
- **THEN** the item is removed and its value is offered again in the input

#### Scenario: Deactivated value is still shown

- **WHEN** an item holds a value that has since been deactivated in the catalog
- **THEN** its chip is still shown and can be removed

### Requirement: Level and details are edited on the chip

For families with levels, activating a chip SHALL open an editor anchored to it offering the level
family's active values in catalog order, together with the family's detail fields: certification
for languages and years of experience for programs. Changing the level or a detail SHALL update the
item in place, without removing and re-adding it. Families without levels (tags) SHALL open no editor.
Closing the editor SHALL return focus to its chip.

#### Scenario: Level is changed in place

- **WHEN** a user opens a language chip and chooses a different level
- **THEN** the same item now carries the new level and no remove-then-add occurs

#### Scenario: Tag chip has no editor

- **WHEN** a user activates a tag chip
- **THEN** no level or detail editor opens

### Requirement: Optional and required levels

A picker SHALL be configured with either an optional or a required level. With an optional level, an
added item SHALL start with an empty level, meaning any level. With a required level, an added item
SHALL be committed at once with the lowest active value of its level family, the first in catalog
order, and the level editor SHALL NOT open. The level and the family's details SHALL then be
changeable by activating the chip. A picker with a required level SHALL NOT commit an item without a
level.

#### Scenario: Optional level

- **WHEN** a value is added to a picker whose level is optional
- **THEN** the item is committed at once with the label `Cualquier nivel`

#### Scenario: Required level defaults to the lowest

- **WHEN** a value is added to a picker whose level is required and the level family's first
  active value in catalog order is A1
- **THEN** the item is committed at once with level A1 and no level editor opens

#### Scenario: Required level is chosen

- **WHEN** a user activates a chip committed with the default level and chooses another level
- **THEN** the same item now carries the chosen level

#### Scenario: Required level is abandoned

- **WHEN** a user activates a chip committed with the default level and closes the editor without
  choosing a level
- **THEN** the item keeps its default level and nothing further is committed

### Requirement: Asynchronous commits show their state

When committing an add, change or removal takes time, the affected chip SHALL show a pending state
until it completes. When a commit fails, the chip SHALL show the Spanish error message and offer a
retry. The failure SHALL leave the other chips unchanged.

#### Scenario: Commit is pending

- **WHEN** an item's commit has not yet completed
- **THEN** its chip shows a pending state

#### Scenario: Commit fails

- **WHEN** an item's commit is refused
- **THEN** its chip shows the error message and a retry action, and the other chips are unchanged

### Requirement: Read-only and disabled pickers

A read-only picker SHALL show its chips only, with no add control, input, remove control or editor;
when it holds no items it SHALL show its Spanish empty-state text instead. An editable picker SHALL
NOT show an empty-state text, since its add control already stands for the empty list. A disabled
picker, used while its catalogs cannot be loaded, SHALL show its chips and a disabled add control.
A picker whose level is required SHALL also be disabled in the same way while its level family has
no active value. Wherever a picker is disabled because catalogs cannot be loaded or offer no level,
the page SHALL show the existing catalog status notice.

#### Scenario: Read-only picker

- **WHEN** a picker is rendered read-only
- **THEN** its chips are shown and no add control, input, remove control or editor is present

#### Scenario: Empty read-only picker

- **WHEN** a read-only picker holds no items
- **THEN** it shows its empty-state text

#### Scenario: Catalogs are unavailable

- **WHEN** the catalogs a picker needs fail to load
- **THEN** the picker's add control is disabled, its existing chips remain visible, and the catalog
  notice is shown

#### Scenario: Required level family has no active value

- **WHEN** a picker's level is required and every value of its level family is inactive
- **THEN** its add control is disabled, its existing chips remain visible and removable, and the
  catalog notice is shown

### Requirement: Picker is keyboard operable, localized and responsive

Every picker action (filtering, adding, moving between chips, opening the editor, choosing a level,
editing details and removing) SHALL be operable by keyboard alone. Every control SHALL have a
Spanish accessible name, and all copy SHALL come from the localization catalogue. Each picker SHALL
expose stable test identifiers derived from its host and family, identical wherever the same host and
family are rendered. At 390 pixels wide the chips SHALL wrap and the editor SHALL remain within the
viewport, without horizontal page scroll.

#### Scenario: Keyboard-only use

- **WHEN** a user adds a value, changes its level and removes it using only the keyboard
- **THEN** every step succeeds and focus is never lost

#### Scenario: Narrow viewport

- **WHEN** a picker with several chips and an open editor is shown at 390 pixels wide
- **THEN** every chip and control is reachable and the page does not scroll horizontally

### Requirement: Families share one row layout

Wherever the application edits or shows skills, languages, programs and tags together (search
criteria, saved presets, position requirements and candidate profiles), it SHALL present them as
one row per family, in the order Habilidades, Idiomas, Programas, Etiquetas, stacked in a single
column. Every such row SHALL have the same look in every host: a bordered row whose visible
family label sits in a fixed-width column on the same line as its chips and add control, wrapping
only when they do not fit. The visible label SHALL be the picker's accessible name. The family
labels SHALL be `Habilidades`, `Idiomas`, `Programas` and `Etiquetas` in every host. A host MAY add
a control next to the label, such as the search `ANY`/`ALL` toggle, without changing the row
layout.

#### Scenario: Same rows in every host

- **WHEN** the advanced search page, a preset editor, a position editor, and a candidate page with
  Competencias both read-only and in edit mode are rendered
- **THEN** each shows the four families as rows in the order Habilidades, Idiomas, Programas,
  Etiquetas, with the label inline before the chips and the same row look

#### Scenario: Row label names the picker

- **WHEN** an assistive technology user reaches a family's picker in any host
- **THEN** the picker is announced with the family's visible row label

#### Scenario: Narrow rows wrap

- **WHEN** a row holding several chips is shown at 390 pixels wide
- **THEN** its chips wrap within the row and the page does not scroll horizontally
