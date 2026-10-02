## MODIFIED Requirements

### Requirement: Detail page keeps viewing and status actions

The candidate page SHALL keep document download and the CV preview for users holding the document download permission, whether or not a panel is in edit mode. It SHALL keep the activate/deactivate action in the page header for users holding the candidate update permission. Activation and deactivation SHALL still require explicit confirmation. The page SHALL NOT offer a link to a separate edit page. A logically removed candidate's page heading SHALL show «(Inactivo)» after the name, including after a status change without reloading; an active candidate's heading SHALL show the name without this suffix. The breadcrumb SHALL keep the plain name.

Every reader SHALL see one relative «Actualizado {elapsed}» line beneath the status action, or in its place when the action is unavailable. A focusable, accessibly named control beside the line SHALL reveal both exact creation and update times, formatted to the minute in the viewer's time zone and available by keyboard and screen reader. The header SHALL give the name and contact line more vertical space and SHALL fit without horizontal scrolling at 390 pixels wide.

#### Scenario: Document is downloaded from the detail page

- **WHEN** a user holding the document download permission opens a candidate with a clean document
- **THEN** the document can be downloaded and previewed from the candidate page

#### Scenario: Editor reaches the edit page

- **WHEN** a user holding the candidate update permission opens the candidate page
- **THEN** editing is reached through each panel's «Editar», no link to a separate edit page is shown, and the activate/deactivate action is offered

#### Scenario: Reader sees no status actions

- **WHEN** a user without the candidate update permission opens the candidate page
- **THEN** neither «Editar» nor the activate/deactivate action is offered, and the relative update line remains visible

#### Scenario: Removed candidate changes state

- **WHEN** an editor deactivates and then reactivates a candidate
- **THEN** «(Inactivo)» appears beside the name after deactivation and disappears after reactivation without a reload, while the breadcrumb stays the plain name

#### Scenario: Exact record times are accessible

- **WHEN** a keyboard or screen-reader user activates the timestamp information control
- **THEN** both labelled exact times are exposed in the viewer's time zone without relying on hover or a title attribute

### Requirement: Competencias panel and stacked sections

The candidate page SHALL present skills, languages, programs and tags together in one panel headed `Competencias`. Each family SHALL be one row in the shared family row layout, in the order Habilidades, Idiomas, Programas, Etiquetas. When catalogs cannot be loaded, the catalog status notice SHALL appear once for the panel while it is in edit mode.

Panels SHALL be stacked vertically in the order Datos principales, Disponibilidad, Competencias, Educación, Experiencia, Notas, Documentos, and every panel SHALL span the full width of the sections column, with one exception. Datos principales and Disponibilidad SHALL share the first row, side by side in two equal columns, while the sections column is wide enough for two. They SHALL stack, Datos principales first, when the column is too narrow for two, and while Datos principales is in edit mode, so that its editor takes the full width. A panel in edit mode SHALL keep its position in that order. The candidate page SHALL have no Auditoría panel.

The sections column SHALL be the full content width, unless the CV preview is shown beside it as the CV preview requirement describes.

When Competencias is read-only, its rows SHALL show chips only. Each family without entries SHALL show its empty-state text on the row's label line, where its chips would be.

#### Scenario: Edit page layout

- **WHEN** an editor puts Competencias in edit mode
- **THEN** every panel keeps its position in the stacked order, and Competencias shows the rows Habilidades, Idiomas, Programas and Etiquetas in that order with their add controls

#### Scenario: Detail page layout

- **WHEN** any reader opens a candidate page on a wide screen
- **THEN** Datos principales and Disponibilidad are shown side by side in the first row, Competencias, Educación, Experiencia, Notas and Documentos follow across the sections column, one below another, and Competencias shows the rows Habilidades, Idiomas, Programas and Etiquetas in that order, read-only

#### Scenario: Main data editor takes the full row

- **WHEN** an editor puts Datos principales in edit mode
- **THEN** its editor spans the sections column and Disponibilidad is shown below it, and both return to one row when the panel closes

#### Scenario: Main data and audit on a narrow column

- **WHEN** the candidate page is shown at 390 pixels wide, or with the CV preview beside a sections column too narrow for two
- **THEN** Datos principales and Disponibilidad are stacked, Datos principales first

#### Scenario: Family without entries on the detail page

- **WHEN** a candidate has no programs and a reader opens the candidate page
- **THEN** the Programas row shows its empty-state text on its label line, as tall as a row with chips

