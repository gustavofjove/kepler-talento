## Why

KTL-5 delivered `IFileStorage`, the ClamAV `IMalwareScanner` adapter, quarantine, fail-closed
promotion, the reconciler and the durable operations worker. Every piece is proven by tests and
none of it is reachable from the product. Today
[document.service.ts](src/app/features/documents/services/document.service.ts) never stores file
content: it validates type and size, builds a `CandidateDocument` metadata record, hands it to
`CandidateService`, and discards the bytes. Users have a CV upload that stores no CV, and a
"secure URL" that resolves to a placeholder blob generated in the browser.

KTL-7 puts the `CND_` document table — storage key, SHA-256, scan state, scan failure code,
primary flag — into PostgreSQL. KTL-8 makes the application read and write candidate records,
their relation collections and document _metadata_. The bytes are still missing, and they are
the highest-risk part of the feature: externally sourced binaries carrying personal data. This
change closes that gap and finishes the candidate aggregate.

## What Changes

### Backend

- **Document upload carrying content.** A multipart slice accepts the file, validates size and
  type against the KTL-5 contract, writes it to quarantine under an application-generated opaque
  key, records the metadata row in `PendingScan`, and enqueues a durable scan operation. The
  response is `202` with the document's metadata and its scan state — never a storage path.
- **Deferred availability.** The scan runs as a durable operation through the existing
  `ScanOperationHandler`. A clean verdict promotes the binary atomically and marks the document
  available; infected, rejected, unscannable, timed-out and errored verdicts leave it
  permanently unavailable. This resolves the brief's deferred decision in favour of the
  asynchronous shape — see `design.md`.
- **Document read slices.** List a candidate's documents with their scan state, and poll a single
  document's state so the SPA can show a quarantined upload becoming available.
- **Controlled download.** A permission-checked streaming slice that serves only `Clean`
  documents, with `Content-Disposition: attachment`, a sanitised filename, `X-Content-Type-Options:
nosniff` and `Cache-Control: private, no-store`, following the header discipline in
  [ReferenceCandidateEndpoints.cs](backend/Web/Features/Candidates/ReferenceCandidateEndpoints.cs).
  A previously clean document stays downloadable while the scanner is unavailable.
- **Document removal** removes the metadata row and its stored binary, including a quarantined
  or infected one, and never leaves an orphan for the reconciler to find.
- **Exactly one primary CV per candidate**, enforced by the partial unique index KTL-7 created
  rather than by writer discipline, so the invariant holds under concurrent writes. Today it is
  an in-memory rule in `CandidateService.addDocument`.
- **BREAKING** (behaviour): the accepted content surface widens from the frontend's current
  PDF-only / 10 MB to the KTL-5 contract — 20 MB across PDF, DOC, DOCX, ODT, RTF, TXT, JPEG,
  PNG, TIFF and BMP, with extension and detected content required to agree. The API contract is
  authoritative and the frontend follows it.
- **Server-side relation uniqueness.** The duplicate rules enforced only in the browser today
  (`CandidateRelationsService` rejects a second entry for the same language, compared case- and
  whitespace-insensitively) become server-side invariants on the language, program and skill
  collections, with the same stable codes and Spanish messages.
- The capability catalogue gains `documents.upload` and `documents.download`, mapped to the
  frontend `upload_candidate_documents` and `download_candidate_documents` permissions.
- `AUD_` audit events for upload accepted, scan outcome, primary change, download and removal —
  carrying actor, document and candidate identifiers and correlation, never file content or
  filename.

### Frontend

- **BREAKING** (internal API): `DocumentService` is rewritten to send real file content as
  `FormData` through [api-transport.ts](src/app/core/http/api-transport.ts), which already
  declines to set a `Content-Type` for `FormData` bodies. `createSecureUrl` and its placeholder
  blob are removed in favour of the transport's `download()` path, which already handles
  `Content-Disposition` filename sanitisation.
- **New user-visible state.** A document is no longer available the instant it uploads. The
  document list surfaces _en análisis_, _disponible_, _no disponible_ and _error de análisis_
  in Spanish, with polling while a document is pending and an explicit, non-technical message
  when a document is refused.
