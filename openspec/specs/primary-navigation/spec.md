## Purpose

Defines how the application shell exposes the available sections to a signed-in user:
which entries are visible for which permissions, how administration entries are grouped
behind a single parent, and how the navigation stays operable across viewport widths and
input methods (pointer, keyboard, touch).

## Requirements

### Requirement: Primary navigation entries and permission visibility

The primary navigation SHALL offer `Dashboard`, `Candidatos`, `Búsqueda`, `Catálogos`,
`Presets`, `Usuarios`, `Roles`, `Importación` and `Auditoría`, each pointing at its existing route.
An entry SHALL be rendered only when the signed-in profile holds the permission that governs its
destination: `Candidatos` and `Búsqueda` require `candidates.read`, `Catálogos` requires
`catalogs.manage`, `Presets` requires `presets.manage`, `Usuarios` requires `users.manage`,
`Roles` requires `roles.manage`, `Importación` requires `candidates.import`, `Auditoría` requires
`audit.read`. `Dashboard` SHALL always be present for an authenticated user.

Hiding an entry is a convenience only. The system SHALL continue to enforce access to
every destination through the route guard and through the API or database authorization that
protects its data, so that a user who reaches a hidden route by URL is refused by those layers and
not by the absence of a link.

All entry labels SHALL be rendered in Spanish with correct accents.

#### Scenario: A user with every permission sees every destination

- **WHEN** a profile holding `candidates.read`, `catalogs.manage`, `presets.manage`,
  `users.manage`, `roles.manage`, `candidates.import` and `audit.read` opens the application
- **THEN** all nine destinations are reachable from the primary navigation

#### Scenario: A user without candidate permission does not see candidate entries

- **WHEN** a profile without `candidates.read` opens the application
- **THEN** `Candidatos` and `Búsqueda` are absent from the navigation
- **AND** `Dashboard` is still present

#### Scenario: Reaching a hidden destination by URL is still refused

- **WHEN** a profile without `roles.manage` navigates directly to the roles route
- **THEN** the route guard redirects the user away from it
- **AND** the refusal does not depend on the navigation having hidden the entry

#### Scenario: A user without preset permission does not see presets

- **WHEN** a profile holding `catalogs.manage` but not `presets.manage` opens the application
- **THEN** `Presets` is absent from the navigation
- **AND** navigating directly to the presets route is redirected by the route guard

#### Scenario: A user without audit permission does not see the trail

- **WHEN** a profile holding every other permission but not `audit.read` opens the application
- **THEN** `Auditoría` is absent from the navigation
- **AND** navigating directly to the audit route is redirected by the route guard, and the API
  refuses the underlying request independently

### Requirement: Administration entries are grouped under a non-navigable parent

`Catálogos`, `Presets`, `Usuarios`, `Roles`, `Importación` and `Auditoría` SHALL be presented as
children of a single parent entry labelled `Admin`, in that order, and SHALL NOT appear as top-level
entries.

The `Admin` parent SHALL be an activatable control that only opens and closes the group.
It SHALL NOT be a link, SHALL NOT carry a destination, and activating it SHALL NOT change
the current route.

The `Admin` parent SHALL be rendered only when at least one of its children is visible to
the signed-in profile. Each child SHALL retain its own individual permission check, so the
group MAY contain fewer than six children.

#### Scenario: Administration entries are no longer top-level

- **WHEN** a user with every permission views the primary navigation
- **THEN** the top level offers `Dashboard`, `Candidatos`, `Búsqueda` and `Admin`
- **AND** `Catálogos`, `Presets`, `Usuarios`, `Roles`, `Importación` and `Auditoría` are reachable
  only after opening `Admin`

#### Scenario: Activating the parent does not navigate

- **WHEN** the user activates the `Admin` parent
- **THEN** the current route is unchanged
- **AND** the group's open state is toggled

#### Scenario: A single administration permission still yields the group

- **WHEN** a profile holds `roles.manage` and none of `catalogs.manage`, `presets.manage`,
  `users.manage`, `candidates.import`, `audit.read`
- **THEN** the `Admin` parent is rendered
- **AND** opening it reveals `Roles` as its only child

#### Scenario: Preset permission alone yields the group

- **WHEN** a profile holds `presets.manage` and none of `catalogs.manage`, `users.manage`,
  `roles.manage`, `candidates.import`, `audit.read`
- **THEN** the `Admin` parent is rendered
- **AND** opening it reveals `Presets` as its only child

#### Scenario: Audit permission alone yields the group

- **WHEN** a profile holds `audit.read` and none of `catalogs.manage`, `presets.manage`,
  `users.manage`, `roles.manage`, `candidates.import`
- **THEN** the `Admin` parent is rendered
- **AND** opening it reveals `Auditoría` as its only child

#### Scenario: No administration permission hides the group entirely

- **WHEN** a profile holds none of `catalogs.manage`, `presets.manage`, `users.manage`,
  `roles.manage`, `candidates.import`, `audit.read`
- **THEN** no `Admin` parent is present in the navigation

#### Scenario: A preset administration route marks the group active

- **WHEN** the current route is the presets list or a preset create or edit route
- **THEN** the `Admin` parent carries the active treatment

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

### Requirement: Breadcrumb trail on sub-pages

Every page that sits below a list SHALL render a breadcrumb trail above its main heading. The trail
SHALL show where the page sits in the application and SHALL let the user return to its parents in one
activation. The trails SHALL be:

