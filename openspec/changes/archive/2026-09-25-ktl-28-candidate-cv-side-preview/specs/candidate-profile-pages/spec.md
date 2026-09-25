## MODIFIED Requirements

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

## ADDED Requirements

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
