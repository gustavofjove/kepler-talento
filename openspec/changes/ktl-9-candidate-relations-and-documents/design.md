## Context

See `proposal.md` — Why. The relevant current state:

- **KTL-5 built the whole document machine and left it unwired.**
  `FileSystemDocumentStorage` already streams to quarantine with a running SHA-256 and a hard
  byte ceiling, deletes the partial file on any failure, and promotes with `File.Move(overwrite:
false)`. `DocumentStorageKey.ResolveContained` refuses any key that escapes its root.
  `ScanOperationHandler` already performs the clean/infected/error branch, promotes, marks the
  row and writes an audit event. `DocumentDownloadService.OpenCleanAsync` already refuses
  anything that is not `Clean` _and_ present in available storage, and sanitises the filename.
  `IOperationRepository.EnqueueAsync(type, correlationId, idempotencyKey, ct)` and the
  `DurableOperationWorker` already give lease-safe, restart-safe execution.
- **KTL-7 delivered the schema.** `CandidateDocument` carries `StorageKey`, `Sha256`,
  `DocumentType`, `IsPrimary`, `SourceKey`, `ScanState`, `ScanFailureCode`, `ScannerSignature`,
  `ScannedAtUtc` and a `Version` row version, with a partial unique index on
  `(CandidateId) WHERE IsPrimary`.
- **KTL-8 removes `ReferenceCandidateEndpoints.cs`**, and with it the only existing routes for
  document download (`/api/reference/documents/{id}`) and operation status
  (`/api/reference/operations/{id}`). Both must be re-homed here as real slices — this change
  cannot assume they survive.
- **The frontend has no upload path at all.** `DocumentService.upload` discards `request.file`
  after reading `name`, `type` and `size`; `createSecureUrl` returns an object URL over a
  locally generated text blob. `ApiTransport` already omits `Content-Type` for `FormData` bodies
  and already sanitises `Content-Disposition` filenames in `download()`.

Constraints that shape the approach: the 20 MB / ten-type contract is fixed by the
`private-document-storage` spec; the ClamAV instance is a shared, single-threaded-per-connection
dependency; and Nginx, Kestrel and the scanner must agree on one effective limit or the
rejection point becomes non-deterministic.

## Goals / Non-Goals

**Goals:**

- One accepted path for document content: quarantine → scan → promote, with no branch that makes
  content available without a clean verdict.
- The user can see, in Spanish, that an upload is being analysed and what happened to it.
- The invariants that were browser-side conventions — one primary CV, no duplicate language —
  become database-enforced.
- Every boundary assertion in the proposal's security section is covered by a test, not by
  review.

**Non-Goals:**

- Re-implementing storage, inspection, scanning, the worker or the reconciler. This change wires
  KTL-5's components; a change to their internals is a defect fix, not scope.
- Re-owning any KTL-7 schema. If a column or index is missing, it is raised against KTL-7.
- Re-designing the relation collection write shape. KTL-8's whole-set replacement stands; the
  brief's "add and remove per collection" is served by it (see Decision 6).
- Retention/purge automation, bulk document operations, preview or Office conversion.

## Decisions

### 1. Upload is asynchronous: `202 Accepted` plus an observable state

_This resolves the brief's deferred decision._

The upload endpoint validates, writes quarantine, inserts the row in `PendingScan`, enqueues a
`document.scan` operation with idempotency key `document:{documentId}:scan`, and returns `202`
with the document metadata including `scanState`. The SPA polls the document until it settles.

**Why not synchronous** (scan inside the request, respond `201` when clean): a 20 MB file must be
read from quarantine and streamed to ClamAV before the response. That couples the request to
scanner latency and to scanner queue depth under concurrent uploads, against `ApiTransport`'s
15 s default timeout. Worse, a timeout on the synchronous path leaves a _stored, quarantined,
unscanned_ document with no client that knows about it — the failure mode is silent rather than
visible.

**The decisive argument is not latency, it is honesty.** The brief already requires the
quarantined state to be user-visible: a document genuinely is not available the instant it
uploads. A synchronous response that returns `201 Created` would have to either lie about
availability or block; the asynchronous shape makes the interface match what is true. The
durable worker exists precisely for this, and it already survives restarts, which the
synchronous path does not.

**Cost accepted:** the SPA gains polling, and a test asserting "upload then download" needs to
wait for a state transition rather than a single response. Integration tests use the
deterministic `FakeMalwareScanner`/`MarkerMalwareScanner` so the wait is bounded and not a sleep.

### 2. Polling, not push

The SPA polls `GET /api/candidates/{candidateId}/documents/{documentId}` with a short backoff
(1 s, then 2 s, then 5 s, capped, and stopped after a bounded number of attempts) while the
state is pending, and stops on unmount. No SSE or WebSocket: a single intranet user waiting a
few seconds for one document does not justify a second transport, a second failure mode in
Nginx, and a second thing to authorize. If document volumes ever make polling a problem, the
endpoint contract does not change.

