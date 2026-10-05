# Candidate Profile Pages Specification

## Purpose

Defines how the application presents a candidate to its users: a single candidate page that shows
the profile and its documents read-only, where each panel is edited in place and saved on its own
(KTL-29), plus the create page for new candidates.

## Requirements

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

### Requirement: Candidate pages are localized and accessible

All copy on the candidate page and the create page SHALL be Spanish and come from the localization
catalogue. Each page SHALL have a single top-level heading and a heading per panel. Within the
Competencias panel, each family SHALL be identified by its visible row label, which names its
picker, rather than by its own heading.

«Editar», «Guardar», «Cancelar» and «Hecho» SHALL have accessible names that include the panel
they act on. On entering edit mode, focus SHALL move to the panel's first control. On saving,
cancelling or finishing, focus SHALL return to that panel's «Editar».

Form controls SHALL keep programmatic labels, stable names and test identifiers. Save
confirmations SHALL be announced to assistive technology. Both pages SHALL be operable by keyboard,
and SHALL NOT scroll horizontally at 390 pixels wide, including while a panel is in edit mode.

#### Scenario: Save confirmation with a screen reader

- **WHEN** an editor saves a form panel
- **THEN** the confirmation is exposed through a live status region

#### Scenario: Edit control names its panel

- **WHEN** an assistive technology user reaches the «Editar» of Educación
- **THEN** it is announced with a name that includes Educación

#### Scenario: Focus follows edit mode

- **WHEN** a keyboard user activates «Editar» on Datos principales and later «Cancelar»
- **THEN** focus moves to the first field of Datos principales and then back to its «Editar»

#### Scenario: Families are named by their row labels

- **WHEN** an assistive technology user navigates the Competencias panel
- **THEN** the panel has its own heading and each family's picker is announced with its row label

#### Scenario: Narrow viewport

- **WHEN** the candidate page is shown at 390 pixels wide with a panel in edit mode
- **THEN** every section and control remains reachable and the page does not scroll horizontally

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

### Requirement: CV preview beside the candidate sections

The candidate page SHALL offer the CV preview to users holding the document download permission,
under the existing preview rules, whether or not a panel is in edit mode. It SHALL offer it only
when the candidate's primary CV can be previewed: a clean PDF whose binary exists, the same rule
that makes the candidate tables offer «Ver». A candidate without a primary CV, or whose primary CV
is of another format, still pending, refused, unscannable or a legacy record without a binary,
SHALL get no preview. Their documents stay available from the Documentos panel. When a pending
primary CV is reported clean while the page is open, the preview SHALL appear without a reload.
The new-candidate page SHALL NOT show a preview.

When the preview is shown and the page's content area is at least 1360 CSS pixels wide:

- The preview SHALL be a column to the right of the sections column.
- It SHALL stay visible below the application header while the sections scroll.
- The page's content area SHALL widen, up to 1920 CSS pixels.

When the content area is narrower, the preview SHALL follow the last section, one below another.

When the preview is not shown, the sections column SHALL take the full content width, and no empty
column SHALL remain.

Two-column field groups inside the sections column SHALL become one column when that column is too
narrow for two, regardless of the viewport width. Reading and keyboard order SHALL be the sections
first and then the preview, at every width.

#### Scenario: Preview beside the sections on a wide screen

- **WHEN** a user holding the document download permission opens the page of a candidate with a
  clean PDF at 1920×1080
- **THEN** the CV preview is shown to the right of Datos principales

#### Scenario: Preview on the edit page

- **WHEN** a user holding the candidate update and document download permissions puts Datos
  principales in edit mode on a candidate with a clean PDF at 1920×1080
- **THEN** the CV preview stays shown to the right of the Datos principales editor

#### Scenario: Preview stays in view

- **WHEN** the preview is shown beside the sections and the user scrolls down to Documentos
- **THEN** the preview remains fully visible below the application header

#### Scenario: Preview stacked on a narrower screen

- **WHEN** a user holding the document download permission opens the candidate page at 1366×768
- **THEN** the CV preview is shown below the last section

#### Scenario: No preview leaves one column

- **WHEN** a user without the document download permission opens the candidate page at 1920×1080
- **THEN** no preview content is requested, the sections take the full content width and no empty
  column is shown

#### Scenario: Primary CV cannot be previewed

