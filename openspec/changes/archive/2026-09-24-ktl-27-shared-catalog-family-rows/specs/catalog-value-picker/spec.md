## ADDED Requirements

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

- **WHEN** the advanced search page, a preset editor, a position editor, a candidate edit page and
  a candidate detail page are rendered
- **THEN** each shows the four families as rows in the order Habilidades, Idiomas, Programas,
  Etiquetas, with the label inline before the chips and the same row look

#### Scenario: Row label names the picker

- **WHEN** an assistive technology user reaches a family's picker in any host
- **THEN** the picker is announced with the family's visible row label

#### Scenario: Narrow rows wrap

- **WHEN** a row holding several chips is shown at 390 pixels wide
- **THEN** its chips wrap within the row and the page does not scroll horizontally

## MODIFIED Requirements

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