- Client-side validation is realigned to the API contract (20 MB, the ten allowed types) and
  framed as a courtesy check, not the boundary.
- `CandidateRelationsService` keeps its Spanish validation messages while surfacing the
  server's refusal when a duplicate loses a race.

## Capabilities

### New Capabilities

<!-- None. The document content lifecycle already has a capability -
     private-document-storage - written by KTL-5; this change makes it reachable
     rather than introducing a parallel one. -->

### Modified Capabilities

- `private-document-storage`: the capability describes storage, scanning and download mechanics
  but never says how content is _accepted_, who may accept or retrieve it, or what a caller
  observes while a document is quarantined. It gains requirements for the upload acceptance
  operation, deferred availability with an observable scan state, per-operation upload and
  download authorization that fails closed, document removal without orphaned binaries, and
  auditing of the document lifecycle.
- `candidate-management`: the "Candidate document metadata" requirement, added by KTL-8, treats
  documents as metadata-only records that a caller attaches. It is replaced by a version in
  which a document exists only as the result of an accepted upload, its primary flag is a stored
  invariant rather than a writer convention, and its availability is a property callers can read.
- `frontend-api-transport`: the transport requirements cover JSON requests and downloads but not
  the upload of file content, and the KTL-8 candidate cutover scenario is satisfied by metadata
  alone. A requirement is added for transmitting file content without a client-set media type,
  and for representing server-side deferred availability in the interface rather than reporting
  success on acceptance.

## Impact

- **Backend**: `Application/Features/Documents/` (new upload, list, get, set-primary, download and
  remove slices), `Application/Abstractions/Documents/DocumentPorts.cs`,
  `Infrastructure/Documents/` (`FileSystemDocumentStorage`, `DocumentContentInspector`,
  `ClamAvScanner`, `DocumentDownloadService`, `ScanOperationHandler`),
  `Infrastructure/Operations/` (scan enqueue), `Web/Features/Documents/` (new endpoints),
  `Application/Abstractions/Identity/ICurrentActor.cs` (two new capabilities),
  `Infrastructure/Persistence/` (document repository, relation uniqueness checks).
- **Frontend**: `src/app/features/documents/` (service, models, upload and list components),
  `src/app/features/candidates/` (document section of the detail screen, relations service),
  `src/app/core/di/services.ts`.
- **Database**: no new tables. This change consumes the `CND_` document and relation schema,
  the partial unique primary index and the runtime grants delivered by KTL-7, and adds no schema
  of its own.
- **Runtime**: Nginx request body limit and the API multipart limit are aligned to the same
  effective 20 MB, and the ClamAV service becomes load-bearing for the product rather than for
  tests only.
- **Tests**: xUnit unit, architecture, security and disposable-PostgreSQL integration tests
  (including an EICAR-style infected upload and a scanner-down case); Vitest suites for the
  rewritten `DocumentService` and the polling document list; the candidate-profile and document
  Playwright flows.
- **Docs**: `README.md`, the published API contract, and the operational runbook's document
  storage section.

### Dependencies and sequencing

- **KTL-5** (foundation) — delivered. Supplies `IDocumentStorage`, `IDocumentContentInspector`,
  `IMalwareScanner`, quarantine and promotion, the reconciler and the durable operations worker.
  This change wires them to the product; it does not rebuild them.
- **KTL-6** (catalogs) — delivered. Supplies the catalog families relation values resolve
  against.
- **KTL-7** (`ktl-7-access-to-postgres-migration`) — **hard prerequisite.** Owns the `CND_`
  document and relation tables, the scan-state columns and the partial unique primary index.
  This change does not restate or re-own that schema.
- **KTL-8** (`ktl-8-candidate-core-writes-api-cutover`) — **hard prerequisite.** Delivers the
  candidate aggregate slices, the relation collection writes and the document metadata writes
  this change builds file content on top of. KTL-8 absorbed the relation and document-metadata
  cutover that the KTL-9 brief anticipated; the brief's relation scope survives here only as
  server-side uniqueness, and its document scope survives in full as content.