- **WHEN** a user holding the document download permission opens the page of a candidate whose
  primary CV is a clean `.docx`, or who has only non-primary documents
- **THEN** no preview is shown, no document content is requested, the sections take the full
  content width, and the documents remain downloadable from Documentos

#### Scenario: Pending primary CV becomes previewable

- **WHEN** the candidate page is open for a candidate whose primary PDF is pending and the scan
  reports it clean
- **THEN** the CV preview appears without reloading the page

#### Scenario: Field groups follow the sections column

- **WHEN** the preview is shown beside the sections, the sections column is too narrow for two
  field columns, and Datos principales, Educación, Experiencia or Documentos is in edit mode
- **THEN** that panel's form shows its fields in one column

#### Scenario: New candidate page has no preview

- **WHEN** an editor opens the new-candidate page
- **THEN** no CV preview is shown

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

### Requirement: Form panels save only their own changes

Datos principales, Competencias, Educación and Experiencia SHALL keep the changes made in edit mode
as a draft. They SHALL NOT write anything until the user activates that panel's «Guardar».

«Guardar» SHALL write only that panel's data. It SHALL then return the panel to read-only with the
saved values, and announce a confirmation accessibly. «Cancelar» SHALL discard the draft, send no
write, and return the panel to read-only with the saved values.

For Competencias, «Guardar» SHALL write only the families (Habilidades, Idiomas, Programas,
Etiquetas) whose entries changed. When some families are saved and others are refused, the saved
families SHALL stay saved. The panel SHALL stay in edit mode, show the Spanish error on each refused
family, and a further «Guardar» SHALL write only the families still unsaved.

Validation that refuses an entry (duplicate value, missing required field, out-of-range year or
date, negative years) SHALL be reported in the panel without saving it.

A validation or save error of a candidate form SHALL be shown in the error style (the danger
colour, not the muted or empty-state style), exposed as an alert, and placed at the top of the form
it refers to, before its first field. This applies to the core record form on both pages, the
Educación and Experiencia add forms, and the document upload form. Errors of Notas SHALL be shown
at the top of the Notas panel, since they can come from adding, editing or retiring a note. A
Competencias family error SHALL stay on that family's row.

In edit mode, Educación and Experiencia SHALL show the form for a new entry after the existing
entries, set apart from them by a visible rule. The form SHALL be titled «Nueva formación» or
«Nueva experiencia», and that title SHALL be its accessible name.

#### Scenario: Staged changes are saved together

- **WHEN** an editor, with Educación in edit mode, adds one entry and removes another and then
  activates «Guardar»
- **THEN** no write is sent before «Guardar». After it, the candidate's education is saved as the
  resulting list, the panel shows it read-only, and a confirmation is announced.

#### Scenario: Cancel discards the draft

- **WHEN** an editor changes Experiencia in edit mode and activates «Cancelar»
- **THEN** no write is sent and the panel shows the saved experience read-only

#### Scenario: Core record is saved

- **WHEN** an editor changes the e-mail in Datos principales and activates «Guardar»
- **THEN** the change persists, the page header shows the new e-mail, and a confirmation is announced

#### Scenario: Core record without a name

- **WHEN** an editor clears the first name in Datos principales and activates «Guardar»
- **THEN** a Spanish validation message is shown in the error style at the top of the form, before
  its first field, and nothing is written

#### Scenario: Add form is set apart from the entries

- **WHEN** an editor puts Educación in edit mode on a candidate with education entries
- **THEN** the entries are listed first, then a rule, then a form titled «Nueva formación»

#### Scenario: Only changed families are written

- **WHEN** an editor, with Competencias in edit mode, adds a skill and changes a language's level
  and activates «Guardar»
- **THEN** the skills and the languages are written, and the programs and tags are not

#### Scenario: One family is refused

- **WHEN** Competencias saves changed skills and languages and the API refuses the languages
- **THEN** the skills stay saved, the panel stays in edit mode with the Spanish error on Idiomas,
  and a further «Guardar» writes only the languages

#### Scenario: Entry is added with the lowest level

- **WHEN** an editor adds a language in Competencias and the language level catalog's first active
  value is A1
- **THEN** the draft shows the language at A1 without asking for a level, and it is saved at A1 on
  «Guardar»

#### Scenario: Level is changed in place

