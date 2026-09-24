## ADDED Requirements

### Requirement: Competencias panel and stacked sections

The candidate detail page and the edit page of an existing candidate SHALL present skills,
languages, programs and tags together in one panel headed `Competencias`, as one family row each
in the shared family row layout, in the order Habilidades, Idiomas, Programas, Etiquetas. When
catalogs cannot be loaded, the catalog status notice SHALL appear once for the panel. Every panel
on both pages SHALL span the full content width and be stacked vertically, in this order:

- Edit page: Datos principales, Competencias, Educación, Experiencia, Notas, Documentos.
- Detail page: Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas,
  Documentos, followed by the CV preview.

On the detail page the Competencias rows SHALL be read-only, and each family without entries
SHALL show its empty-state text in its row.

#### Scenario: Edit page layout

- **WHEN** an editor opens an existing candidate's edit page
- **THEN** Datos principales, Competencias, Educación, Experiencia, Notas and Documentos are shown
  full width, one below another, and Competencias shows the rows Habilidades, Idiomas, Programas
  and Etiquetas in that order

#### Scenario: Detail page layout

- **WHEN** any reader opens a candidate's detail page
- **THEN** Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas and Documentos
  are shown full width, one below another, and Competencias shows the four rows read-only

#### Scenario: Family without entries on the detail page

- **WHEN** a candidate has no programs and a reader opens the detail page
- **THEN** the Programas row shows its empty-state text

#### Scenario: Catalogs are unavailable on the edit page

- **WHEN** the catalogs fail to load on the edit page
- **THEN** a single catalog notice is shown in the Competencias panel and every family's add
  control is disabled

## MODIFIED Requirements

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