- **Blocks KTL-10** (search filters over relations).

### Personal data and security

**Highest security surface in the programme so far.** CV documents are externally sourced
binaries containing personal data, and this change is the first to accept them from a user.

- **Principle 1.** Only opaque relative keys are stored. The original filename is metadata,
  never a physical path, and is sanitised before display and before it reaches a
  `Content-Disposition` header. Document contents never reach logs, audit events or problem
  responses. Removing a document removes its binary as well as its row.
- **Principle 3, fail closed.** Content is quarantined on arrival and becomes downloadable only
  after a clean verdict. Infected, rejected, unscannable, timed-out and errored outcomes are
  terminal for availability. A scanner failure must not degrade into permissive behaviour: an
  unavailable scanner blocks _new_ availability while leaving _existing_ clean documents
  downloadable, which is the only combination that is both safe and operable.
- **Principle 3, no path exposure.** No response — success, problem or download header — may
  contain a host path, drive letter, UNC path, mount point, storage root or permanent URL. This
  is asserted by test, not by review.
- **Principle 3, authorization.** Upload and download are permission-checked per request and
  fail closed for an unauthenticated actor and for an authenticated actor lacking the
  capability. A download URL is not a capability: possessing an identifier grants nothing.
- **Widened content surface.** Moving from PDF-only/10 MB to the ten-type allowlist at 20 MB is
  a deliberate widening of what the system will accept, made in KTL-5 and honoured here. Because
  it is a widening, the security tests cover each newly accepted type as accepted _and_ each
  rejected class as refused: executables, scripts, HTML, SVG, archives submitted as CVs,
  encrypted or password-protected files, and macro-enabled Office documents (DOCM). Extension
  and detected content must agree.
- **Bounded work.** Scanning enforces the KTL-5 duration, expanded-size, recursion and
  file-count limits, so a 20 MB upload cannot become an unbounded scan.

### Assumptions and edge cases

- A candidate may hold several documents; only one may be primary, and a candidate may have
  none. Removing the primary document does not silently promote another server-side — the
  frontend's current auto-promotion becomes an explicit user action, because a server that
  quietly changes which CV is authoritative is worse than one that asks.
- A document that never reaches a clean verdict stays visible to the user as refused, with its
  reason, rather than disappearing. Silent disappearance would read as data loss.
- Scan latency for a 20 MB file is assumed to be seconds, not minutes; the asynchronous shape
  is chosen so that this assumption is not load-bearing for the request timeout.
- Bulk document operations, in-browser Office conversion and document preview stay out of
  scope, as in KTL-5.
- Authentication remains out of scope: the KTL-5 development actor stays development-only and
  production continues to fail closed without a real actor.
- Legacy documents loaded from Access by KTL-7 carry a `SourceKey` and may lack a binary. They
  are listed with an unavailable state and a distinguishable reason rather than being hidden
  or reported as an error.

### Success criteria

1. Uploaded document content is actually persisted and retrievable — the current behaviour of
   discarding bytes is gone.
2. A clean upload is quarantined, scanned, promoted, and becomes downloadable, with the
   transition visible in the interface in Spanish.
3. An EICAR-style infected upload never becomes available and raises an audit event.
4. An upload that cannot be scanned — encrypted, malformed, or scanner error or timeout — fails
   closed.
5. A previously clean document remains downloadable while the scanner is down, and no new
   document becomes available during that window.
6. Exactly one primary CV per candidate holds under concurrent writes.
7. Files above 20 MB and disallowed types are rejected at the Nginx, API and scanner boundaries,
   and each of the ten allowed types is accepted.
8. No response, header or log exposes an internal storage path or key.
9. Download fails closed without `download_candidate_documents`, and upload without
   `upload_candidate_documents`.
10. Relation add/remove succeeds through the real path for all five collections, with the
    existing duplicate rules and Spanish messages preserved and now enforced server-side.
11. Relation values resolve against catalog families; an unknown value is rejected with a stable
    code.
12. Backend unit, integration, architecture and security tests pass, plus the frontend suites and
    the candidate-profile and document Playwright flows.