- **WHEN** an editor changes an existing language entry's level in the draft and saves
- **THEN** the entry persists with the new level and keeps its certification

#### Scenario: No active level

- **WHEN** every value of the program level catalog is inactive
- **THEN** the editor cannot add a program to the draft

#### Scenario: Duplicate entry is refused in the draft

- **WHEN** an editor adds to the Competencias draft a language the candidate already has
- **THEN** the Spanish duplicate message is shown and the language is not added to the draft

### Requirement: Notes and documents act immediately in edit mode

Notas and Documentos SHALL show their add, edit, retire, upload, mark-primary and remove controls
only in edit mode. Each of those actions SHALL persist as soon as it is performed, as before, with
its existing confirmations. «Hecho» SHALL return the panel to read-only. Document download and the
CV preview SHALL be available in both modes to holders of the document download permission.

#### Scenario: Note added in edit mode

- **WHEN** an editor activates «Editar» on Notas, adds a note and activates «Hecho»
- **THEN** the note persisted when it was added, and the panel shows it read-only

#### Scenario: Document uploaded in edit mode

- **WHEN** a document manager activates «Editar» on Documentos and uploads a file
- **THEN** the upload is sent at once and the document is listed with its scan state

#### Scenario: Download while read-only

- **WHEN** a user holding the document download permission views Documentos read-only
- **THEN** a clean document can be downloaded and previewed

### Requirement: One panel is edited at a time and unsaved changes are protected

At most one panel SHALL be in edit mode. A panel has unsaved changes when:

- for a form panel, its draft differs from the saved data;
- for Notas, the new-note text is not empty or a note is being edited;
- for Documentos, a file is selected and not yet uploaded.

When the user activates «Editar» on another panel:

- the open panel SHALL close if it has no unsaved changes;
- otherwise the application SHALL ask for confirmation to discard them. Declining SHALL keep the
  open panel and its draft.

When a panel has unsaved changes, leaving the candidate page by navigation within the application
SHALL ask for confirmation, and declining SHALL keep the user on the page with the draft intact.
Reloading or closing the browser tab SHALL trigger the browser's leave confirmation.

#### Scenario: Switching from a clean panel

- **WHEN** Educación is in edit mode without changes and the editor activates «Editar» on
  Experiencia
- **THEN** Educación returns to read-only and Experiencia enters edit mode

#### Scenario: Switching from a panel with unsaved changes

- **WHEN** Educación has unsaved changes and the editor activates «Editar» on Experiencia
- **THEN** a confirmation asks whether to discard the changes, and declining keeps Educación open
  with its draft

#### Scenario: Leaving with unsaved changes

- **WHEN** Datos principales has unsaved changes and the editor activates the `Candidatos`
  breadcrumb
- **THEN** a confirmation is shown, and declining keeps the editor on the candidate page with the
  draft

#### Scenario: Closing the tab with unsaved changes

- **WHEN** a panel has unsaved changes and the user reloads or closes the tab
- **THEN** the browser asks for confirmation before leaving

#### Scenario: Leaving without unsaved changes

- **WHEN** no panel has unsaved changes and the user navigates away
- **THEN** no confirmation is shown

### Requirement: Panel editing follows API permissions

«Editar» SHALL be offered on Datos principales, Competencias, Educación, Experiencia and Notas only
to holders of the candidate update permission. It SHALL be offered on Documentos only to holders of
the document upload permission. A user holding neither SHALL see no «Editar» and no editing
control.

On a removed candidate, Competencias, Educación, Experiencia and Notas SHALL NOT offer «Editar»,
and the page SHALL state in Spanish that the candidate must be reactivated to edit those sections.
Datos principales and Documentos SHALL keep following their permissions.

The API SHALL remain the authorization control for every write, independently of which controls
the page renders.

#### Scenario: Reader views a candidate

- **WHEN** a user holding only the candidate read permission opens a candidate page
- **THEN** every panel is displayed with the same content and no «Editar» or editing control is
  present

#### Scenario: Editor without upload permission

- **WHEN** a user holding the candidate update permission but not the document upload permission
  opens a candidate page
- **THEN** every editable panel except Documentos offers «Editar», and the documents are still
  listed

#### Scenario: Document manager without update permission

- **WHEN** a user holding the document upload permission but not the candidate update permission
  opens a candidate page