| Page               | Trail                                         |
| ------------------ | --------------------------------------------- |
| Candidate detail   | `Candidatos` › candidate full name            |
| Candidate creation | `Candidatos` › `Nuevo candidato`              |
| Candidate edit     | `Candidatos` › candidate full name › `Editar` |
| Position detail    | `Posiciones` › position title                 |
| Position creation  | `Posiciones` › `Nueva posición`               |
| Position edit      | `Posiciones` › position title › `Editar`      |
| Preset creation    | `Admin` › `Presets` › `Nuevo preset`          |
| Preset edit        | `Admin` › `Presets` › preset name             |

`Candidatos`, `Posiciones` and `Presets` SHALL lead to the candidate list, the position list and the
preset list respectively, without restoring any list filter, sort or page. A candidate full name or
position title that is not the last segment SHALL lead to that record's detail page. The trail SHALL
be the same whichever page the user arrived from.

The segment that names the current page (the last one in each trail above) SHALL NOT be activatable
and SHALL be exposed to assistive technology as the current page. `Admin` SHALL NOT be activatable,
because the Admin navigation parent has no destination. Any other segment SHALL be activatable only when the signed-in
profile holds the permission that governs its destination: `candidates.read` for `Candidatos` and a
candidate name, `positions.read` for `Posiciones` and a position title, `presets.manage` for
`Presets`. Otherwise it SHALL be rendered as plain text. Rendering a segment as text is a convenience
only: access to every destination SHALL continue to be enforced by the route guard and the API.

Record segments SHALL show the stored record, never a value being edited on the page. While the
record is loading, the record segment and any segment after it SHALL be omitted. No empty or
placeholder label SHALL be shown, and the remaining parent segments SHALL stay activatable under the
permission rule above. When the record fails to load or is not found, the parent segments SHALL
still be offered in the same way. The record label SHALL NOT be copied into the page URL, the document title or any log.

Top-level destinations SHALL NOT render a breadcrumb. These are the Dashboard, the candidate list,
Búsqueda, the position list, the preset list, Catálogos, Usuarios, Roles, Importación and Auditoría.

The trail SHALL be a navigation landmark with the Spanish accessible name `Ruta de navegación`,
presenting its segments as an ordered list. Separators between segments SHALL NOT be exposed to
assistive technology. Activatable segments SHALL be reachable and operable by keyboard with a
visible focus indicator. The current page SHALL NOT add a tab stop. At every supported viewport
width the trail SHALL fit without causing horizontal page scroll. It SHALL wrap or visually truncate
long labels while keeping the full label available. All fixed segment labels SHALL be Spanish with
correct accents.

#### Scenario: Returning to the candidate list from a profile

- **WHEN** a profile holding `candidates.read` opens a candidate's detail page
- **THEN** the trail reads `Candidatos` › the candidate's full name
- **AND** the full name is not activatable and is announced as the current page
- **AND** activating `Candidatos` opens the candidate list with no filters applied

#### Scenario: Returning to the profile from the edit page

- **WHEN** a profile holding `candidates.read` and `candidates.update` opens a candidate's edit page
- **THEN** the trail reads `Candidatos` › the candidate's full name › `Editar`
- **AND** activating the full name opens that candidate's detail page

#### Scenario: Creating a candidate

- **WHEN** the user opens the candidate creation page
- **THEN** the trail reads `Candidatos` › `Nuevo candidato`

#### Scenario: Position trails

- **WHEN** a profile holding `positions.read` and `positions.manage` opens a position's edit page
- **THEN** the trail reads `Posiciones` › the position title › `Editar`
- **AND** `Posiciones` leads to the position list and the title leads to the position's detail page

#### Scenario: An unsaved edit does not change the trail

- **WHEN** the user changes the title field on a position's edit page without saving
- **THEN** the trail still shows the stored title

#### Scenario: Preset trails start with a non-activatable Admin segment

- **WHEN** a profile holding `presets.manage` opens a preset's edit page
- **THEN** the trail reads `Admin` › `Presets` › the preset name
- **AND** `Admin` is plain text
- **AND** activating `Presets` opens the preset list

#### Scenario: The trail does not depend on the origin

- **WHEN** the user opens a candidate's detail page from the advanced search results or from a
  position's matching candidates
- **THEN** the trail reads `Candidatos` › the candidate's full name

#### Scenario: Loading shows only the parent segments

- **WHEN** a candidate's edit page is still loading the candidate
- **THEN** the trail shows `Candidatos`, still activatable, and neither the name nor `Editar`

#### Scenario: A failed load still offers the way back

- **WHEN** a candidate's detail page fails to load the candidate or the candidate is not found
- **THEN** the trail still offers `Candidatos` leading to the candidate list

#### Scenario: A segment the viewer may not open is plain text

- **WHEN** a profile holding `candidates.update` but not `candidates.read` opens a candidate's edit page
- **THEN** `Candidatos` and the candidate's full name are plain text, not activatable
- **AND** navigating to the candidate list by URL is still refused by the route guard

#### Scenario: Top-level pages have no trail

- **WHEN** the user opens the Dashboard, the candidate list, Búsqueda, the position list, the preset
  list, Catálogos, Usuarios, Roles, Importación or Auditoría
- **THEN** no breadcrumb trail is rendered

#### Scenario: Accessible structure

- **WHEN** assistive technology reads a sub-page
- **THEN** it finds a navigation landmark named `Ruta de navegación` containing an ordered list of
  segments
- **AND** the separators between segments are not announced

#### Scenario: Keyboard operation

- **WHEN** a keyboard user tabs through the trail on a candidate's edit page
- **THEN** `Candidatos` and the candidate's full name each receive visible focus and can be activated
- **AND** the current-page segment receives no focus

#### Scenario: Narrow viewport with a long name

- **WHEN** a candidate with a very long full name is opened at 390 pixels wide
- **THEN** the trail fits within the viewport without horizontal page scroll
- **AND** the full name remains available to the user
