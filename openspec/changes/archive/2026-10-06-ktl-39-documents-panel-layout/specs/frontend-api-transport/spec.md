## MODIFIED Requirements

### Requirement: Deferred availability in the interface

When the API accepts work whose result is not immediately available, the interface SHALL present
the intermediate state to the user in Spanish rather than reporting completion. It SHALL observe
the server's state until it settles, SHALL show the settled outcome — available, by offering the
document's download without a pending or refused label, or refused with a non-technical reason —
and SHALL stop observing when the user leaves the view.

#### Scenario: Accepted upload is not yet available

- **WHEN** the API accepts a document upload that is awaiting a scan result
- **THEN** the interface shows the document in an "en análisis" state, does not offer a download,
  and does not report the upload as finished

#### Scenario: Work settles as available

- **WHEN** the observed document becomes available
- **THEN** the interface removes its "en análisis" state, shows no state label for it, and offers
  a download to holders of the download permission, without requiring the user to reload the page

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
