# Data Tables Specification

## Purpose

Defines how record tables across the application look and how a user opens a record from them,
so that every list behaves the same way.

## Requirements

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

The selection cell, the actions cell and the «CV» cell SHALL be excluded as a whole, so a click
next to one of their controls does nothing, and they SHALL NOT look clickable.

These tables SHALL NOT offer a separate «Abrir», «Ver», «Detalle» or «Editar» button that only
opens the same destination. The CV column's «Ver», which shows the candidate's CV on the same page
without navigating, is not such a button.

#### Scenario: Row is clicked

- **WHEN** a user clicks a candidate list row outside its links and controls
- **THEN** the candidate page opens

#### Scenario: Control inside the row is used

- **WHEN** a user clicks a row's e-mail link, dropdown, «Eliminar», «Quitar de la posición», «Ver»
  or another button
- **THEN** that control acts and the page does not navigate

#### Scenario: Selection cell is clicked

- **WHEN** a manager clicks the selection checkbox, or the empty space of its cell, in the candidate list
- **THEN** only the selection changes and the page does not navigate

#### Scenario: Near-miss beside a row's actions or CV buttons

- **WHEN** a user clicks the empty space of a row's actions cell or «CV» cell
- **THEN** nothing happens and the page does not navigate

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

Wherever a phone is shown, a leading Spanish country code (`+34`, `0034`, `(+34)` or `(34)`) SHALL
be left out when what follows is a nine-digit Spanish number, keeping the rest as stored. Any other
number SHALL be shown as stored. This is a display rule only: the stored value, the edit form, the
text search and the export keep the number as entered.

#### Scenario: Phone is shown

- **WHEN** a candidate with a phone and an e-mail appears in the candidate list or on the candidate page
- **THEN** the phone is plain text and the e-mail is a mail link

#### Scenario: Spanish prefix is left out

- **WHEN** a candidate whose phone is stored as `+34 600 111 222` appears in the candidate list, the
  search results, a position's candidates or the candidate page header
- **THEN** the phone is shown as `600 111 222`

#### Scenario: Other numbers are shown as stored

- **WHEN** a candidate's phone is stored as `+33 6 12 34 56 78` or `+34 600`
- **THEN** it is shown exactly as stored

### Requirement: CV preview from a table row

The candidate list, the advanced search results, a position's candidates and a position's matches
SHALL end with a «CV» column for actors holding the document download permission, and SHALL NOT
render that column for anyone else. A row's CV cell SHALL hold, from left to right:

- a download button when the API reports that the candidate's primary CV can be downloaded (clean,
  with its file, in any allowed format), which downloads that CV through the permission-checked,
  audited document download without navigating;
- a «Ver» button when the API reports that the primary CV can be previewed.

A cell with neither SHALL be empty, with no disabled button. No other CV indicator SHALL be shown in
these tables. When the CV can no longer be downloaded at the moment the download is used, the user
SHALL be told so and nothing SHALL be downloaded.

Both buttons SHALL be icons with an accessible name and a matching tooltip: a disk for «Descargar
el CV de …»; an eye for «Ver», crossed out for «Ocultar». The «Ver»/«Ocultar» icon SHALL carry an
arrow towards where the CV opens: right for «Ver» and left for «Ocultar» beside the table, down for
«Ver» and up for «Ocultar» under the row.

«Ver» SHALL show the candidate's CV on the same page without navigating or changing the URL:

- beside the table, in a panel that stays in view while the page scrolls and pushes the table
  aside rather than covering it, when the page's content area is at least 1360 CSS pixels wide;
- otherwise, in a row inserted directly below the candidate's row, whose content fits the visible
  width of the table's scrolling container.

At most one CV SHALL be open on a page, across every table on it. Opening a CV SHALL close any
other. While a row's CV is open:

- its button SHALL be «Ocultar» and expose the expanded state;
- the row SHALL be visibly highlighted, distinctly from hover;
- the other rows' buttons SHALL be «Ver».

The buttons' accessible names SHALL name the candidate («Ver el CV de …», «Ocultar el CV de …»). A
candidate listed in two tables on the same page SHALL toggle only in the table whose button was
used.

The CV SHALL be headed «CV de …» with a link to the candidate page and an «Ocultar» control, and
SHALL show the same document picker, availability messages, viewer and download fallback as the
candidate page's CV preview. It SHALL always attempt to display the PDF inline and SHALL offer
«Descargar» only when the browser cannot render it.

The CV SHALL close:

- on «Ocultar», in its row or its panel;
- when another CV is opened;
- when its row leaves the table through paging, sorting, filtering, a new search, removal from the
  position or a state change that hides it;
- when the page is left.

Closing SHALL return focus to the row's button when focus was inside the CV.

When the content area crosses the 1360-pixel threshold while a CV is open, the CV SHALL stay open
for the same row and move to the other placement. Its content SHALL NOT be requested again. After
moving into the table, the row SHALL be scrolled into view, and focus that was inside the CV SHALL
move to the row's button.

Any opening animation SHALL be suppressed when the user prefers reduced motion.

#### Scenario: Column for a permitted actor