- **THEN** only Documentos offers «Editar»

#### Scenario: Removed candidate

- **WHEN** an editor opens the page of a removed candidate
- **THEN** Competencias, Educación, Experiencia and Notas offer no «Editar», a Spanish explanation
  is shown, and Datos principales still offers «Editar»

#### Scenario: Write bypasses the page

- **WHEN** an unauthenticated or unauthorized caller sends a candidate, relation, note or document
  write directly to the API
- **THEN** the API refuses it and nothing changes

### Requirement: Panel saves handle concurrent edits

Every panel write SHALL carry the version of the candidate the page holds, and SHALL adopt the
version the API returns. Saving several panels one after another on the same page SHALL NOT be
refused as a conflicting edit. A write refused because another user changed the candidate SHALL
show a Spanish conflict message, keep the panel in edit mode with its draft, and overwrite nothing.

#### Scenario: Two panels saved in turn

- **WHEN** an editor saves Competencias and then saves a change to Datos principales
- **THEN** both changes persist and no conflict is reported

#### Scenario: Someone else edited the candidate

- **WHEN** another user changes the candidate after the editor opened the page and the editor saves
  a panel
- **THEN** the save is refused as a conflict with a Spanish message, the draft is kept, and nothing
  is overwritten

### Requirement: Candidate creation continues on the candidate page

The create page SHALL show only the core record form, a CV picker that can pre-fill empty fields of
that form, and a note that the other sections can be edited on the candidate page once it is saved.
After the first successful save, the application SHALL open the new candidate's page for every
creator. A creator holding the candidate update permission SHALL find «Editar» offered there under
the panel permission rules.

#### Scenario: New candidate is saved

- **WHEN** a user holding the candidate create and update permissions completes the create form
  and saves
- **THEN** the application opens the new candidate's page, where each editable panel offers
  «Editar»

#### Scenario: Creator without update permission

- **WHEN** a user holding the candidate create permission but not the update permission saves a
  new candidate
- **THEN** the application opens the new candidate's page without any «Editar» on the candidate
  update panels

#### Scenario: Create page content

- **WHEN** a user opens the create page
- **THEN** only the CV picker, the core record form and the post-save note are shown

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

### Requirement: Positions panel on the candidate page

The candidate page SHALL show a «Posiciones» panel, after Experiencia and before Notas, when the actor
holds `positions.read`. It SHALL list every position the candidate is linked to, with open positions
first and closed (past) positions after them shown as muted. Each row SHALL show the position title
linking to the position page, the position status, the stage and the date added. Clicking a row
outside its controls opens the position; the title is the row's keyboard-reachable link.

For an actor holding `positions.manage`, a row on an open position SHALL offer a labelled stage
selector and «Quitar de la posición». A stage change SHALL save immediately. Removal SHALL ask for
confirmation. The panel SHALL offer «Añadir a posición» for an active candidate, opening a picker of
open positions searchable by text, in which positions the candidate is already on are not
selectable.

The panel SHALL NOT take part in per-panel edit mode: it has no «Editar» and does not count towards
unsaved-change protection. Without `positions.read`, the panel SHALL NOT render and no position
request SHALL be sent.

#### Scenario: Current and past positions are listed

- **WHEN** a reader opens a candidate linked to one open and one closed position
- **THEN** the open position is listed first, the closed one follows muted, and both show title, status, stage and date added

#### Scenario: Candidate is added to a position from their page

- **WHEN** a manager activates «Añadir a posición» and selects an open position the candidate does not match
- **THEN** the link is created at stage Nuevo and appears in the panel

#### Scenario: Closed position row

- **WHEN** a manager views a row whose position is closed
- **THEN** the stage is shown read-only and no removal is offered

#### Scenario: Removed candidate

- **WHEN** a manager opens a logically removed candidate
- **THEN** «Añadir a posición» is not offered, while existing links remain listed

#### Scenario: Actor lacks position permission

- **WHEN** an actor without `positions.read` opens the candidate page
- **THEN** no «Posiciones» panel is shown and no position request is sent

#### Scenario: Panel does not block editing

- **WHEN** a stage is changed while another panel is in edit mode
- **THEN** the other panel's draft and edit mode are unaffected

### Requirement: Availability block on the candidate page

