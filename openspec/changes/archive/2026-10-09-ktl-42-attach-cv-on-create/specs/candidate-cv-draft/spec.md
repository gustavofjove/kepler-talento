## MODIFIED Requirements

### Requirement: Create form applies a draft without overwriting

The create page SHALL offer a CV picker to a user who can create candidates. Applying a draft SHALL
fill only fields that are empty at the moment the draft arrives and SHALL NOT change a field the
user has typed in. Every filled field SHALL stay editable, SHALL be marked as suggested from the CV,
and a `low` confidence suggestion SHALL additionally be marked for review. Nothing SHALL be saved
until the user submits the form. Whether the CV is attached to the saved candidate is governed by
«Read CV is attached to the created candidate».

#### Scenario: Draft fills empty fields

- **WHEN** a user picks a CV on the create page and the draft arrives while every field is empty
- **THEN** each suggested field is filled and marked as suggested, low-confidence ones are also
  marked for review, and no candidate has been saved

#### Scenario: Typed values are kept

- **WHEN** a user has typed a first name and then picks a CV whose draft suggests a different one
- **THEN** the typed first name is kept and only the empty fields are filled

#### Scenario: User edits a suggestion

- **WHEN** the user changes a suggested value
- **THEN** the new value is kept and the field is no longer marked as suggested

#### Scenario: Empty or refused draft

- **WHEN** the draft is empty because the CV has no text, or the CV is refused
- **THEN** the form is unchanged and the user sees a localized message explaining what to do,
  without technical detail

#### Scenario: Draft request is in flight

- **WHEN** the draft request has not yet completed
- **THEN** the picker shows a busy state and the form remains usable

#### Scenario: Saved candidate has no document

- **WHEN** the user saves a candidate after applying a draft, and the CV is not attached because
  the attach choice was cleared or the user lacks `documents.upload`
- **THEN** the candidate is created exactly as from a manually filled form, with no document
  attached

#### Scenario: Picking a CV saves nothing by itself

- **WHEN** the user picks a CV and leaves the create page without submitting the form
- **THEN** no candidate and no document exist as a result, and the file is no longer held by the
  page

## ADDED Requirements

### Requirement: Read CV is attached to the created candidate

After the draft endpoint has returned a draft for a CV, including an empty `cv_draft.no_text`
draft, the create page SHALL hold that file in browser memory only, until the page is left or
another CV is picked. A CV whose draft request was refused, failed or was superseded SHALL NOT be
held. While a CV is held and the user holds `documents.upload`, the page SHALL offer a choice to
attach it, selected by default; a user without `documents.upload` SHALL NOT be offered the choice
and SHALL NOT have the CV attached.

When the candidate is created and the choice is selected, the page SHALL upload the held file
through the standard candidate document upload as the candidate's primary CV document, with every
control of that operation (permission check before validation, quarantine, malware scan before the
document is available, private storage under an opaque key, audit) applying unchanged. The CV
draft operation itself SHALL continue to store nothing.

A failed attach SHALL NOT undo or block the candidate's creation: the user SHALL be taken to the
created candidate and SHALL see a localized warning that the CV was not attached and can be
uploaded from the candidate page. If the candidate's creation fails, nothing SHALL be uploaded.
The page SHALL NOT log the file or its name.

#### Scenario: CV is attached on save

- **WHEN** a user with `documents.upload` picks a CV, keeps the attach choice selected, and saves
  the form
- **THEN** the candidate is created, its primary CV document is that file in the pending-scan
  state, and the user is taken to the candidate page

#### Scenario: Latest CV wins

- **WHEN** the user picks one CV, then picks another whose draft is returned, and saves with the
  choice selected
- **THEN** only the second CV is attached

#### Scenario: Refused CV is not offered

- **WHEN** the draft endpoint refuses the picked CV
- **THEN** no attach choice is offered for it and saving attaches no document

#### Scenario: Actor cannot upload documents

- **WHEN** a user who may create candidates but lacks `documents.upload` picks a CV and saves
- **THEN** no attach choice is shown, no upload is attempted, and the candidate is created with no
  document

#### Scenario: Attach fails after the candidate is created

- **WHEN** the candidate is created but the document upload is refused or fails
- **THEN** the candidate is kept, the user is taken to it, and a localized warning says the CV was
  not attached and can be uploaded from the candidate page

#### Scenario: Candidate creation fails

- **WHEN** the candidate create request fails
- **THEN** no document upload is attempted, the user stays on the create page with the error
  shown, and the held CV and attach choice are kept for a retry

#### Scenario: Upload endpoint still fails closed

- **WHEN** an unauthenticated caller, or an actor without `documents.upload`, posts a file to the
  candidate document upload
- **THEN** the request is refused before validation and nothing is stored
