## ADDED Requirements

### Requirement: File content transmission

The shared transport SHALL send a selected file's content to the API as a multipart request
without setting a media type of its own, so that the browser supplies the multipart boundary.
File content SHALL NOT be transformed, truncated, or re-encoded before transmission, and no
feature service SHALL report an upload as succeeding without having transmitted the content.

#### Scenario: File is uploaded

- **WHEN** a feature service uploads a selected file
- **THEN** the request carries the file's bytes as multipart form data, the transport sets no
  media type for that body, and the correlation identifier is present as on any other request

#### Scenario: Content is discarded

- **WHEN** a feature service builds an upload from a file's name, type and size without
  transmitting its bytes
- **THEN** the change is incomplete, because a stored document must contain the uploaded content

#### Scenario: Upload is refused by the API

- **WHEN** the API refuses an upload for size, format, or authorization
- **THEN** the refusal is surfaced as a Spanish message derived from the problem response, and
  the interface shows no stored document

#### Scenario: Upload is cancelled or times out

- **WHEN** an upload is cancelled by the user or exceeds its timeout
- **THEN** the request is aborted, the interface reports it as not completed, and no document is
  shown as stored

### Requirement: Deferred availability in the interface

When the API accepts work whose result is not immediately available, the interface SHALL present
the intermediate state to the user in Spanish rather than reporting completion. It SHALL observe
the server's state until it settles, SHALL show the settled outcome — available or refused with
a non-technical reason — and SHALL stop observing when the user leaves the view.

#### Scenario: Accepted upload is not yet available

- **WHEN** the API accepts a document upload that is awaiting a scan result
- **THEN** the interface shows the document in an "en análisis" state, does not offer a download,
  and does not report the upload as finished

#### Scenario: Work settles as available

- **WHEN** the observed document becomes available
- **THEN** the interface shows it as "disponible" and offers a download, without requiring the
  user to reload the page

#### Scenario: Work settles as refused

- **WHEN** the observed document is refused
- **THEN** the interface shows a Spanish explanation of the refusal, keeps the document visible
  in its refused state, and offers no download

#### Scenario: User leaves the view while work is pending

- **WHEN** the user navigates away while a document is still pending
- **THEN** the interface stops observing it and issues no further requests for it

#### Scenario: Observation cannot reach the API

- **WHEN** the API becomes unreachable while a document is being observed
- **THEN** the interface reports the failure to observe rather than assuming either outcome, and
  never shows a pending document as available
