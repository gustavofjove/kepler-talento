## ADDED Requirements

### Requirement: Chips carry their value's catalog colour

Wherever the picker is used, each chip SHALL be drawn in the colour its value holds in its catalog
family. Its text, level, details and remove control SHALL keep their content. The colour SHALL be
resolved for active and deactivated values alike. A chip whose value no longer resolves to a
catalog value SHALL be drawn in the default orange. Colour SHALL never be the only way a chip's
value is conveyed.

Each value offered for adding SHALL show a circle in that value's colour to the left of its name.
The circle SHALL be decorative: the option's accessible name and text SHALL remain the value's
name alone. Level options SHALL carry no circle.

#### Scenario: Chip shows its value's colour

- **WHEN** the skill «Análisis» has the colour `blue` and a candidate's competencies, a search's
  criteria or a position's requirements include it
- **THEN** its chip is drawn blue in both the read-only and the editable picker

#### Scenario: Deactivated value keeps its colour on the chip

- **WHEN** a chip holds a tag that has since been deactivated and has the colour `green`
- **THEN** the chip is drawn green

#### Scenario: Offered values show their colour

- **WHEN** a user opens the list of skills to add and «Análisis» has the colour `blue`
- **THEN** the «Análisis» option shows a blue circle to the left of its name, and is still named
  «Análisis»

#### Scenario: Unresolved value falls back to orange

- **WHEN** a chip holds a value whose name no longer matches any value of its catalog family
- **THEN** the chip is drawn in the default orange
