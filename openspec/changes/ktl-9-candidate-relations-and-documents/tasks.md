## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-9` from an up-to-date `main` that already contains
      KTL-7 and KTL-8; abort and report if either prerequisite is absent
- [x] 0.2 Confirm the `CND_` document table exposes `StorageKey`, `Sha256`, `ScanState`,
      `ScanFailureCode`, `ScannerSignature`, `ScannedAtUtc`, `IsPrimary` and the partial unique
      primary index; raise any gap against KTL-7 rather than adding schema here

## 1. Capabilities and authorization

- [ ] 1.1 Add `documents.upload` and `documents.download` to the backend permission constants
      alongside the existing candidate capabilities
- [ ] 1.2 Map them onto the frontend `upload_candidate_documents` and
      `download_candidate_documents` permission names in the permission model
- [ ] 1.3 Unit-test that an unauthenticated actor and an actor without each capability are
      refused, and that `candidates.read` alone grants neither upload nor download

## 2. Upload slice

- [ ] 2.1 Add `Application/Features/Documents/UploadCandidateDocument` — command, handler and
      response carrying document metadata and scan state, never a storage key
- [ ] 2.2 Generate the opaque storage key and stream the content through
      `IDocumentStorage.WriteQuarantineAsync` with `DocumentStorageOptions.MaximumBytes` as the
      ceiling, so an oversized or empty body is refused mid-stream and the partial file removed
- [ ] 2.3 Run `IDocumentContentInspector.InspectAsync` against the quarantined file; on refusal
      call `DeleteQuarantineIfExistsAsync` and insert no row
- [ ] 2.4 Insert the `CandidateDocument` row in `PendingScan` with the recorded size and SHA-256
      only after validation passes, honouring the requested primary flag
- [ ] 2.5 Enqueue the scan through `IOperationRepository.EnqueueAsync("document.scan", correlationId,
    $"document:{id}:scan", ct)` in the same unit of work as the row insert
- [ ] 2.6 Add `POST /api/candidates/{candidateId}/documents` in `Web/Features/Documents/`,
      returning `202` with the metadata; reject before reading the body when the declared length
      exceeds the limit
- [ ] 2.7 Unit-test rejection paths leave no row and no stored object: oversized, empty,
      disallowed type, extension/content mismatch, missing file part

## 3. Read, primary and removal slices

- [ ] 3.1 Add the list slice `GET /api/candidates/{candidateId}/documents` returning each
      document's metadata and availability state, including legacy rows with no binary
- [ ] 3.2 Add the single-document slice `GET /api/candidates/{candidateId}/documents/{id}` for
      polling
- [ ] 3.3 Add `PUT /api/candidates/{candidateId}/documents/{id}/primary` as one transaction that
      clears the current primary and sets the new one, translating the partial-unique-index
      violation to the stable conflict code without retrying
- [ ] 3.4 Add `DELETE /api/candidates/{candidateId}/documents/{id}`: delete the row and commit,
      then delete the stored object from whichever of quarantine or available holds it; leave no
      primary behind and promote nothing automatically
- [ ] 3.5 Ensure no response field from any of these slices carries a storage key, path, root or
      permanent URL

## 4. Download slice

- [ ] 4.1 Add `GET /api/candidates/{candidateId}/documents/{id}/content` backed by
      `IDocumentDownloadService.OpenCleanAsync`, re-homing the behaviour lost with
      `ReferenceCandidateEndpoints.cs`
- [ ] 4.2 Check `documents.download` before any storage or database work; return `404` for an
      unauthorized or unknown document so the refusal is not an existence oracle
- [ ] 4.3 Set `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff` and
      `Content-Disposition: attachment` with the sanitised filename; disable range processing
- [ ] 4.4 Unit-test that pending, infected, rejected, scan-failed and missing-binary documents
      are all refused, and that a clean document streams its exact bytes

## 5. Scan wiring and terminal refusal

- [ ] 5.1 Confirm `ScanOperationHandler` is registered with the `DurableOperationWorker` for the
      `document.scan` type in the composition root
- [ ] 5.2 Make refusal terminal: a document already `Infected`, `Rejected` or `ScanFailed` is not
      re-promoted by a later scan, restart or reconciliation run
- [ ] 5.3 Verify an unavailable scanner blocks new availability while existing `Clean` documents
      remain downloadable, and that readiness health reports the scanner state

## 6. Relation uniqueness

- [ ] 6.1 Enforce case- and whitespace-insensitive uniqueness of the referenced value within a
      candidate's language, program and skill collections in the relation write path
- [ ] 6.2 Return the existing Spanish messages verbatim — `El candidato ya tiene este idioma
    registrado.`, `El candidato ya tiene este programa registrado.`, `El candidato ya tiene
    esta habilidad registrada.` — with stable validation codes
- [ ] 6.3 Confirm the normalising unique index backing concurrent writers exists in the KTL-7
      schema; if it does not, raise it against KTL-7 and record the gap in this change

> KTL-7 gap confirmed on 2026-09-10: `CND_CandidateLanguages`,
> `CND_CandidatePrograms` and `CND_CandidateSkills` have candidate lookup and source-key
> indexes, but no unique `(CandidateId, referenced catalog Id)` index. Per the KTL-9 design,
> this change does not add or re-own that schema. Concurrent duplicate writers cannot be
> signed off until KTL-7 is corrected and redeployed.

