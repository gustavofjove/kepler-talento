# Design — KTL-20 CV document preview

## Context

See `proposal.md` — Why. This design covers only what is not obvious from the requirements in
`specs/private-document-storage/spec.md`.

Constraints that shape the approach:

- **The bytes endpoint already exists and is correct.** `GET /api/candidates/{candidateId}/documents/{documentId}/content`
  ([DocumentEndpoints.cs](../../../backend/Web/Features/Documents/DocumentEndpoints.cs)) checks
  `documents.download`, returns `401` for unauthenticated callers and `404` for authenticated
  callers without permission, serves only `Clean` documents via
  `IDocumentDownloadService.OpenCleanAsync`, and sets
  `Cache-Control: private, no-store` plus `X-Content-Type-Options: nosniff`.
- **The API forbids being framed.** Every API response carries `X-Frame-Options: DENY` and
  `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
  ([Program.cs:126-133](../../../backend/Web/Program.cs#L126-L133)). Under Compose these are
  stripped from `/api/` by nginx `proxy_hide_header`, but they are present on the Vite dev
  proxy path, so a frame pointed at the endpoint is unreliable by construction and must not be
  used.
- **The SPA CSP has no frame or object directive.** `nginx.conf` serves
  `default-src 'self'; img-src 'self' data: blob:; …`, so `frame-src` and `object-src` fall back
  to `default-src 'self'` and a `blob:` document is blocked.
- **The transport is the only sanctioned fetch path.** `ApiTransport` supplies the correlation
  id, the timeout and the ProblemDetails→`AppError` mapping; its default timeout is 15 s, which
  is too tight for a 20 MB file.
- **Metadata already distinguishes previewable content.** `CandidateDocumentResponse` carries
  `mimeType`, `isPrimary`, `uploadedAt` and `availabilityState`.

## Goals / Non-Goals

**Goals:**

- Render a clean PDF inline with no new backend surface, no new runtime dependency, and the
  smallest possible CSP delta.
- Keep one fetch path for document bytes so authorization, correlation and error mapping cannot
  diverge between download and preview.
- Make every non-previewable and non-available case explicit and non-technical in the UI, without
  requesting content.

**Non-Goals:**

- Any viewer feature beyond what the browser provides (paging, zoom, search, annotations,
  printing controls).
- Cross-browser parity beyond the browsers this intranet targets (Chromium-based and Firefox).
- Reducing or restructuring document audit entries.

## Decisions

### D1. Fetch through `ApiTransport`, render a same-origin `blob:` object URL

The panel calls a new `DocumentService.openPreview(candidateId, documentId, fallbackFileName, signal?)`
which performs the same `transport.download()` call as `download()` but returns the `ApiDownload`
instead of clicking an anchor. `download()` is refactored to call `openPreview()` and then do the
anchor click, so there is exactly one place that fetches document bytes and the service stays the
single point of access to the backend capability.

The object URL is created from the returned blob and handed to the viewer element.

- **Alternative — point the viewer at `/api/.../content` directly.** Rejected: blocked by
  `X-Frame-Options: DENY` on the dev-proxy path, bypasses the transport's correlation id,
  timeout and error mapping, and would surface a raw API URL in the DOM.
- **Alternative — a signed short-lived preview URL.** Rejected: a new backend surface and a new
  durable reference to a private document, which the spec forbids, to solve a problem the blob
  already solves.

`openPreview` passes `timeoutMs: 60_000`, matching the upload path, because the 15 s default
would abort a large scanned CV on a slow link.

### D2. `<object type="application/pdf">`, not `<iframe>`

The viewer is `<object type="application/pdf" data={objectUrl}>` with the fallback message and
the «Descargar» link as its child content.

- `<object>` has native fallback content: a browser without a PDF viewer renders the children
  instead of a blank rectangle, which is exactly the degraded experience we want.
- Declaring `type` avoids depending on how the blob's recorded content type is interpreted.
- `sandbox` is not applied. It is not an `<object>` attribute, and on an `<iframe>` it is the
  known cause of the built-in Chromium viewer failing to initialise. The content is confined by
  the CSP and by the blob being an inert, same-origin, non-scriptable PDF resource rather than by
  a sandbox attribute; this is recorded here so it is a decision, not an oversight.
- **Alternative — `<iframe>`.** Equivalent at the CSP level (both directives are granted) but
  gives no fallback content and forces the sandbox question. Rejected.
- **Alternative — pdf.js on a canvas.** Rejected in the ticket: a sizeable new runtime
  dependency plus a worker asset, to re-implement a viewer the browser already ships.

The e2e spec asserts the element renders with a `blob:` source in Chromium; the design does not
claim more than that about rendering fidelity.

### D3. CSP delta: exactly `frame-src blob:; object-src blob:`

`nginx.conf` gains those two directives and nothing else. `default-src` stays `'self'`,
`script-src` stays `'self'`, `frame-ancestors` stays `'none'`.

This is a deliberate, documented relaxation touching principle 3's boundary, so it is recorded
with its alternative and mitigation as the rules require:

- **Reason**: rendering a PDF the API already authorised and scanned requires the browser to
  treat a same-origin `blob:` as an embeddable document; there is no narrower directive.
- **Simpler alternative considered**: leaving the CSP alone and rendering with pdf.js (D2), which
  trades a two-directive relaxation for a new runtime dependency and a worker script. Judged the
  worse trade.
- **Mitigation**: `blob:` is granted only to `frame-src` and `object-src`, never to `script-src`,
  `default-src` or `connect-src`. Blobs are same-origin, created only from responses the API
  authorised, and revoked. `frame-ancestors 'none'` is untouched, so the app still cannot be
  framed by anyone. A security test asserts the served CSP matches this exact shape, so a future
  widening fails a test rather than passing silently.

### D4. Selection: primary first, fall back to newest previewable

`pickDefaultDocument` returns the document with `isPrimary === true` when it is previewable;
otherwise the previewable document with the newest `uploadedAt`; otherwise the primary document
even when not previewable, so the panel explains why rather than looking empty; otherwise
`undefined`. Ties on `uploadedAt` break on document id for determinism, which keeps the unit test
meaningful.

Previewability is `availabilityState === 'Available' && mimeType === 'application/pdf'`. The
frontend trusts the recorded `mimeType`: the API already rejected uploads whose extension and
detected content disagree, so re-sniffing in the browser would add no security and could
contradict the server.

### D5. Blob cache scoped to the page visit

Fetched blobs are kept in a `Map<documentId, string>` of object URLs for the lifetime of the
panel. Re-selecting a document already fetched does not hit the API again; every URL is revoked
on unmount and when the candidate changes.

- Rationale: fewer repeated transfers of personal data over the network, and fewer duplicate
  `document.downloaded` audit entries from a user toggling between two CVs.
- Trade-off: memory grows with the number of documents viewed in one visit, bounded by the 20 MB
  upload cap per document. Realistic usage is one or two documents per visit, so this is
  accepted over revoking on every switch.

### D6. Preview stays audited as a download

`DownloadCandidateDocumentHandler` writes a `document.downloaded` entry
([ManageCandidateDocuments.cs:97](../../../backend/Application/Features/Documents/ManageCandidateDocuments.cs#L97)).
A preview therefore produces one. This is correct — the content left the server — and is kept
rather than split into a `document.previewed` action, which would need a new endpoint or a new
parameter and a corresponding audit-contract change. The operational consequence (more audit rows
per candidate visit) is recorded in `docs/ktl-20/document-preview.md` so it is not discovered
later as a surprise.

## Stack, data, authorization and storage impact

- **Stack**: frontend only, plus one line of `nginx.conf`. No new npm or NuGet dependency. No
  backend project, endpoint, handler, command, query or contract changes.
- **Data model**: none. No entity, table, column, index, constraint or **migration**, and
  therefore no runtime grant change — `ktl_runtime`'s privileges are untouched.
- **Authorization**: unchanged and API-side. The panel calls `usePermission('documents.download')`
  to decide whether to render, which is convenience only; the control remains the
  `documents.download` check in the endpoint and handler, which returns `404`. No authorization
  logic is duplicated in the service — the service fetches, the API decides.
- **Storage**: unchanged. No new read path, no key or path exposure, no caching of the binary
  beyond the in-memory object URL, and the response stays `private, no-store`.

## Test strategy

- **Unit (Vitest + Testing Library)**: `candidate-cv-preview.logic.spec.ts` covers
  `isPreviewable` and `pickDefaultDocument` (primary previewable, primary not previewable,
  no primary, ties, empty). `candidate-cv-preview.spec.tsx` covers the rendered states, picker
  switching, cache hit, loading, error + retry, abort on unmount, object-URL revocation, and the
  panel's absence without the permission; `URL.createObjectURL`/`revokeObjectURL` are stubbed
  because jsdom does not implement them. `document.service.spec.ts` covers `openPreview` using
  the content path with the 60 s timeout and not clicking an anchor, and `download()` still
  clicking one.
- **E2E (Playwright, no Spanish-text selectors)**: `candidate-documents.spec.ts` uploads a PDF
  fixture, waits for availability, and asserts the viewer renders from a `blob:` source with no
  CSP violation logged; then selects a non-PDF document and asserts the unsupported state.
- **Security evidence**: a `tests/security/` check that the served CSP grants `blob:` only to
  `frame-src` and `object-src` and still sets `frame-ancestors 'none'`; `secure-access.spec.ts`
  asserting no panel without `documents.download`, `401` on an unauthenticated direct content
  request and `404` on an authenticated request without permission. `npm run security:rls` and
  `npm run security:storage` must still pass.
- **Backend**: no changes expected, so no new xUnit tests. If implementation ends up touching the
  API, the existing download permission matrix in `Tests/IntegrationTests` is re-run and its
  output reported before the slice is called done.

## Risks / Trade-offs

- **The CSP delta is a real widening of the frontend policy** → Confined to two directives, never
  to script or default sources, and pinned by a security test that fails if it grows.
- **Browser PDF rendering is outside our control; a user could have the built-in viewer disabled**
  → `<object>` fallback content shows the explanation and the download link, so the page degrades
  to today's behaviour instead of showing an empty box.
- **Previewing transfers up to 20 MB on page load for users who only wanted the profile** → The
  fetch happens only for a clean PDF and only for holders of the download permission; it is
  cached per visit. If bandwidth proves to be a problem in use, gating the fetch behind an
  explicit «Ver CV» button is a one-line follow-up that changes no contract.
- **Audit volume rises** (D6) → Documented in the runbook; no schema change, and the entries are
  accurate rather than noise.
- **Word CVs still cannot be previewed**, which is half of the brief's wish → Explicit Spanish
  message plus download; server-side conversion is deferred to its own ticket with its own
  dependency justification, rather than smuggled in here.

## Migration Plan

No data migration. Deployment is the SPA bundle plus the nginx image, which must be rebuilt for
the CSP change to take effect (`docker compose build nginx`), since port 4200 serves the bundle
and config baked into the image.

Rollback is reverting the frontend and `nginx.conf` changes and rebuilding; nothing persisted
changes, so there is no data to restore and no forward-compatibility concern.
