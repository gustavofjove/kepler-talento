## MODIFIED Requirements

### Requirement: Edit page owns every candidate change

The edit page of an existing candidate SHALL offer, on one page, editing of the core record,
languages, programs, education, experience, skills, tags and custom notes, and, for users also
holding the document upload permission, uploading and managing documents. Reaching the edit page
SHALL require the candidate update permission. The API SHALL remain the authorization control for
every write, independently of which controls the page renders.

Languages, programs, skills and tags SHALL be edited with the catalog value picker. For languages,
programs and skills the level SHALL be required: an entry SHALL be saved only once its level is
chosen, and abandoning the level choice SHALL save nothing. An existing entry's level, and its
certification (languages) or years of experience (programs), SHALL be changeable in place without
removing the entry. Each add, change and removal SHALL persist immediately, as the other sections
do, and a refused write SHALL be reported on the affected entry without discarding the others.

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

#### Scenario: Entry is saved once its level is chosen

- **WHEN** an editor adds a language and chooses its level
- **THEN** the language and level persist immediately

#### Scenario: Level choice is abandoned

- **WHEN** an editor adds a language and closes the level choice without selecting a level
- **THEN** no language entry is added and no write is sent

#### Scenario: Level is changed in place

- **WHEN** an editor changes an existing language entry from one level to another
- **THEN** the entry persists with the new level and keeps its certification

#### Scenario: Relation write is refused

- **WHEN** the API refuses a relation write, for example because another user changed the candidate
- **THEN** the affected entry shows the Spanish error with a retry, and the other entries and the
  core form are unchanged
