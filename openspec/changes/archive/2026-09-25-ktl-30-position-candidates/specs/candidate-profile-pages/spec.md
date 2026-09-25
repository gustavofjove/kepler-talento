## ADDED Requirements

### Requirement: Positions panel on the candidate page

The candidate page SHALL show a «Posiciones» panel, after Experiencia and before Notas, when the actor
holds `positions.read`. It SHALL list every position the candidate is linked to, with open positions
first and closed (past) positions after them shown as muted. Each row SHALL show the position title
linking to the position page, the position status, the stage and the date added. Clicking a row
outside its controls opens the position; the title is the row's keyboard-reachable link.

For an actor holding `positions.manage`, a row on an open position SHALL offer a labelled stage
selector and «Quitar de la posición». A stage change SHALL save immediately. Removal SHALL ask for
confirmation. The panel SHALL offer «Añadir a posición» for an active candidate, opening a picker of
open positions searchable by text, in which positions the candidate is already on are not
selectable.

The panel SHALL NOT take part in per-panel edit mode: it has no «Editar» and does not count towards
unsaved-change protection. Without `positions.read`, the panel SHALL NOT render and no position
request SHALL be sent.

#### Scenario: Current and past positions are listed

- **WHEN** a reader opens a candidate linked to one open and one closed position
- **THEN** the open position is listed first, the closed one follows muted, and both show title, status, stage and date added

#### Scenario: Candidate is added to a position from their page

- **WHEN** a manager activates «Añadir a posición» and selects an open position the candidate does not match
- **THEN** the link is created at stage Nuevo and appears in the panel

#### Scenario: Closed position row

- **WHEN** a manager views a row whose position is closed
- **THEN** the stage is shown read-only and no removal is offered

#### Scenario: Removed candidate

- **WHEN** a manager opens a logically removed candidate
- **THEN** «Añadir a posición» is not offered, while existing links remain listed

#### Scenario: Actor lacks position permission

- **WHEN** an actor without `positions.read` opens the candidate page
- **THEN** no «Posiciones» panel is shown and no position request is sent

#### Scenario: Panel does not block editing

- **WHEN** a stage is changed while another panel is in edit mode
- **THEN** the other panel's draft and edit mode are unaffected
