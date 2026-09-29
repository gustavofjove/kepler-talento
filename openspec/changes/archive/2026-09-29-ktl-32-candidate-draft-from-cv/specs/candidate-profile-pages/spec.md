## MODIFIED Requirements

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
