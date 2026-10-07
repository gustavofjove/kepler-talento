## Purpose

Defines the «Inicio» home page: a summary of candidates, availability, open positions and shared
saved searches in which every figure and row leads to the screen where the user acts on it, each
panel governed by its own permission and its own loading, empty and error states.

## Requirements

### Requirement: Home page identity and header actions

The application home route (`/app`) SHALL render a page whose main heading is `Inicio` and whose
subtitle is `Resumen de candidatos, posiciones y búsquedas guardadas.`

The header SHALL offer up to three create actions, each rendered only when the actor holds its
permission: `Alta de candidato` (`candidates.create`) leading to candidate creation, `Nueva posición`
(`positions.manage`) leading to position creation, and `Importar candidatos` (`candidates.import`)
leading to the import page. The page SHALL NOT offer shortcuts that only repeat the primary
navigation. Hiding an action is a convenience only; the route guards and the API SHALL continue to
refuse unauthorized callers.

#### Scenario: Actor holding every create permission

- **WHEN** an actor holding `candidates.create`, `positions.manage` and `candidates.import` opens the
  home page
- **THEN** the heading reads `Inicio` and the three actions are shown, each leading to its
  destination

#### Scenario: Candidate creation is not offered without permission

- **WHEN** an actor without `candidates.create` opens the home page
- **THEN** no `Alta de candidato` action is rendered

#### Scenario: Navigation shortcuts are gone

- **WHEN** any actor opens the home page
- **THEN** no «Centro operativo» block and no «Con CV principal» figure is rendered

### Requirement: Candidate summary card

With `candidates.read`, the page SHALL show a `Candidatos` card with the number of active
candidates and its availability split into available, unavailable and not checked. The not-checked
figure SHALL be the active total minus the other two, and SHALL never be below zero. A bar SHALL
show the split proportionally; it SHALL be hidden from assistive technology and SHALL NOT render
when the total is zero. Every figure SHALL also be rendered as text, so meaning never depends on
colour.

Each figure SHALL be a link: the active total to the candidate list, available to the list filtered
by `available` and sorted by check date descending, unavailable to the list filtered by
`unavailable`, and not checked to the list filtered by `unknown`.

With `candidates.delete`, the card SHALL also show the number of inactive candidates (all
candidates minus active, never below zero) as plain text, not a link. Without that permission no
request including inactive candidates SHALL be sent.

The page SHALL also show a `Sin CV principal` tile with the number of active candidates without a
primary CV, linking to the candidate list filtered by `cv=no`.

#### Scenario: Availability split

- **GIVEN** 10 active candidates: 4 `available`, 2 `unavailable` and 4 `unknown`
- **WHEN** a reader with `candidates.read` opens the home page
- **THEN** the card shows 10 activos, 4 disponibles, 2 no disponibles and 4 sin comprobar
- **AND** each figure opens the candidate list with the matching filter in the URL

#### Scenario: Not-checked figure never goes negative

- **WHEN** the counts arrive inconsistent because data changed between requests, so available plus
  unavailable exceeds the total
- **THEN** the not-checked figure shows 0

#### Scenario: Inactive count for removers only

- **WHEN** an actor holding `candidates.delete` opens the page and 3 candidates are removed
- **THEN** `3 inactivos` is shown as text
- **AND** for an actor without `candidates.delete`, no figure is shown and no search including
  inactive candidates is sent

#### Scenario: Candidates without a primary CV

- **WHEN** a reader opens the page
- **THEN** the `Sin CV principal` tile shows the number of active candidates without a primary CV and
  opens `/app/candidates?cv=no`

### Requirement: Open positions summary

With `positions.read`, the page SHALL show a `Posiciones abiertas` tile with the number of open
positions, linking to the position list, and a `Posiciones abiertas` panel listing at most the 5
most recently updated open positions. Each row SHALL show the title, the count of linked candidates
at each stage (`Nuevo`, `Preseleccionado`, `Entrevista`, `Contratado`, `Descartado`) and the total.
The title SHALL be a keyboard-reachable link to the position and the whole row SHALL be
activatable. The panel SHALL offer `Ver todas (N)` leading to the position list. When there are no
open positions it SHALL show `No hay posiciones abiertas.`, plus `Nueva posición` for actors holding
`positions.manage`.

#### Scenario: Open positions with stage counts

- **GIVEN** 6 open positions and 1 closed position
- **WHEN** a reader with `positions.read` opens the page
- **THEN** the tile shows 6 and the panel lists the 5 most recently updated open positions
- **AND** a position with links at Nuevo ×3, Entrevista ×1 and Descartado ×2 shows 3, 0, 1, 0, 2
  and a total of 6

#### Scenario: Opening a position from the panel

- **WHEN** the user activates a row or its title
- **THEN** the position's page opens

#### Scenario: No open positions

- **WHEN** there are no open positions
- **THEN** the panel shows `No hay posiciones abiertas.`, and an actor holding `positions.manage`
  also sees `Nueva posición`

