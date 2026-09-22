## ADDED Requirements

### Requirement: Positions is a permission-filtered primary destination

The primary navigation SHALL declare **Posiciones** as a top-level destination after **Búsqueda** and before the **Admin** disclosure. It SHALL link to `/app/positions` and SHALL be present only when the current actor holds `positions.read`. The same navigation item and permission behavior SHALL be used in the desktop and mobile presentations.

#### Scenario: Position reader views desktop navigation

- **WHEN** an authenticated actor holding `positions.read` views the wide navigation
- **THEN** **Posiciones** appears after **Búsqueda** and before **Admin** and links to `/app/positions`

#### Scenario: Actor lacks position read permission

- **WHEN** an authenticated actor without `positions.read` views the navigation
- **THEN** no **Posiciones** navigation item is rendered

#### Scenario: Position reader views mobile navigation

- **WHEN** an actor holding `positions.read` opens the navigation below the existing breakpoint
- **THEN** the same **Posiciones** destination is keyboard-operable in the same order without introducing a second navigation tree
