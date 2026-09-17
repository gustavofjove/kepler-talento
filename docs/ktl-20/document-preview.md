# KTL-20: CV document preview

The candidate detail page ends with an inline CV preview for users holding
`documents.download`. The panel uses the existing permission-checked content endpoint; hiding the
panel is only a usability measure and is not the authorization boundary.

Only an `Available` document recorded as `application/pdf` is previewed. DOC, DOCX, ODT, RTF,
TXT and image documents remain downloadable but are not rendered inline. Server-side Word-to-PDF
conversion is deliberately deferred because it would add a conversion pipeline, dependencies and
new security boundaries.

The SPA fetches the non-cacheable response through `ApiTransport`, creates an opaque `blob:` URL
and renders it in the browser PDF viewer. Fetched `Blob` bytes are cached by document for the page
visit to avoid repeated transfers and duplicate audit events. Only the active selection has an
object URL; it is revoked when the selection or candidate changes and when the panel unmounts.
Pending documents are polled until scanning settles so a newly available PDF appears without a
page reload. No storage key, host path or permanent URL enters the DOM.

## Security policy

The frontend CSP adds exactly `frame-src blob:; object-src blob:`. This is required by the native
PDF viewer. `default-src 'self'`, `script-src 'self'`, `connect-src 'self'` and
`frame-ancestors 'none'` remain unchanged. The relaxation is confined to embedded documents;
`blob:` is not an allowed script or connection source. A security test pins the complete policy.

Preview uses `/api/candidates/{candidateId}/documents/{documentId}/content`, so the backend clean
scan gate, `documents.download` authorization, `private, no-store` response and non-disclosure
rules are unchanged. Serving a preview writes the existing `document.downloaded` audit event
because the bytes have left the server. No document content is recorded in that event.