### Requirement: Recent candidate panels

With `candidates.read`, the page SHALL show two panels of at most 5 active candidates each:

- `Últimos disponibles`: candidates whose availability is `available`, most recently checked first,
  each with its availability and the elapsed time since the check, and `Ver todos (N)` leading to
  the same view as the «disponibles» figure. Empty state: `Ningún candidato está marcado como
disponible.`
- `Últimos añadidos`: candidates most recently created first, each with its availability and
  `Sin CV` when it has no primary CV, and `Ver todos` leading to the candidate list sorted by
  creation descending. Empty state: `Todavía no hay candidatos.`

Each row SHALL show only the candidate's name, linking to the candidate's page, plus the state
above. It SHALL NOT show e-mail, phone or any CV action.

#### Scenario: Most recently confirmed available first

- **GIVEN** candidates checked `available` on 01/10, 03/10 and 05/10, and one checked `unavailable`
  on 06/10
- **WHEN** a reader opens the page
- **THEN** `Últimos disponibles` lists the three available candidates, 05/10 first, each with the
  elapsed time
- **AND** `Ver todos (3)` opens the list filtered by `available` and sorted by check date descending

#### Scenario: Most recently added first

- **WHEN** a reader opens the page
- **THEN** `Últimos añadidos` lists the five most recently created active candidates, newest first,
  each with its availability and `Sin CV` when it has no primary CV
- **AND** `Ver todos` opens the candidate list sorted by creation descending

#### Scenario: Rows expose names only

- **WHEN** the recent panels render
- **THEN** no e-mail address, phone number or CV control appears in them

### Requirement: Saved searches panel

With `candidates.read`, the page SHALL show a `Búsquedas guardadas` panel listing at most 5 shared
presets, ordered by last use descending, never-used presets last, then by name. Each row SHALL show
the preset name linking to the advanced search with that preset applied, and `Usada <tiempo>` or
`Sin usar`. Actors holding `presets.manage` SHALL also see `Gestionar presets` leading to preset
administration. Empty state: `No hay búsquedas guardadas.`

#### Scenario: Ordering by last use

- **GIVEN** presets last used 3 days ago, 1 day ago and never
- **WHEN** a reader opens the page
- **THEN** they are listed in the order 1 day, 3 days, never

#### Scenario: Opening a saved search

- **WHEN** the user activates a preset in the panel
- **THEN** the advanced search opens with that preset applied

### Requirement: Independent, permission-scoped panels

Each panel SHALL load, fail and show its empty state independently: one panel's failure SHALL NOT
blank another. Loading SHALL be announced to assistive technology and a failure SHALL show
`No se ha podido cargar este resumen.` in that panel only. Requests SHALL run in parallel and be
abandoned when the user leaves the page.

A panel the actor may not see SHALL NOT be rendered and SHALL send no request. An actor holding
neither `candidates.read` nor `positions.read` SHALL see the header, any actions they hold, and
`No tienes acceso a ningún resumen.`, and the page SHALL send no data request.

The page SHALL be composed from existing endpoints, with at most 7 data requests: 5 candidate
searches, 1 position list and 1 preset list. No candidate search SHALL carry a text filter, and no
search term, filter value or candidate identifier SHALL be placed in a URL by the page.

#### Scenario: Positions fail, candidates still show

- **WHEN** the position list request fails
- **THEN** the positions tile and panel show the error message
- **AND** every candidate panel still shows its data

#### Scenario: Position reader only

- **WHEN** an actor holding `positions.read` but not `candidates.read` opens the page
- **THEN** only the positions tile and panel are rendered
- **AND** no candidate search and no preset request is sent

#### Scenario: Candidate reader only

- **WHEN** an actor holding `candidates.read` but not `positions.read` opens the page
- **THEN** only the candidate panels are rendered and no position request is sent

#### Scenario: Neither read permission

- **WHEN** an actor holding neither `candidates.read` nor `positions.read` opens the page
- **THEN** `No tienes acceso a ningún resumen.` is shown and no data request is sent

#### Scenario: Every panel empty

- **WHEN** there are no open positions, no available candidates, no candidates and no presets
- **THEN** each panel shows its own empty message

### Requirement: Home page accessibility and layout

Each panel SHALL be a labelled region with a heading. Every link SHALL be reachable and operable by
keyboard with a visible focus indicator. All copy SHALL be Spanish with correct accents. At 390
pixels wide the panels SHALL stack in reading order and the page SHALL NOT scroll horizontally; the
positions table MAY scroll inside its own container.

#### Scenario: Narrow viewport

- **WHEN** the page is shown 390 pixels wide
- **THEN** the panels stack and the document has no horizontal scroll

#### Scenario: Assistive technology

- **WHEN** a screen reader user explores the page
- **THEN** each panel is announced as a named region with a heading, loading is announced, and the
  availability bar is not announced