#### Scenario: Catalogs are unavailable on the edit page

- **WHEN** the catalogs fail to load and an editor puts Competencias in edit mode
- **THEN** a single catalog notice is shown in the Competencias panel, every family's add control is disabled, and «Guardar» is disabled

### Requirement: One candidate page with per-panel edit mode

An existing candidate SHALL be viewed and edited on a single candidate page. The page SHALL show the panels Datos principales, Disponibilidad, Competencias, Educación, Experiencia, Notas and Documentos read-only by default.

Datos principales, Competencias, Educación, Experiencia, Notas and Documentos SHALL each offer an «Editar» action. Activating it SHALL replace that panel, in its place on the page, with its editor. Disponibilidad SHALL NOT offer «Editar» and SHALL act immediately. The other panels SHALL stay read-only while one panel is in edit mode.

The address of a former candidate edit page SHALL lead to the candidate page of the same candidate for any user holding the candidate read permission.

#### Scenario: Editor opens a candidate

- **WHEN** a user holding the candidate update and document upload permissions opens a candidate page
- **THEN** every panel is read-only and Datos principales, Competencias, Educación, Experiencia, Notas and Documentos each offer «Editar», while Disponibilidad does not

#### Scenario: Panel enters edit mode in place

- **WHEN** an editor activates «Editar» on Educación
- **THEN** Educación shows its editor in the same position on the page, and every other panel stays read-only

#### Scenario: Former edit address

- **WHEN** a user holding the candidate read permission opens a candidate's former edit address
- **THEN** the application shows that candidate's page, and the former address is replaced in the browser history

#### Scenario: Section without entries

- **WHEN** a candidate has no entries in a section
- **THEN** the panel shows that section's empty state rather than a blank area

### Requirement: Candidate values are shown in readable form

The candidate page SHALL show stored values in the active language's readable form, not in their storage format:

- The relative «Actualizado» line SHALL use the active language. The exact creation and update times disclosed from it SHALL show a date and time to the minute in the viewer's time zone, never an ISO timestamp.
- Datos principales' reception and review dates are calendar days. They SHALL show a date without a time (for example «1 sept 2026»), and SHALL show the stored day in every time zone. A stored value that is not a valid calendar day SHALL be shown as stored, and an empty one as «Pendiente».
- In read mode, Recepción SHALL be hidden when its stored day equals the creation day in the viewer's time zone. It SHALL be shown when empty or when the days differ. The core record form SHALL always show the reception field.
- The review date SHALL be labelled «Revisión LOPD» in read mode and «Fecha de revisión LOPD» in the form. The form SHALL explain that the date is for deciding whether candidate data may still be retained. In read mode a review date before the viewer's current local day SHALL have a text «(vencida)» marker; today through 30 calendar days ahead inclusive SHALL have a text «(próxima)» marker. Later or empty dates SHALL have no marker.
- The location SHALL show as «Location (Province)». When only one of the two is stored, it SHALL show that one alone, with no empty parentheses.
- Datos principales SHALL NOT show a candidate status or a free-text availability. Availability is shown only in the availability block, and the core record form SHALL NOT edit it.

These are display rules only: the stored values are unchanged.

#### Scenario: Audit times are readable

- **WHEN** a reader opens a candidate created at 2026-09-30T09:49:00Z from Spain in summer time
- **THEN** the timestamp information control reveals «Creado 30 sept 2026, 11:49» and the similarly formatted update time

#### Scenario: Calendar day in a western time zone

- **WHEN** a candidate received on 2026-09-01 is viewed from a time zone behind UTC
- **THEN** the reception date, when shown, reads «1 sept 2026», not the day before

#### Scenario: Review urgency boundaries

- **WHEN** a reader views a review date of yesterday, today, 30 days ahead, or 31 days ahead in their local calendar
- **THEN** the first is marked «(vencida)», the next two «(próxima)», and the last has no marker

#### Scenario: Reception matches creation day

- **WHEN** a candidate's reception day equals the creation day in the viewer's time zone
- **THEN** Recepción is absent in read mode but remains in the core record form

#### Scenario: Reception differs or is empty

- **WHEN** a candidate's reception day differs from the local creation day, or is empty
- **THEN** Recepción is shown in read mode with its date or «Pendiente»

#### Scenario: Location with and without province

- **WHEN** a candidate has location Alcobendas and province Madrid, and another has only the province Madrid
- **THEN** the first shows «Alcobendas (Madrid)» and the second shows «Madrid»

