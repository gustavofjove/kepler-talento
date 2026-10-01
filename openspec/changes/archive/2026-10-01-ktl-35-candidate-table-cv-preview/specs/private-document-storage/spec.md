## MODIFIED Requirements

### Requirement: Controlled inline preview

A document SHALL be renderable inside the application only through the same permission-checked,
clean-scan-gated response that serves a download. This holds whether the preview is opened from
the candidate page or from a candidate table row. Inline preview SHALL NOT introduce a second
delivery path, a permanent URL, a public URL, or any weaker gate. Only formats the client can
render natively SHALL be previewed; every other allowed format SHALL be offered for download
instead, with an explanation and without fetching its content. A list's indication that a primary
CV can be previewed SHALL NOT grant access to its content, and SHALL NOT replace any check of the
content response.

#### Scenario: Clean previewable document is rendered

- **WHEN** a caller holding the document download permission opens a candidate whose document is
  available, clean, and of a natively renderable format
- **THEN** the document content is served by the same permission-checked, non-cacheable response
  used for download and is rendered inside the application without a further navigation

#### Scenario: Preview opened from a table row

- **WHEN** a caller holding the document download permission activates «Ver» on a candidate table
  row
- **THEN** the content is requested through the same permission-checked, clean-scan-gated,
  audited response as on the candidate page, and nothing is fetched when the selected document is
  not clean

#### Scenario: Preview requires the download permission

- **WHEN** a caller without the document download permission views a candidate
- **THEN** no document content is requested, no preview is offered, and a direct request for the
  content is denied without revealing whether the document exists

#### Scenario: Unclean document is never previewed

- **WHEN** a candidate's document is pending, infected, rejected, unscannable, or a legacy record
  without a binary
- **THEN** no content request is made, no preview is rendered, and the caller is told only its
  availability state

#### Scenario: Format cannot be rendered natively

- **WHEN** the selected document's recorded content type is an allowed format the client cannot
  render natively
- **THEN** no content is fetched, the caller is told the format cannot be previewed, and the
  download action remains available

#### Scenario: Preview exposes no durable reference

- **WHEN** a document is previewed
- **THEN** the rendered reference is opaque, confined to the current page, released when the
  selection changes, the preview closes or the page is left, and at no point exposes a storage
  key, host path, drive letter, mount point, or permanent URL

#### Scenario: Preview is audited as a download

- **WHEN** a document's content is served for an inline preview
- **THEN** the same download audit entry is recorded as for an explicit download, because the
  content left the server, and it contains no file content

#### Scenario: Content delivery fails

- **WHEN** the content request for a preview is denied, times out, or fails
- **THEN** no partial or stale content is rendered, the caller is shown a non-technical failure
  message with a retry affordance, and no storage detail is revealed

#### Scenario: Acting identity is inspected

- **WHEN** the acting identity on a document audit event is inspected
- **THEN** it is an internal user identifier or the system actor, and it is not an email address, a
  display name or an external subject identifier

#### Scenario: Background process produces an event

- **WHEN** a scan verdict or a storage reconciliation records an audit event with no person behind it
- **THEN** the event records the system actor, distinguishable from an absent actor
