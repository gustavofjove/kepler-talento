## ADDED Requirements

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