- **WHEN** an actor holding the document download permission views any of the four tables
- **THEN** its last column is «CV»; a row whose primary CV is a clean PDF shows the download and
  «Ver» buttons, a row whose primary CV is clean in another format shows only the download button,
  and a row whose primary CV is pending, refused or missing shows an empty cell

#### Scenario: CV downloaded from a row

- **WHEN** a user activates «Descargar el CV de …» on a row whose primary CV is a clean `.docx`
- **THEN** that file is downloaded through the audited document download and the page does not
  navigate

#### Scenario: Download no longer available

- **WHEN** a row's primary CV was removed after the table loaded and the user activates its
  download button
- **THEN** the user is told the CV can no longer be downloaded and nothing is downloaded

#### Scenario: Arrow follows the placement

- **WHEN** a user views a row with a previewable CV beside the table and then under the row
- **THEN** «Ver» shows an arrow pointing right and then down, and «Ocultar» an arrow pointing left
  and then up

#### Scenario: Column without the download permission

- **WHEN** an actor without the document download permission views any of the four tables
- **THEN** no «CV» column and no «Ver» button are rendered

#### Scenario: CV opened on a wide screen

- **WHEN** a user activates «Ver» on a row at 1920×1080
- **THEN** the CV is shown in a panel to the right of the table, headed with the candidate's name
- **AND** the row is highlighted, its button reads «Ocultar» with the expanded state, the table
  stays visible and usable, and the URL is unchanged

#### Scenario: Another CV is opened

- **WHEN** a CV is open and the user activates «Ver» on another row
- **THEN** the panel shows the other candidate's CV, the first row returns to «Ver» without
  highlight, and the page shows a single CV viewer

#### Scenario: CV is hidden

- **WHEN** the user activates «Ocultar» on the open row or on its panel
- **THEN** the CV closes, the page returns to its single-column layout at the default width, and
  focus is on the row's «Ver»

#### Scenario: CV opened on a narrower screen

- **WHEN** a user activates «Ver» at 1366×768
- **THEN** the CV is shown in a row directly below that candidate's row and nothing is added at the
  bottom of the page

#### Scenario: CV opened at phone width

- **WHEN** a CV is open at 390 pixels wide
- **THEN** the page does not scroll horizontally and the CV fits the screen width even when the
  table scrolls sideways

#### Scenario: Window crosses the threshold

- **WHEN** a CV is open beside the table and the window is narrowed below the threshold
- **THEN** the CV is shown below its row, the row is in view, its button still reads «Ocultar», and
  no second content request is made
- **AND** widening the window again moves the CV back beside the table

#### Scenario: Two tables on one page

- **WHEN** a CV is open in «Candidatos de la posición» and the user activates «Ver» in «Candidatos
  que encajan»
- **THEN** the first CV closes and only the second row reads «Ocultar»

#### Scenario: Same candidate in both tables

- **WHEN** a candidate appears in both position tables and the user activates «Ver» in one of them
- **THEN** only that table's row is highlighted and reads «Ocultar»

#### Scenario: Row leaves the table

- **WHEN** a CV is open and the user changes page, sort order or filters, runs a new search, or
  removes that candidate from the position
- **THEN** the CV closes

#### Scenario: Previewable state changed after loading

- **WHEN** a row's primary CV was replaced or removed after the table loaded and the user activates
  «Ver»
- **THEN** the CV area shows the document's availability message, never an empty area, and no
  content is fetched for a document that is not clean

#### Scenario: Browser cannot render the PDF

- **WHEN** a CV is opened in a browser without an inline PDF viewer
- **THEN** the CV area shows the fallback message with «Descargar»

### Requirement: Candidate tables show availability

The candidate list, the advanced search results and «Candidatos que encajan» SHALL show a
«Disponibilidad» column where a candidate status used to be:

- for a known check, its value and the time elapsed since the check («No disponible · hace 6
  meses»);
- otherwise «Sin comprobar».

No candidate table SHALL show a candidate status. «Candidatos de la posición» SHALL keep showing the
stage instead. In the candidate list, the column SHALL be sortable by check date, and the filter bar
SHALL offer a single-value «Disponibilidad» filter, kept in the page URL like the other list
filters, with a removable chip when set. The CSV export of search results SHALL carry
`disponibilidad` (the Spanish value label) and `comprobado_el` (`yyyy-MM-dd`, blank when unchecked)
instead of `estado`.

#### Scenario: Availability column

- **WHEN** a reader views the candidate list with one candidate checked `unavailable` six months ago
  and one never checked
- **THEN** the first row reads «No disponible · hace 6 meses», the second reads «Sin comprobar», and
  no column shows a candidate status

#### Scenario: List is filtered by availability

- **WHEN** a reader selects «Disponible» in the list's «Disponibilidad» filter
- **THEN** only available candidates are listed, the URL keeps the filter, and a chip removes it

#### Scenario: List is sorted by check date

- **WHEN** a reader sorts the list by «Disponibilidad»
- **THEN** rows are ordered by check date with unchecked candidates last, and the column header
  reports the sort direction

#### Scenario: Export carries availability

- **WHEN** a reader exports search results
- **THEN** the file has `disponibilidad` and `comprobado_el` columns and no `estado` column
