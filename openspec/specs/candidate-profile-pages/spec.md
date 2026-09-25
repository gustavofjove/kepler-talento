# Candidate Profile Pages Specification

## Purpose

Defines how the application presents a candidate to its users: a read-only detail page for viewing
a profile and its documents, and an edit page that owns every change to the candidate.

## Requirements

### Requirement: Candidate detail page is read-only

The candidate detail page SHALL display the candidate's core record, relation collections (languages,
programs, education, experience, skills), tags, custom notes and documents, and SHALL NOT render any
control that adds, changes, removes, retires, uploads, replaces or marks as primary any of them. This
SHALL hold for every user, whatever permissions they hold. Each section SHALL show its empty state
when it has no entries.

#### Scenario: Editor views a candidate

- **WHEN** a user holding the candidate update and document upload permissions opens a candidate's
  detail page
- **THEN** every section is displayed and no add, remove, edit, retire, upload, replace or
  set-primary control is present

#### Scenario: Reader views a candidate

- **WHEN** a user holding only the candidate read permission opens a candidate's detail page
- **THEN** every section is displayed with the same content and no editing control is present

#### Scenario: Section without entries

- **WHEN** a candidate has no entries in a section
- **THEN** the detail page shows that section's empty state rather than a blank area

### Requirement: Detail page keeps viewing and status actions

The detail page SHALL keep document download and the CV preview for users holding the document
download permission, and SHALL keep the link to the edit page and the activate/deactivate action for
users holding the candidate update permission. Activation and deactivation SHALL still require
explicit confirmation.

#### Scenario: Document is downloaded from the detail page

- **WHEN** a user holding the document download permission opens a candidate with a clean document
- **THEN** the document can be downloaded and previewed from the detail page

#### Scenario: Editor reaches the edit page

- **WHEN** a user holding the candidate update permission opens the detail page
- **THEN** a link to the candidate's edit page and the activate/deactivate action are offered

#### Scenario: Reader sees no status actions

- **WHEN** a user without the candidate update permission opens the detail page
- **THEN** neither the edit link nor the activate/deactivate action is offered

### Requirement: Edit page owns every candidate change

The edit page of an existing candidate SHALL offer, on one page, editing of the core record,
languages, programs, education, experience, skills, tags and custom notes, and, for users also
holding the document upload permission, uploading and managing documents. Reaching the edit page
SHALL require the candidate update permission. The API SHALL remain the authorization control for
every write, independently of which controls the page renders.

Languages, programs, skills and tags SHALL be edited with the catalog value picker. For languages,
programs and skills the level SHALL be required: an added entry SHALL be saved at once with the
lowest active level of its level family in catalog order, and no entry SHALL be saved without a
level. An existing entry's level, and its certification (languages) or years of experience
(programs), SHALL be changeable in place without removing the entry. Each add, change and removal
SHALL persist immediately, as the other sections do, and a refused write SHALL be reported on the
affected entry without discarding the others.

#### Scenario: Editor changes every part of a candidate

- **WHEN** a user holding the candidate update and document upload permissions opens a candidate's
  edit page
- **THEN** they can change the core fields, add, change and remove relation entries and tags, add,
  edit and retire notes, and upload and manage documents without leaving the page

#### Scenario: Editor without upload permission

- **WHEN** a user holding the candidate update permission but not the document upload permission
  opens the edit page
- **THEN** every section except document upload and management is editable, and the documents are
  still listed

#### Scenario: Reader is refused the edit page

- **WHEN** a user without the candidate update permission navigates to a candidate's edit page
- **THEN** the page is not shown and no write control is rendered

#### Scenario: Write bypasses the pages

- **WHEN** an unauthenticated or unauthorized caller sends a candidate, relation, note or document
  write directly to the API
- **THEN** the API refuses it as it does today, and nothing changes

#### Scenario: Entry is saved with the lowest level

- **WHEN** an editor adds a language and the language level catalog's first active value is A1
- **THEN** the language persists immediately with level A1, and no level choice is requested

#### Scenario: Entry is saved once its level is chosen

- **WHEN** an editor adds a language and then chooses another level on its chip
- **THEN** the language persists first with the lowest level and then with the chosen level, as one
  entry

#### Scenario: Level choice is abandoned

- **WHEN** an editor opens a newly added language's chip and closes the editor without choosing a
  level
- **THEN** the language stays saved with the lowest level and no further write is sent

#### Scenario: No active level

- **WHEN** every value of the program level catalog is inactive
- **THEN** the editor cannot add a program, and no program write is sent

#### Scenario: Level is changed in place

- **WHEN** an editor changes an existing language entry from one level to another
- **THEN** the entry persists with the new level and keeps its certification

#### Scenario: Relation write is refused

- **WHEN** the API refuses a relation write, for example because another user changed the candidate
- **THEN** the affected entry shows the Spanish error with a retry, and the other entries and the
  core form are unchanged

### Requirement: Edit page states its save behaviours

The edit page SHALL tell the user that the core record is saved with the save action and that every
other section is saved as soon as a change is made. Saving the core record of an existing candidate
SHALL keep the user on the edit page, confirm the save accessibly, and SHALL NOT discard entries in
the other sections. A change in one section SHALL NOT cause the user's own subsequent save on the
same page to be refused as a conflicting edit; a concurrent edit by another user SHALL still be
refused as a conflict.

#### Scenario: Core record is saved

- **WHEN** an editor changes a core field on an existing candidate and saves
- **THEN** the change persists, a confirmation is announced, and the user remains on the edit page

#### Scenario: Section change then core save