If polling exhausts its attempts, the interface says the analysis is still running and offers a
manual refresh — it never falls back to showing the document as available.

### 3. Two capabilities, checked in the slice

`documents.upload` and `documents.download` are separate from `candidates.read`/`candidates.update`,
because uploading an externally sourced binary and retrieving one are distinct risks from editing
a text field. Both are checked in the endpoint before any storage or database work, exactly as
`ReferenceCandidateEndpoints` did, and both fail closed for an unauthenticated actor. A download
refusal returns `404` rather than `403` when the caller lacks the capability _and_ the document
may not exist, so refusals do not become an existence oracle.

### 4. Validation order: cheap and total before expensive and partial

1. Authorization.
2. Declared size (`Content-Length` / multipart section length) against 20 MB — reject before
   reading.
3. Stream to quarantine with `WriteQuarantineAsync`, whose `maximumBytes` ceiling is the real
   enforcement (a declared length can lie) and which deletes the partial file on breach.
4. `IDocumentContentInspector.InspectAsync` over the quarantined file — extension and detected
   content must agree and be on the allowlist.
5. On rejection at step 4, `DeleteQuarantineIfExistsAsync` and insert nothing.

Inspection runs against the _stored_ file rather than the request stream so that what was
inspected is what will be scanned and served. The row is inserted only after steps 2–4 pass, so
"a rejected upload leaves no record and no object" is structural rather than a cleanup path.

### 5. The primary CV invariant lives in PostgreSQL

Setting a document primary runs as one transaction that clears the current primary and sets the
new one, relying on KTL-7's partial unique index to reject a concurrent second winner. The
resulting unique violation is translated to the stable conflict code — it is not caught and
retried, because a retry would silently reorder two users' intentions. This is why concurrent
primary designation is a spec scenario: the invariant is asserted against the database, not
against a mutex.

Removing the primary document leaves the candidate with **no** primary. The frontend's current
auto-promotion of `remaining[0]` is dropped: which CV is authoritative is a judgement, and a
server that silently makes it produces a wrong authoritative CV with no audit trail of the
decision. The UI prompts the user to choose instead.

### 6. Relation writes keep KTL-8's whole-set replacement

The KTL-9 brief asks for "add and remove per collection". KTL-8 shipped whole-set replacement
against the candidate's concurrency token. Replacing that with per-item slices now would cost a
second write shape, a second authorization surface and a second concurrency story for no user-
visible gain — the UI's "add" and "remove" buttons map onto a set replacement perfectly well,
and the set shape is what makes "no partial collection interleaved with another actor's change"
true. So the brief's relation scope reduces here to the one thing KTL-8 did not cover:
**uniqueness enforced server-side**.

Uniqueness is enforced by a case- and whitespace-insensitive comparison in the write path _and_
by a unique index on the normalised value, so that two concurrent writers cannot both win. The
Spanish messages are the existing ones, verbatim:

- `El candidato ya tiene este idioma registrado.`
- `El candidato ya tiene este programa registrado.`
- `El candidato ya tiene esta habilidad registrada.`

Education and experience have no duplicate rule today and gain none — two degrees from the same
institution, or two stints at the same company, are legitimate.

**Note:** if the normalising unique index does not already exist in the KTL-7 schema, it is
raised against KTL-7 rather than added here, per the non-goal above. The in-transaction check
still ships here either way.

### 7. Re-homing the reference endpoints

KTL-8 deletes `ReferenceCandidateEndpoints.cs`. This change adds, under
`Web/Features/Documents/`:

| Route                                                      | Purpose                     |
| ---------------------------------------------------------- | --------------------------- |
| `POST /api/candidates/{candidateId}/documents`             | multipart upload, `202`     |
| `GET /api/candidates/{candidateId}/documents`              | list with states            |
| `GET /api/candidates/{candidateId}/documents/{id}`         | single state, for polling   |
| `PUT /api/candidates/{candidateId}/documents/{id}/primary` | designate primary           |
| `GET /api/candidates/{candidateId}/documents/{id}/content` | permission-checked download |
| `DELETE /api/candidates/{candidateId}/documents/{id}`      | remove record and object    |

The download endpoint keeps the header discipline the reference endpoint established —
`Cache-Control: private, no-store`, `Results.File(..., enableRangeProcessing: false)` with the
sanitised filename — and adds `X-Content-Type-Options: nosniff`. Range processing stays off:
partial reads of quarantined-then-promoted content add a caching surface for no intranet benefit.

