## MODIFIED Requirements

### Requirement: Primary navigation entries and permission visibility

The primary navigation SHALL offer `Inicio`, `Candidatos`, `Búsqueda`, `Catálogos`,
`Presets`, `Usuarios`, `Roles`, `Importación` and `Auditoría`, each pointing at its existing route.
`Inicio` SHALL point at the application home route and SHALL replace the former `Dashboard` label;
no entry SHALL be labelled `Dashboard`.
An entry SHALL be rendered only when the signed-in profile holds the permission that governs its
destination: `Candidatos` and `Búsqueda` require `candidates.read`, `Catálogos` requires
`catalogs.manage`, `Presets` requires `presets.manage`, `Usuarios` requires `users.manage`,
`Roles` requires `roles.manage`, `Importación` requires `candidates.import`, `Auditoría` requires
`audit.read`. `Inicio` SHALL always be present for an authenticated user.

Hiding an entry is a convenience only. The system SHALL continue to enforce access to
every destination through the route guard and through the API or database authorization that
protects its data, so that a user who reaches a hidden route by URL is refused by those layers and
not by the absence of a link.

All entry labels SHALL be rendered in Spanish with correct accents.

#### Scenario: A user with every permission sees every destination

- **WHEN** a profile holding `candidates.read`, `catalogs.manage`, `presets.manage`,
  `users.manage`, `roles.manage`, `candidates.import` and `audit.read` opens the application
- **THEN** all nine destinations are reachable from the primary navigation

#### Scenario: The home entry is labelled Inicio

- **WHEN** any authenticated user views the primary navigation
- **THEN** the entry leading to the home route reads `Inicio`
- **AND** no entry reads `Dashboard`

#### Scenario: A user without candidate permission does not see candidate entries

- **WHEN** a profile without `candidates.read` opens the application
- **THEN** `Candidatos` and `Búsqueda` are absent from the navigation
- **AND** `Inicio` is still present

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
- **THEN** the top level offers `Inicio`, `Candidatos`, `Búsqueda` and `Admin`
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
- **THEN** `Inicio`, `Candidatos`, `Búsqueda` and `Admin` are laid out horizontally in
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

Within that panel, `Inicio`, `Candidatos` and `Búsqueda` SHALL be full-width vertical
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
- **THEN** a full-width vertical panel opens below the header containing `Inicio`,
  `Candidatos`, `Búsqueda` and the `Admin` accordion

#### Scenario: Expanding the administration accordion

- **WHEN** the user activates `Admin` inside the vertical panel
- **THEN** its visible children appear below it, indented
- **AND** the content that follows is displaced downward rather than covered

### Requirement: Breadcrumb trail on sub-pages

Every page that sits below a list SHALL render a breadcrumb trail above its main heading. The trail
SHALL show where the page sits in the application and SHALL let the user return to its parents in one
activation. The trails SHALL be:

| Page               | Trail                                    |
| ------------------ | ---------------------------------------- |
| Candidate detail   | `Candidatos` › candidate full name       |
| Candidate creation | `Candidatos` › `Nuevo candidato`         |
| Position detail    | `Posiciones` › position title            |
| Position creation  | `Posiciones` › `Nueva posición`          |
| Position edit      | `Posiciones` › position title › `Editar` |
| Preset creation    | `Admin` › `Presets` › `Nuevo preset`     |
| Preset edit        | `Admin` › `Presets` › preset name        |

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

Top-level destinations SHALL NOT render a breadcrumb. These are Inicio, the candidate list,
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

- **WHEN** a profile holding `candidates.read` opens a candidate's former edit address
- **THEN** the candidate page is shown and the trail reads `Candidatos` › the candidate's full name,
  with no `Editar` segment

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

- **WHEN** a candidate's page is still loading the candidate
- **THEN** the trail shows `Candidatos`, still activatable, and not the name

#### Scenario: A failed load still offers the way back

- **WHEN** a candidate's detail page fails to load the candidate or the candidate is not found
- **THEN** the trail still offers `Candidatos` leading to the candidate list

#### Scenario: A segment the viewer may not open is plain text

- **WHEN** a profile holding `candidates.create` but not `candidates.read` opens the candidate creation
  page
- **THEN** `Candidatos` is plain text, not activatable
- **AND** navigating to the candidate list by URL is still refused by the route guard

#### Scenario: An unsaved panel edit does not change the trail

- **WHEN** the user changes the first name in the Datos principales panel of a candidate page
  without saving
- **THEN** the trail still shows the stored full name

#### Scenario: Top-level pages have no trail

- **WHEN** the user opens Inicio, the candidate list, Búsqueda, the position list, the preset
  list, Catálogos, Usuarios, Roles, Importación or Auditoría
- **THEN** no breadcrumb trail is rendered

#### Scenario: Accessible structure

- **WHEN** assistive technology reads a sub-page
- **THEN** it finds a navigation landmark named `Ruta de navegación` containing an ordered list of
  segments
- **AND** the separators between segments are not announced

#### Scenario: Keyboard operation

- **WHEN** a keyboard user tabs through the trail on a position's edit page
- **THEN** `Posiciones` and the position title each receive visible focus and can be activated
- **AND** the current-page segment receives no focus

#### Scenario: Narrow viewport with a long name

- **WHEN** a candidate with a very long full name is opened at 390 pixels wide
- **THEN** the trail fits within the viewport without horizontal page scroll
- **AND** the full name remains available to the user
