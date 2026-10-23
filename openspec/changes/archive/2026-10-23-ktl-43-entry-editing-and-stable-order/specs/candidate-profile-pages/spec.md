## MODIFIED Requirements

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

In edit mode, each Educación and Experiencia entry SHALL also offer «Editar», whose accessible name
names the entry («Editar la formación {titulación}», «Editar la experiencia {puesto} en
{empresa}»). «Editar» SHALL load the entry into the same form, retitled «Editar formación» or
«Editar experiencia», move focus to the form's first field and mark the entry's row as the one
being edited. The submit control SHALL read «Actualizar formación» or «Actualizar experiencia», and
«Cancelar edición» SHALL return the form to a new entry without changing the draft. An update SHALL
apply the same validation as an add, replace the entry in the draft where it stands, keep its
identity and the fields the form does not show (notes, functions), and return the form to a new
entry. An update that changes no value SHALL leave the panel without unsaved changes. The form SHALL
keep offering the entry's education type, education status or sector when that catalog value is no
longer active, so the update does not drop it. Removing the entry being edited, «Cancelar» or a
successful «Guardar» SHALL also end the edit.

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

#### Scenario: Entry is edited in place

- **WHEN** an editor activates «Editar» on an education entry, changes its centre, activates
  «Actualizar formación» and then «Guardar»
- **THEN** the education is saved with that entry in the same position, under the same identity,
  with the new centre, and the form is titled «Nueva formación» again

#### Scenario: Edit keeps the fields the form does not show

- **WHEN** an editor updates an experience entry that has functions recorded
- **THEN** the saved entry keeps those functions

#### Scenario: Entry holds a retired catalog value

- **WHEN** an editor edits an education entry whose status is no longer an active catalog value
- **THEN** the form shows that status selected, and updating the entry keeps it

#### Scenario: Edit that changes nothing

- **WHEN** an editor activates «Editar» on an entry and then «Actualizar» without changing a field
- **THEN** the panel reports no unsaved changes

#### Scenario: Edit is cancelled

- **WHEN** an editor changes a field of an entry being edited and activates «Cancelar edición»
- **THEN** the entry is unchanged and the form is empty and titled for a new entry

#### Scenario: Edit refused by validation

- **WHEN** an editor clears the degree of an education entry being edited and activates
  «Actualizar formación»
- **THEN** the Spanish validation message is shown at the top of the form, the form stays in edit,
  and the entry is unchanged

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

## ADDED Requirements

### Requirement: Educación and Experiencia rows lead with their years

Each Educación and Experiencia row SHALL begin with the years of the entry, in a muted column of
fixed minimum width so that the rows align, in both read-only and edit mode:

- an experience with a start and an end date in different years SHALL show `{inicio}–{fin}`;
- an experience with only one of those dates, or both in the same year, SHALL show that year;
- a current experience with a start date SHALL show `{inicio}–actualidad` and SHALL NOT repeat
  the «Actual» marker; a current experience without a start date SHALL keep the marker and show no
  years;
- an education entry SHALL show its end year.

When no row of a panel has a year, the column SHALL NOT be shown; when any row has one, every row
SHALL reserve the column.

#### Scenario: Experience range

- **WHEN** Experiencia lists an entry from 1 March 2019 to 30 June 2024
- **THEN** its row begins with `2019–2024`

#### Scenario: Current role

- **WHEN** Experiencia lists a current role started in 2020
- **THEN** its row begins with `2020–actualidad` and shows no «Actual» marker

#### Scenario: Education end year

- **WHEN** Educación lists an entry finished in 2020 and another without an end year
- **THEN** the first row begins with `2020` and the second keeps an empty years column

#### Scenario: No years at all

- **WHEN** no Experiencia entry has a date
- **THEN** the rows show no years column

### Requirement: Experience form groups its dates

The Experiencia form SHALL order its fields, in a two-column grid, as Empresa and Puesto; Sector
and Años de experiencia; «Puesto actual» on its own row; and Fecha inicio and Fecha fin together on
the last row.

#### Scenario: Field order

- **WHEN** an editor opens the Experiencia form
- **THEN** «Años de experiencia» is beside «Sector», «Puesto actual» follows, and both dates share
  the last row
