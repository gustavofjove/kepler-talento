## MODIFIED Requirements

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
