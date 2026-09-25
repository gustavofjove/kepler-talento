## MODIFIED Requirements

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
