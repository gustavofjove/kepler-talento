## Purpose

Defines how the application shell exposes the available sections to a signed-in user:
which entries are visible for which permissions, how administration entries are grouped
behind a single parent, and how the navigation stays operable across viewport widths and
input methods (pointer, keyboard, touch).

## Requirements

### Requirement: Primary navigation entries and permission visibility

The primary navigation SHALL offer `Dashboard`, `Candidatos`, `Búsqueda`, `Catálogos`,
`Usuarios`, `Roles` and `Importación`, each pointing at its existing route. An entry
SHALL be rendered only when the signed-in profile holds the permission that governs its
destination: `Candidatos` and `Búsqueda` require `view_candidates`, `Catálogos` requires
`manage_catalogs`, `Usuarios` requires `manage_users`, `Roles` requires `manage_roles`,
`Importación` requires `import_candidates`. `Dashboard` SHALL always be present for an
authenticated user.

Hiding an entry is a convenience only. The system SHALL continue to enforce access to
every destination through the route guard and through database row-level security, so
that a user who reaches a hidden route by URL is refused by those layers and not by the
absence of a link.

All entry labels SHALL be rendered in Spanish with correct accents.

#### Scenario: A user with every permission sees every destination

- **WHEN** a profile holding `view_candidates`, `manage_catalogs`, `manage_users`,
  `manage_roles` and `import_candidates` opens the application
- **THEN** all seven destinations are reachable from the primary navigation

#### Scenario: A user without candidate permission does not see candidate entries

- **WHEN** a profile without `view_candidates` opens the application
- **THEN** `Candidatos` and `Búsqueda` are absent from the navigation
- **AND** `Dashboard` is still present

#### Scenario: Reaching a hidden destination by URL is still refused

- **WHEN** a profile without `manage_roles` navigates directly to the roles route
- **THEN** the route guard redirects the user away from it
- **AND** the refusal does not depend on the navigation having hidden the entry

### Requirement: Administration entries are grouped under a non-navigable parent

`Catálogos`, `Usuarios`, `Roles` and `Importación` SHALL be presented as children of a
single parent entry labelled `Admin`, and SHALL NOT appear as top-level entries.

The `Admin` parent SHALL be an activatable control that only opens and closes the group.
It SHALL NOT be a link, SHALL NOT carry a destination, and activating it SHALL NOT change
the current route.

The `Admin` parent SHALL be rendered only when at least one of its children is visible to
the signed-in profile. Each child SHALL retain its own individual permission check, so the
group MAY contain fewer than four children.

#### Scenario: Administration entries are no longer top-level

- **WHEN** a user with every permission views the primary navigation
- **THEN** the top level offers `Dashboard`, `Candidatos`, `Búsqueda` and `Admin`
- **AND** `Catálogos`, `Usuarios`, `Roles` and `Importación` are reachable only after
  opening `Admin`

#### Scenario: Activating the parent does not navigate

- **WHEN** the user activates the `Admin` parent
- **THEN** the current route is unchanged
- **AND** the group's open state is toggled

#### Scenario: A single administration permission still yields the group

- **WHEN** a profile holds `manage_roles` and none of `manage_catalogs`, `manage_users`,
  `import_candidates`
- **THEN** the `Admin` parent is rendered
- **AND** opening it reveals `Roles` as its only child

#### Scenario: No administration permission hides the group entirely

- **WHEN** a profile holds none of `manage_catalogs`, `manage_users`, `manage_roles`,
  `import_candidates`
- **THEN** no `Admin` parent is present in the navigation

### Requirement: Active route indication

The navigation SHALL indicate the entry matching the current route using the shell's
established active treatment, and SHALL expose that entry as the current page to assistive
technology.

When the current route belongs to one of the administration children, the `Admin` parent
SHALL also carry the active treatment, so that the active section is discoverable without
opening the group.

#### Scenario: A top-level entry is active

- **WHEN** the current route is the candidate list
- **THEN** the `Candidatos` entry carries the active treatment and is announced as the
  current page

#### Scenario: An administration child is active

- **WHEN** the current route is the roles page
- **THEN** the `Admin` parent carries the active treatment
- **AND** the `Roles` child carries the active treatment once the group is open

#### Scenario: Landing on an administration route directly

- **WHEN** the user loads the roles route by URL or reloads on it
- **THEN** the `Admin` parent already shows the active treatment before any interaction

### Requirement: Wide-viewport navigation layout

At a viewport width of 768 pixels or more, the navigation SHALL present its top-level
entries horizontally within the header, and SHALL NOT offer a hamburger control.

Activating the `Admin` parent SHALL open a panel positioned directly below it, listing the
visible administration children. The panel SHALL be layered above the page content and
below the notification region, and SHALL NOT be clipped by the header.

On a route belonging to an administration child, the group SHALL be open when the
navigation first renders.

