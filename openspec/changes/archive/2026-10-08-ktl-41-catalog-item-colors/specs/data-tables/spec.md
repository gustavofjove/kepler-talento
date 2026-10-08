## ADDED Requirements

### Requirement: Catalog values are edited from their row actions

A Catálogos value row SHALL NOT react to a click outside its controls: it SHALL neither navigate nor
start the inline edit, and the value's name SHALL be plain text. The «Acciones» column SHALL offer,
as icon buttons with no visible text, an edit button that starts that row's inline edit, the up and
down reorder arrows, and the deactivate button (or, for an inactive value, the activate button).
Each icon button SHALL have an accessible name that identifies the action and the value (for
example «Editar Inglés», «Desactivar Inglés», «Activar Inglés») and a tooltip naming the action.
Deactivation SHALL still ask for confirmation; activation SHALL NOT.

While a row is being edited, the other rows' actions and «Nuevo» SHALL be disabled, not hidden, so
the table does not reflow, until the edit is saved or cancelled.

The form that adds a value SHALL be hidden by default. A «Nuevo» button on the same line as the
family selector, to its right, SHALL show it and move focus to its name field; while the form is
shown, «Nuevo» and the row edit buttons SHALL be disabled. The form SHALL lay out the name in one
half of its line and, in the other half, the colour (for families that have colours) followed by
the code. «Cancelar» SHALL hide the form and discard what was typed; a successful «Añadir» SHALL
add the value, hide the form and reset it.

#### Scenario: Catalog row is clicked

- **WHEN** a user clicks a catalog value row outside its controls
- **THEN** nothing navigates and no inline edit starts

#### Scenario: Catalog value is edited from its pencil

- **WHEN** a user activates a row's edit button, by pointer or keyboard, while no row is being
  edited
- **THEN** that row switches to its inline editor

#### Scenario: Other rows are locked during an edit

- **WHEN** one row is being edited
- **THEN** every action button of the other rows, and «Nuevo», is disabled

#### Scenario: Catalog value is deactivated from its icon

- **WHEN** a user activates a row's deactivate button and confirms
- **THEN** the value becomes inactive and its row offers the activate button instead

#### Scenario: Catalog value is reordered

- **WHEN** a user activates a row's up or down arrow
- **THEN** the value moves one place and the inline editor does not open

#### Scenario: Add form opens from «Nuevo»

- **WHEN** an administrator opens Catálogos
- **THEN** the add form is hidden, and «Nuevo» to the right of the family selector shows it with
  focus on its name field

#### Scenario: Add form is cancelled

- **WHEN** an administrator types a name in the add form and presses «Cancelar»
- **THEN** the form is hidden, and opening it again shows it empty

#### Scenario: Value is added

- **WHEN** an administrator adds a value through the form
- **THEN** the value is created and the form is hidden and reset

## REMOVED Requirements

### Requirement: Catalog rows open their inline editor

**Reason**: KTL-41 moves catalog editing to an explicit edit icon in «Acciones», because a click
anywhere on the row reads like the navigation of tables that have a detail page, and the row now
also holds the colour control.

**Migration**: Use the row's edit button (`catalog-edit`, accessible name «Editar <value>») instead
of clicking the row or the value's name. The reorder arrows are unchanged. Deactivation and
activation are icon buttons (`catalog-toggle-active`) with the accessible names «Desactivar
<value>» and «Activar <value>». The add form opens from «Nuevo» (`catalog-new`).
