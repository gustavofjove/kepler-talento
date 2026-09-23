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

#### Scenario: Editor changes every part of a candidate

- **WHEN** a user holding the candidate update and document upload permissions opens a candidate's
  edit page
- **THEN** they can change the core fields, add and remove relation entries and tags, add, edit and
  retire notes, and upload and manage documents without leaving the page

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
localization catalogue. Each page SHALL have a single top-level heading and a heading per section;
form controls SHALL keep programmatic labels, stable names and test identifiers; the save
confirmation SHALL be announced to assistive technology. Both pages SHALL be operable by keyboard
and SHALL NOT scroll horizontally at 390 pixels wide.

#### Scenario: Save confirmation with a screen reader

- **WHEN** an editor saves the core record
- **THEN** the confirmation is exposed through a live status region

#### Scenario: Narrow viewport

- **WHEN** the detail or edit page is shown at 390 pixels wide
- **THEN** every section and control remains reachable and the page does not scroll horizontally
