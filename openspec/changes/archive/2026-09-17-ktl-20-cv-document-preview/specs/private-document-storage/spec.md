## ADDED Requirements

### Requirement: Controlled inline preview

A document SHALL be renderable inside the application only through the same permission-checked,
clean-scan-gated response that serves a download. Inline preview SHALL NOT introduce a second
delivery path, a permanent URL, a public URL, or any weaker gate. Only formats the client can
render natively SHALL be previewed; every other allowed format SHALL be offered for download
instead, with an explanation and without fetching its content.

#### Scenario: Clean previewable document is rendered

- **WHEN** a caller holding the document download permission opens a candidate whose document is
  available, clean, and of a natively renderable format
- **THEN** the document content is served by the same permission-checked, non-cacheable response
  used for download and is rendered inside the application without a further navigation

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
  selection changes or the page is left, and at no point exposes a storage key, host path,
  drive letter, mount point, or permanent URL

#### Scenario: Preview is audited as a download

- **WHEN** a document's content is served for an inline preview
- **THEN** the same download audit entry is recorded as for an explicit download, because the
  content left the server, and it contains no file content

#### Scenario: Content delivery fails

- **WHEN** the content request for a preview is denied, times out, or fails
- **THEN** no partial or stale content is rendered, the caller is shown a non-technical failure
  message with a retry affordance, and no storage detail is revealed