#### Scenario: Horizontal top level

- **WHEN** the viewport is 1280 pixels wide
- **THEN** `Dashboard`, `Candidatos`, `Búsqueda` and `Admin` are laid out horizontally in
  the header
- **AND** no hamburger control is present

#### Scenario: Opening the administration panel

- **WHEN** the user activates `Admin`
- **THEN** a panel appears immediately below it containing the visible administration
  children
- **AND** the panel is fully visible over the page content

#### Scenario: Group opens on an administration route

- **WHEN** the navigation first renders on the import route
- **THEN** the administration group is already open

### Requirement: Narrow-viewport navigation layout

At a viewport width of 767 pixels or less, the navigation SHALL hide its horizontal
entries and SHALL offer a single hamburger control at the trailing edge of the header,
after the role indicator.

Activating the hamburger SHALL open a full-width vertical panel below the header. The
panel SHALL take part in the normal document flow and displace the page content downward
rather than overlaying it.

Within that panel, `Dashboard`, `Candidatos` and `Búsqueda` SHALL be full-width vertical
entries, and `Admin` SHALL behave as an accordion: activating it SHALL reveal its visible
children below it, displacing the entries that follow, and the children SHALL be indented
relative to their siblings to convey the hierarchy.

The header SHALL remain a single row at every supported width, with no wrapping and no
overlap between the brand block, the navigation control and the sign-out control.

Every activatable navigation control SHALL present a touch target of at least 44 by 44
pixels.

#### Scenario: Hamburger replaces the horizontal entries

- **WHEN** the viewport is 390 pixels wide
- **THEN** the horizontal entries are not displayed
- **AND** a hamburger control is present at the trailing edge of the header
- **AND** the header occupies a single row with no overlap

#### Scenario: Opening the vertical panel

- **WHEN** the user activates the hamburger
- **THEN** a full-width vertical panel opens below the header containing `Dashboard`,
  `Candidatos`, `Búsqueda` and the `Admin` accordion

#### Scenario: Expanding the administration accordion

- **WHEN** the user activates `Admin` inside the vertical panel
- **THEN** its visible children appear below it, indented
- **AND** the content that follows is displaced downward rather than covered

### Requirement: Dismissing an open navigation panel

An open panel SHALL close when the user presses `Escape`, when the user activates a
region outside it, and whenever the route changes.

When a panel is closed with `Escape`, keyboard focus SHALL return to the control that
opened it.

#### Scenario: Escape closes and restores focus

- **WHEN** the administration panel is open and the user presses `Escape`
- **THEN** the panel closes
- **AND** focus is on the `Admin` parent

#### Scenario: Activating outside closes the panel

- **WHEN** the administration panel is open and the user activates a point outside it
- **THEN** the panel closes

#### Scenario: Navigating closes every panel

- **WHEN** the user activates a navigation entry from within any open panel
- **THEN** the application navigates to that destination
- **AND** every open panel is closed

#### Scenario: Crossing the breakpoint with a panel open

- **WHEN** a panel is open and the viewport is resized across 768 pixels
- **THEN** the navigation remains fully operable at the new width, with no panel left
  visible but unreachable

### Requirement: Keyboard and assistive-technology operability

The navigation SHALL be fully operable by keyboard alone at every viewport width: reaching
a group parent, opening it, reaching its children and activating one SHALL all be possible
without a pointer, and the focused control SHALL be visibly indicated.

Each group parent and the hamburger control SHALL expose their expanded or collapsed state
and identify the region they control to assistive technology. Their accessible names SHALL
be the entry label, or Spanish text describing the action for the hamburger; decorative
indicators such as a chevron SHALL NOT contribute to the accessible name.

The skip-to-content affordance SHALL remain the first focusable element of the page at
every viewport width.

Where the user has requested reduced motion, the navigation SHALL NOT animate the opening
or closing of its panels or the rotation of its indicators.

Navigation text SHALL keep a contrast ratio of at least 4.5:1 against its background in
both resting and active states.

#### Scenario: Keyboard path through the group

- **WHEN** a keyboard user focuses the `Admin` parent and activates it
- **THEN** the group opens and its children are reachable by continued tabbing
- **AND** activating a child navigates to that destination

#### Scenario: State is exposed to assistive technology

- **WHEN** the administration group is closed and then opened
- **THEN** its parent reports the collapsed and then the expanded state, and identifies
  the panel it controls

#### Scenario: Skip link keeps priority

- **WHEN** the user tabs once from the top of the page at 390 pixels wide
- **THEN** the skip-to-content affordance receives focus
- **AND** activating it moves focus to the main content region

#### Scenario: Reduced motion is honoured

- **WHEN** the user has requested reduced motion and opens a panel
- **THEN** the panel appears without a transition and the indicator does not animate