- **WHEN** an editor adds a skill and then saves a changed core field on the same page
- **THEN** both changes persist and no conflict is reported

#### Scenario: Unsaved core draft while a section changes

- **WHEN** an editor has an unsaved core change and adds a language
- **THEN** the language persists immediately and the unsaved core change stays in the form until
  it is saved

#### Scenario: Someone else edited the candidate

- **WHEN** another user changes the candidate after the editor opened the page and the editor saves
- **THEN** the save is refused as a conflict with a Spanish message and nothing is overwritten

### Requirement: Candidate creation continues on the edit page

The create page SHALL show only the core record form, with a note that the other sections become
available once the candidate is saved. After the first successful save, the application SHALL open
the new candidate's edit page with every section available when the user holds the candidate update
permission, and the new candidate's detail page otherwise.

#### Scenario: New candidate is saved

- **WHEN** a user holding the candidate create and update permissions completes the create form
  and saves
- **THEN** the application opens the new candidate's edit page, where relations, tags, notes and
  documents can be added

#### Scenario: Creator without update permission

- **WHEN** a user holding the candidate create permission but not the update permission saves a
  new candidate
- **THEN** the application opens the new candidate's detail page instead of a page it would refuse

#### Scenario: Create page content

- **WHEN** a user opens the create page
- **THEN** only the core record form and the post-save note are shown

### Requirement: Candidate pages are localized and accessible

All copy on the candidate detail, edit and create pages SHALL be Spanish and come from the
localization catalogue. Each page SHALL have a single top-level heading and a heading per panel;
within the Competencias panel each family SHALL be identified by its visible row label, which
names its picker, rather than by its own heading. Form controls SHALL keep programmatic labels,
stable names and test identifiers; the save confirmation SHALL be announced to assistive
technology. Both pages SHALL be operable by keyboard and SHALL NOT scroll horizontally at 390
pixels wide.

#### Scenario: Save confirmation with a screen reader

- **WHEN** an editor saves the core record
- **THEN** the confirmation is exposed through a live status region

#### Scenario: Families are named by their row labels

- **WHEN** an assistive technology user navigates the Competencias panel
- **THEN** the panel has its own heading and each family's picker is announced with its row label

#### Scenario: Narrow viewport

- **WHEN** the detail or edit page is shown at 390 pixels wide
- **THEN** every section and control remains reachable and the page does not scroll horizontally

### Requirement: Competencias panel and stacked sections

The candidate detail page and the edit page of an existing candidate SHALL present skills,
languages, programs and tags together in one panel headed `Competencias`, as one family row each
in the shared family row layout, in the order Habilidades, Idiomas, Programas, Etiquetas. When
catalogs cannot be loaded, the catalog status notice SHALL appear once for the panel. Every section
panel on both pages SHALL span the full width of the sections column and be stacked vertically, in
this order:

- Edit page: Datos principales, Competencias, Educación, Experiencia, Notas, Documentos.
- Detail page: Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas,
  Documentos.

The sections column SHALL be the full content width, unless the CV preview is shown beside it as
the CV preview requirement describes.

On the detail page the Competencias rows SHALL be read-only, and each family without entries
SHALL show its empty-state text in its row.

#### Scenario: Edit page layout

- **WHEN** an editor opens an existing candidate's edit page
- **THEN** Datos principales, Competencias, Educación, Experiencia, Notas and Documentos are shown
  across the sections column, one below another, and Competencias shows the rows Habilidades,
  Idiomas, Programas and Etiquetas in that order

#### Scenario: Detail page layout

- **WHEN** any reader opens a candidate's detail page
- **THEN** Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas and Documentos
  are shown across the sections column, one below another, and Competencias shows the four rows
  read-only

#### Scenario: Family without entries on the detail page

- **WHEN** a candidate has no programs and a reader opens the detail page
- **THEN** the Programas row shows its empty-state text

#### Scenario: Catalogs are unavailable on the edit page

- **WHEN** the catalogs fail to load on the edit page
- **THEN** a single catalog notice is shown in the Competencias panel and every family's add
  control is disabled

### Requirement: CV preview beside the candidate sections

The candidate detail page and the edit page of an existing candidate SHALL offer the CV preview
to users holding the document download permission, under the existing preview rules. The
new-candidate page SHALL NOT show a preview.

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

- **WHEN** a user holding the document download permission opens the detail page of a candidate
  with a clean PDF at 1920×1080
- **THEN** the CV preview is shown to the right of Datos principales

#### Scenario: Preview on the edit page

- **WHEN** a user holding both the candidate update and document download permissions opens the
  edit page of a candidate with a clean PDF at 1920×1080
- **THEN** the CV preview is shown to the right of Datos principales

#### Scenario: Preview stays in view

- **WHEN** the preview is shown beside the sections and the user scrolls down to Documentos
- **THEN** the preview remains fully visible below the application header

#### Scenario: Preview stacked on a narrower screen

- **WHEN** a user holding the document download permission opens either page at 1366×768
- **THEN** the CV preview is shown below the last section

#### Scenario: No preview leaves one column

- **WHEN** a user without the document download permission opens either page at 1920×1080
- **THEN** no preview content is requested, the sections take the full content width and no empty
  column is shown

#### Scenario: Field groups follow the sections column

- **WHEN** the preview is shown beside the sections and the sections column is too narrow for two
  field columns
- **THEN** the main data form and the Educación, Experiencia and Documentos forms show their fields
  in one column

#### Scenario: New candidate page has no preview

- **WHEN** an editor opens the new-candidate page
- **THEN** no CV preview is shown