#### Scenario: Core record has no status or availability

- **WHEN** an editor views Datos principales and then opens its form
- **THEN** neither shows a status or an availability field, and the create page does not ask for one either

### Requirement: Availability block on the candidate page

The candidate page SHALL show the candidate's availability in a panel headed «Disponibilidad» in the first summary row beside Datos principales, never in the page header:

- **Line:** «Sin comprobar», «Disponible», «No disponible» or «No disponible hasta {date}».
- **When known, check metadata:** «Comprobado el {date} por {name} · {time elapsed}». The time elapsed is in the active language («hoy», «hace 7 meses»). Dates are calendar days shown in the stored day in every time zone.

The value SHALL NOT repeat «Disponibilidad» beneath the panel heading. When there is room, the check metadata SHALL sit to the right of the value on the same row; it SHALL wrap without horizontal scrolling when there is not. Only the value SHALL be bold. The summary row, hints and actions SHALL have readable vertical separation.

For an actor holding `candidates.update` on an active candidate, the block SHALL offer:

- «Sigue igual», only when the value is known and any until date is today or later. It records the stored value and until date again with today's date.
- «Cambiar…» («Registrar comprobación» when the value is `unknown`). It opens an inline form with:
  - a labelled radio group for the value;
  - a «Hasta (opcional)» date, shown only for «No disponible»;
  - a «Comprobado el» date defaulting to today, hidden for «Sin comprobar»;
  - «Registrar» and «Cancelar». The date field and these actions SHALL share one row when the panel has enough width and SHALL wrap without horizontal scrolling when it does not.
- Right after each successful check, an inline «Deshacer». It records the previous value, until date and check date again. It stays until another check is made or the page is left, and is never removed on a timer.

When the until date has passed, the line SHALL be marked «(vencido)», a hint SHALL ask for a new check, and «Sigue igual» SHALL NOT be offered.

Without `candidates.update`, or on a logically removed candidate, the block SHALL be read-only.

The block SHALL act immediately. It SHALL NOT take part in per-panel edit mode, SHALL NOT count towards unsaved-change protection, and SHALL stay usable while a panel is being edited. After a check, the page SHALL adopt the returned candidate and version, so a later save of another panel is not refused because of that check. The result of each check and undo SHALL be announced through a polite live region, and focus SHALL return to «Cambiar…» after saving or cancelling the form.

#### Scenario: Unchecked candidate

- **WHEN** an editor opens a candidate whose availability is `unknown`
- **THEN** the block reads «Sin comprobar» without a repeated «Disponibilidad» label and offers «Registrar comprobación» but not «Sigue igual»

#### Scenario: Reconfirming

- **WHEN** an editor activates «Sigue igual» on a candidate checked `available` on 2026-03-12
- **THEN** the block shows today's date, the editor's name and «hoy», the value is unchanged, and «Deshacer» is offered

#### Scenario: Check metadata beside the value

- **WHEN** a reader opens a checked candidate on a wide sections column
- **THEN** the bold availability value is on the left and regular-weight «Comprobado el …» metadata is to its right; on a narrow column they remain readable without horizontal scrolling

#### Scenario: Undoing a reconfirm

- **WHEN** the editor then activates «Deshacer»
- **THEN** the block again shows 2026-03-12 and the previous value, and the confirmation is announced

#### Scenario: Lapsed until date

- **WHEN** a reader opens a candidate checked «No disponible» until a date that has passed
- **THEN** the line is marked «(vencido)», the hint asks for a new check, and «Sigue igual» is not offered

#### Scenario: Changing the value

- **WHEN** an editor opens «Cambiar…», chooses «No disponible», enters an until date and saves
- **THEN** the block shows «No disponible hasta» that date with today's check, and focus returns to «Cambiar…»

#### Scenario: Registering in the inline form

- **WHEN** an editor selects «Disponible» in the inline form on a wide summary panel
- **THEN** «Comprobado el», «Registrar» and «Cancelar» share one row; on a narrow panel they wrap and remain reachable without horizontal scrolling

#### Scenario: Check while another panel is edited

- **WHEN** Datos principales is in edit mode with unsaved changes and the editor activates «Sigue igual», then saves Datos principales
- **THEN** both writes succeed, and no unsaved-change prompt or concurrency conflict appears

#### Scenario: Reader sees no availability actions

- **WHEN** an actor without `candidates.update` opens a candidate
- **THEN** the availability lines are shown and no «Sigue igual», «Cambiar…» or «Deshacer» is offered