- [ ] 6.4 Leave education and experience without a duplicate rule, and add a test asserting two
      degrees from the same institution are accepted

## 7. Auditing

- [ ] 7.1 Record `AUD_` events for upload accepted, scan outcome, primary change, download and
      removal, each carrying actor, document identifier, candidate identifier, kind, outcome and
      correlation
- [ ] 7.2 Assert by test that no audit event contains file bytes, the original filename, or a
      storage key or path
- [ ] 7.3 Confirm no audit event describing an applied change is written for a refused upload

## 8. Limits and runtime configuration

- [ ] 8.1 Express 20 MB once via `DocumentStorageOptions.MaximumBytes` and derive the Kestrel and
      multipart limits from it, with a small envelope for multipart framing
- [ ] 8.2 Raise Nginx `client_max_body_size` to match in the same deployment configuration
- [ ] 8.3 Extend the startup safety check so a configured API limit above
      `DocumentStorageOptions.AbsoluteMaximumBytes` fails fast, following `ValidateAndPrepare`

## 9. Frontend document service

- [ ] 9.1 Rewrite `DocumentService.upload` to post a `FormData` carrying the `File`, the document
      type and the primary flag through `ApiTransport`, setting no `Content-Type`
- [ ] 9.2 Add `list`, `get`, `setPrimary`, `remove` and `download` against the new endpoints;
      `download` uses the transport's `download()` and saves the returned blob under the
      sanitised filename
- [ ] 9.3 Delete `createSecureUrl` and the `SecureDocumentUrl` model, and remove every consumer
      of the placeholder blob
- [ ] 9.4 Realign the client-side checks to 20 MB and the ten allowed types, commenting in code
      that this is a courtesy check and the API is the boundary
- [ ] 9.5 Surface the server's refusal message when a relation duplicate loses a race, keeping
      the local Spanish messages for the checks the client still makes

## 10. Frontend scan-state experience

- [ ] 10.1 Render the availability states in the document list with the Spanish copy from
      `design.md` — `En análisis`, `Disponible`, `No disponible`, `Error de análisis`, and the
      legacy no-binary case — with correct accents
- [ ] 10.2 Poll a pending document with the bounded backoff from `design.md`, stopping on
      unmount and on a settled state
- [ ] 10.3 On exhausted attempts, report that the analysis is still running and offer a manual
      refresh; never show a pending document as available
- [ ] 10.4 Hide the download control for any non-available document, and show the non-technical
      refusal explanation without naming scanner, signature or code
- [ ] 10.5 Prompt the user to choose a new primary CV after removing the primary one, rather than
      promoting silently

## 11. Update existing tests

- [ ] 11.1 Review and update the existing `DocumentService` Vitest suite for the API-backed
      service, removing assertions that depended on the discarded-bytes behaviour and the
      placeholder secure URL
- [ ] 11.2 Update the candidate detail and document component tests for the asynchronous upload
      and the new states
- [ ] 11.3 Update `DocumentStorageTests` and any KTL-5 unit tests whose expectations change now
      that the components are reached from real slices
- [ ] 11.4 Update the architecture tests to cover the new `Application/Features/Documents` and
      `Web/Features/Documents` projects' dependency direction

## 12. Security tests

- [ ] 12.1 Accept each of the ten allowed types (PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF,
      BMP) with agreeing extension and content
- [ ] 12.2 Refuse each rejected class: executable, script, HTML, SVG, archive submitted as a CV,
      encrypted or password-protected file, and DOCM
- [ ] 12.3 Refuse a file whose extension and detected content disagree
- [ ] 12.4 Upload an EICAR-style file end to end: it never becomes downloadable and its detection
      is audited
- [ ] 12.5 Assert no response body, header or log line from any document endpoint contains a host
      path, drive letter, UNC path, mount point, storage root or storage key
- [ ] 12.6 Assert upload and download fail closed for an unauthenticated actor and for an actor
      missing the specific capability

## 13. Run the suites

- [ ] 13.1 Run the backend unit tests and report the result
- [ ] 13.2 Run the backend integration tests against disposable PostgreSQL, including concurrent
      primary designation and concurrent duplicate relation writes, and verify the resulting
      database and storage state (row present, object in the expected root, no orphan)
- [ ] 13.3 Run the architecture and security suites and report the result
- [ ] 13.4 Run the frontend Vitest suites and report the result
- [ ] 13.5 Verify the legacy Supabase integration checks for unchanged legacy paths still pass

## 14. End-to-end verification

- [ ] 14.1 Run the candidate-profile Playwright spec and report the result
- [ ] 14.2 Run the document Playwright flow: upload a real PDF, observe `En análisis` become
      `Disponible`, download it, and confirm the downloaded bytes match the uploaded file
- [ ] 14.3 Run a Playwright case uploading an oversized file and confirm the Spanish rejection
      message appears rather than an opaque proxy error, with a 21 MB file exercising the Nginx
      limit
- [ ] 14.4 Restore seed data after the e2e run
- [ ] 14.5 Run `npm run e2e` in full and report the result

## 15. Documentation

- [ ] 15.1 Update `README.md` with the document upload/download flow and the two new capabilities
- [ ] 15.2 Regenerate and review the published API contract for the six new document routes
- [ ] 15.3 Add a "scanner unavailable" section to the operational runbook stating that new
      uploads are refused by design while existing clean documents stay downloadable
- [ ] 15.4 Record in the release note that a successful upload now means "accepted and being
      analysed", not "immediately available"