The candidate page SHALL show the candidate's availability in a panel headed «Disponibilidad» in the first summary row beside Datos principales, never in the page header:

- **Line:** the value as a toned chip — neutral «Sin comprobar», green «Disponible» or red «No disponible» — followed, for «No disponible» with an until date, by «hasta {date}» as text, and by the «(vencido)» marker when that date has passed.
- **When known, check metadata:** «Comprobado el {date} por {name} · {time elapsed}». The time elapsed is in the active language («hoy», «hace 7 meses»). Dates are calendar days shown in the stored day in every time zone.

The value SHALL NOT repeat «Disponibilidad» beneath the panel heading. When there is room, the check metadata SHALL sit to the right of the value on the same row; it SHALL wrap without horizontal scrolling when there is not. The check metadata SHALL be regular weight. The summary row, hints and actions SHALL have readable vertical separation.

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
- **THEN** the block shows a neutral «Sin comprobar» chip without a repeated «Disponibilidad» label and offers «Registrar comprobación» but not «Sigue igual»

#### Scenario: Reconfirming

- **WHEN** an editor activates «Sigue igual» on a candidate checked `available` on 2026-03-12
- **THEN** the block shows today's date, the editor's name and «hoy», the value is unchanged, and «Deshacer» is offered

#### Scenario: Check metadata beside the value

- **WHEN** a reader opens a checked candidate on a wide sections column
- **THEN** the availability chip is on the left and regular-weight «Comprobado el …» metadata is to its right; on a narrow column they remain readable without horizontal scrolling

#### Scenario: Undoing a reconfirm

- **WHEN** the editor then activates «Deshacer»
- **THEN** the block again shows 2026-03-12 and the previous value, and the confirmation is announced

#### Scenario: Lapsed until date

- **WHEN** a reader opens a candidate checked «No disponible» until a date that has passed
- **THEN** the line shows the red «No disponible» chip, «hasta» that date and the «(vencido)» marker, the hint asks for a new check, and «Sigue igual» is not offered

#### Scenario: Changing the value

- **WHEN** an editor opens «Cambiar…», chooses «No disponible», enters an until date and saves
- **THEN** the block shows a red «No disponible» chip followed by «hasta» that date with today's check, and focus returns to «Cambiar…»

#### Scenario: Registering in the inline form

- **WHEN** an editor selects «Disponible» in the inline form on a wide summary panel
- **THEN** «Comprobado el», «Registrar» and «Cancelar» share one row; on a narrow panel they wrap and remain reachable without horizontal scrolling

#### Scenario: Check while another panel is edited

- **WHEN** Datos principales is in edit mode with unsaved changes and the editor activates «Sigue igual», then saves Datos principales
- **THEN** both writes succeed, and no unsaved-change prompt or concurrency conflict appears

#### Scenario: Reader sees no availability actions

- **WHEN** an actor without `candidates.update` opens a candidate
- **THEN** the availability lines are shown and no «Sigue igual», «Cambiar…» or «Deshacer» is offered

### Requirement: Pipeline badges derived from positions

For an actor holding `positions.read`, the candidate page SHALL show badges beside the candidate's
name, derived from the candidate's position links and never stored:

- «En proceso» when at least one link on an open position is at `new`, `shortlisted` or
  `interview`;
- «Contratado» when at least one link, on any position, is at `hired`.

Both MAY show at once. The badges SHALL be text, not colour alone. They SHALL be derived from the
same link list the «Posiciones» panel loads, without a second request, and SHALL follow stage
changes made in that panel without a reload. Without `positions.read`, no badge SHALL be shown and
no position request SHALL be sent.

#### Scenario: Candidate in process and hired before

- **WHEN** a reader with `positions.read` opens a candidate linked at `interview` on an open position
  and at `hired` on a closed one
- **THEN** both «En proceso» and «Contratado» are shown

#### Scenario: Stage change updates the badges

- **WHEN** a manager changes that open position's link to `rejected` in «Posiciones»
- **THEN** «En proceso» disappears without a reload and «Contratado» remains

#### Scenario: Candidate on a closed position only

- **WHEN** a candidate's only link is at `shortlisted` on a closed position
- **THEN** no badge is shown

#### Scenario: Reader without position permission

- **WHEN** an actor without `positions.read` opens a candidate
- **THEN** no badge is shown and no position request is sent