Operation status is _not_ re-exposed to the SPA. The client polls the document, not the
operation, because the document's availability is the thing the user cares about and the
operation record carries worker-internal detail. Operator visibility into failed scans stays
available through the durable-operations capability.

### 8. Deletion order: object first, then row — no, row first, then object

Removal deletes the database row in a transaction, commits, then deletes the stored object. If
the object delete fails, the reconciler reports an orphan, which is a detectable, repairable
state. The reverse order risks a record pointing at a missing object, which reads to a user as a
document that exists but cannot be opened — worse, and less detectable. This is a deliberate
choice of the failure mode that the existing reconciler already looks for.

### 9. One effective limit, declared once

20 MB is expressed as `DocumentStorageOptions.MaximumBytes`, and Nginx `client_max_body_size`
and the Kestrel/multipart limit are set from the same figure with a small envelope for multipart
framing. A startup check fails fast if the configured API limit exceeds
`DocumentStorageOptions.AbsoluteMaximumBytes`, following the existing
`ValidateAndPrepare` pattern. The frontend's 20 MB check is a courtesy that avoids a pointless
upload; it is documented in code as _not_ the boundary.

### 10. Frontend shape

`DocumentService` is rewritten against `ApiTransport`: `upload()` builds a `FormData` with the
`File`, the document type and the primary flag and posts it — no `Content-Type` is set, which
`ApiTransport` already handles. `download()` uses the transport's `download()` and triggers a
save from the returned blob and sanitised filename. `createSecureUrl` and `SecureDocumentUrl`
are deleted rather than deprecated: a placeholder that fabricates a "secure" URL is worse than
no method.

Spanish copy for the new states:

| State                   | Copy                                                                             |
| ----------------------- | -------------------------------------------------------------------------------- |
| `PendingScan`           | `En análisis` — badge, no download control                                       |
| `Clean`                 | `Disponible`                                                                     |
| `Infected` / `Rejected` | `No disponible` + `El archivo no ha superado el análisis de seguridad.`          |
| `ScanFailed`            | `Error de análisis` + `No se ha podido analizar el archivo. Inténtalo de nuevo.` |
| legacy, no binary       | `No disponible` + `Documento heredado sin archivo asociado.`                     |

The refusal copy is deliberately non-technical: it names no scanner, signature or code. The
stable code travels in the problem response for support, not in the interface.

## Risks / Trade-offs

- **ClamAV becomes load-bearing for the product, not just for tests.** → Readiness health
  already reports scanner availability (`intranet-runtime`). New uploads fail closed while it is
  down and existing clean downloads keep working, which is the operable half of fail-closed. The
  runbook gains a "scanner down" section stating that uploads are refused by design.
- **Polling adds request volume and a new class of UI bug** (a poll that never stops). →
  Bounded attempts, explicit stop on unmount, and a Vitest case asserting no request is issued
  after unmount.
- **Widening the accepted types widens the attack surface**, which is the point of the KTL-5
  decision but is still a widening. → Each newly accepted type gets an acceptance test and each
  rejected class a refusal test; extension/content agreement is mandatory; scanning limits stay
  bounded so a 20 MB archive-like document cannot become an unbounded scan.
- **A quarantined document whose scan operation is lost** would sit pending forever. → The
  durable worker's lease recovery reclaims it after a restart; the reconciler reports
  stale-quarantined objects; the UI shows it as still being analysed rather than as available.
- **The `202` shape means "upload succeeded" no longer means "CV stored and usable"**, which is
  a genuine change in what users experience. → This is stated in the release note and in the
  Spanish copy; it is a correction of a false impression the old build gave, not a regression.
- **KTL-8 and KTL-7 are both hard prerequisites and neither is landed.** → If either slips, this
  change is blocked rather than duplicating their work. The one place the boundary is thin is
  the normalising unique index for relation uniqueness (Decision 6), which is called out
  explicitly.

## Migration Plan

1. Deploy backend with the new endpoints while the SPA still uses the old service — the new
   routes are unused, and nothing regresses.
2. Confirm scanner readiness and storage readiness are green in the target environment before
   the SPA cutover; a red scanner means uploads would be refused.
3. Deploy the SPA cutover. There is no data migration: no document content exists to migrate,
   because none was ever stored. Existing document _metadata_ rows (from KTL-7's Access import
   and from KTL-8 writes) that have no binary are listed with a legacy-unavailable state.
4. **Rollback**: revert the SPA. The backend endpoints are additive and can stay. Documents
   uploaded during the window remain stored and scanned; they simply become unreachable from the
   UI until the SPA is re-deployed. Nothing is destroyed by a rollback.
5. Nginx `client_max_body_size` must be raised in the same deployment as the API limit; a
   mismatched proxy limit turns an oversized upload into an opaque `413` from the proxy instead
   of a stable problem response. Verify both after deploy with a 21 MB file.
