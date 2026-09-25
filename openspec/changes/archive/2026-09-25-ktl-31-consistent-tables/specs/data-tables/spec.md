## Purpose

Defines how record tables across the application look and how a user opens a record from them,
so that every list behaves the same way.

## ADDED Requirements

### Requirement: Rows open their record

The Candidatos, Posiciones and Presets lists, the advanced search results and both position
candidate tables SHALL open a record when a row is clicked:

- a Candidatos or search-result row opens the candidate page;
- a Posiciones row opens the position page;
- a Presets row opens the preset's edit page;
- a position candidate row opens the candidate, and a candidate's position row opens the
  position.

A click on a link, button, input, dropdown or label inside the row, a click inside a cell marked
as excluded, or a click made while text is selected SHALL keep its own behaviour and SHALL NOT
navigate. A Ctrl/⌘-click or middle-click SHALL open the destination in a new tab instead.

These tables SHALL NOT offer a separate «Abrir», «Ver», «Detalle» or «Editar» button that only
opens the same destination.

#### Scenario: Row is clicked

- **WHEN** a user clicks a candidate list row outside its links and controls
- **THEN** the candidate page opens

#### Scenario: Control inside the row is used

- **WHEN** a user clicks a row's e-mail link, dropdown, «Eliminar», «Quitar de la posición» or another button
- **THEN** that control acts and the page does not navigate

#### Scenario: Selection cell is clicked

- **WHEN** a manager clicks the selection checkbox, or the empty space of its cell, in the candidate list
- **THEN** only the selection changes and the page does not navigate

#### Scenario: Row is opened in a new tab

- **WHEN** a user Ctrl-clicks or middle-clicks a positions list row
- **THEN** the position opens in a new tab and the current page stays

### Requirement: Every clickable row keeps a keyboard link

Each clickable row SHALL contain a real link to the same destination on the record's name or
title, styled as plain text with a visible focus ring. The row itself SHALL NOT be a tab stop, and
the row click SHALL NOT be the only way to reach the destination.

#### Scenario: Keyboard user opens a record

- **WHEN** a keyboard user tabs to a preset's name in the presets list and presses Enter
- **THEN** the preset's edit page opens

### Requirement: Tables without a record page have no row navigation

The Catálogos and Usuarios tables SHALL NOT navigate on row click, because their records have no
page of their own and are edited in place. Their existing in-row controls SHALL keep working.

#### Scenario: User row is clicked

- **WHEN** an administrator clicks a user row outside the role dropdown and the activation button
- **THEN** nothing navigates

#### Scenario: Catalog row is clicked without leaving the page

- **WHEN** a user clicks a catalog value row outside its controls
- **THEN** the page does not navigate

### Requirement: Catalog rows open their inline editor

A click on a Catálogos value row outside its controls SHALL start that row's inline edit, as
«Editar» did, while no other row is being edited. The value's name SHALL be a keyboard-reachable
button to the same action, and the table SHALL NOT offer a separate «Editar» button. While a row
is being edited, clicks on other rows SHALL do nothing. The reorder actions SHALL be up and down
arrow icon buttons whose accessible names identify the action and the value.

#### Scenario: Catalog row is clicked

- **WHEN** a user clicks a catalog value row outside its controls and no row is being edited
- **THEN** that row switches to its inline editor and nothing navigates

#### Scenario: Catalog row is clicked during another edit

- **WHEN** a user clicks a catalog value row while another row is being edited
- **THEN** nothing changes

#### Scenario: Catalog value is reordered

- **WHEN** a user activates a row's up or down arrow
- **THEN** the value moves one place and the inline editor does not open

### Requirement: Consistent table styling

Every table named in this capability SHALL:

- centre its cell content vertically;
- render in-row dropdowns and buttons at the compact control height;
- keep wide content scrolling inside its own container, without horizontal page scroll at
  390 pixels wide.

Dropdowns and buttons outside these tables SHALL keep their current size.

#### Scenario: Mixed row content is aligned

- **WHEN** a row contains text, a badge, a dropdown and buttons
- **THEN** all of them are vertically centred on one line and the dropdown is no taller than the buttons

#### Scenario: Narrow viewport

- **WHEN** any of these tables is shown at 390 pixels wide
- **THEN** the page does not scroll horizontally

### Requirement: Phones are plain text

Candidate phone numbers SHALL be rendered as plain text, not as `tel:` links, in every table and
on the candidate page header. E-mail addresses SHALL remain `mailto:` links.

#### Scenario: Phone is shown

- **WHEN** a candidate with a phone and an e-mail appears in the candidate list or on the candidate page
- **THEN** the phone is plain text and the e-mail is a mail link
