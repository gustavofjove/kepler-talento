## Context

See proposal.md for the motivation. The requirements are in `specs/candidate-cv-draft/spec.md` and
the create-page delta in `specs/candidate-profile-pages/spec.md`.

Current state that shapes the approach:

- Document upload (`POST /api/candidates/{candidateId}/documents`, `UploadCandidateDocument.cs`)
  writes to quarantine, records a pending document and queues a `document.scan` durable operation.
  It needs a candidate id, and its scan is asynchronous. KTL-17's import upload follows the same
  quarantine → operation → promote pattern.
- `IMalwareScanner.ScanAsync(Stream)` (`ClamAvScanner`, INSTREAM over TCP, 60 s timeout) and
  `IDocumentContentInspector.InspectAsync(fileName, Stream)` (extension/magic/zip checks, rejects
  `vbaProject.bin` and `EncryptedPackage`) are reusable ports that take a stream, not a stored key.
- `DocumentStorageOptions` holds the 20 MB limit and the Kestrel body cap; the upload endpoint
  checks authorization before `ReadFormAsync`.
- `AuditEvent` takes a free `SubjectId` string, and event types must be in the closed
  `AuditEventTypes.All` catalogue.
- `CandidateForm` owns its draft in `useState`, initialised once from props; the create page
  renders it without a `formId`. Location and province are free-text columns; there is no
  municipality or province reference data in the repository.
- No rate or concurrency limiter is configured in `Program.cs` today.

## Goals / Non-Goals

**Goals:**

- A synchronous request/response draft: pick a file, get suggestions in a few seconds.
- The same safety gates as a stored document (auth first, allowlist, size, clean scan before any
  parse), with nothing persisted.
- Extraction rules isolated behind one port so they can be measured against a local corpus and
  later replaced (OCR, local model) without touching the endpoint or the SPA.

**Non-Goals:**

- A stored reliability metric. Measurement is done with a local, git-ignored corpus harness and a
  manual test sheet (D9); a per-field "kept/edited" counter would need a table, a migration and a
  new data category for a one-off experiment.
- Attaching the CV to the candidate on save (brief's open question, deferred to a follow-up).
- Skills and languages from catalogs (the brief's stretch goal): dropped to keep the experiment
  focused on the six core fields.

## Decisions

### D1. Synchronous scan inside the request, not a polled draft

The endpoint buffers the upload, scans it with `IMalwareScanner`, and parses on `Clean`, all in
one request. A polled draft would need a stored quarantine object, a draft row, a durable
operation, a cleanup job and a polling UI — i.e. the very persistence the brief forbids for this
data. A CV scan takes well under the 60 s scanner timeout, and the SPA shows a busy state.

_Alternative:_ reuse the `document.scan` operation with a temporary candidate. Rejected: creates a
record the user never asked for and couples the draft to the document lifecycle.

### D2. Bytes live in a bounded in-memory buffer only

The file is copied from the multipart section into a `MemoryStream` capped at
`DocumentStorageOptions.MaximumBytes` (20 MB) and disposed at the end of the request. It is never
written to the quarantine or available roots, so there is no key, no path and no reconciliation
concern. ASP.NET Core's form reader buffers large files to a temp file; to avoid a disk copy the
endpoint reads the body with `MultipartReader` directly (the section stream is copied into the
capped buffer) instead of `ReadFormAsync`. The in-memory "quarantine" is the buffer before the
clean verdict: only the inspector (magic bytes) and the scanner read it before `Clean`.

_Departure check (principle 3, "binaries … remain quarantined until clean"):_ honoured in intent —
nothing parses unscanned content and nothing becomes available at all. The simpler alternative
(writing to quarantine and deleting after) was rejected because it creates an on-disk copy of
personal data that a crash could orphan.

### D3. Concurrency and work bounds

- A singleton `CvDraftGate` (a `SemaphoreSlim` of `MaxConcurrent` slots, no queue) entered
  inside the endpoint **after** the permission check caps memory at ~4 × 20 MB. A full gate throws
  the new `TooManyRequestsException` → 429 ProblemDetails `cv_draft.busy` with `Retry-After`.
  _Implementation change:_ the design first named the shared-framework rate-limiting middleware,
  but that middleware runs before the endpoint, so a saturated limiter would answer an
  unauthenticated caller with 429 instead of refusing it as unauthorized. The in-endpoint gate
  keeps "permission first" true under load and needs no middleware ordering.
- Options (`CvDraftOptions`, section `CvDraft`): `MaxConcurrent` 4, `MaxPdfPages` 5,
  `MaxCharacters` 50 000, `TimeBudgetSeconds` 20 (scan excluded; the scanner has its own timeout).
  Extraction runs under a linked `CancellationTokenSource`; exceeding the budget returns
  `cv_draft.too_complex` (422).
- PDF: only the first `MaxPdfPages` pages are opened. DOCX: only `word/document.xml` is streamed
  with `XmlReader` (DTD processing prohibited, `MaxCharactersInDocument` set), stopping at
  `MaxCharacters`.

### D4. Endpoint and contract

`Web/Features/Candidates/CandidateDraftEndpoints.cs`, `MapCandidateDraftEndpoints()` registered in
`Program.cs`, group `/api/candidates` with a single `POST /draft-from-document`
(`.WithName("CreateCandidateDraftFromDocument")`, `.DisableAntiforgery()`,
no rate-limiting middleware). Order inside the delegate, mirroring `DocumentEndpoints`:

1. `candidates.create` check (`ForbiddenException` → 403) — before touching the body; then the
   `CvDraftGate` slot (D3).
2. `Content-Length` > body cap → 400 `cv_draft.size.exceeded`; non-multipart / no `file` part →
   400 `cv_draft.file.missing`.
3. Read the `file` section into the capped buffer; empty → 400 `cv_draft.file.empty`.
4. Dispatch `CreateCandidateDraftCommand(fileName, buffer)` through `ISender`.

Handler (`Application/Features/Candidates/CvDraft/CreateCandidateDraft.cs`) repeats
`CandidateGuards.RequireCreate(actor)`, then:

| Step                                    | Refusal | HTTP | Code                           |
| --------------------------------------- | ------- | ---- | ------------------------------ |
| Extension not `.pdf`/`.docx`            | yes     | 400  | `cv_draft.format.unsupported`  |
| `IDocumentContentInspector` rejects     | yes     | 400  | `cv_draft.content.rejected`    |
| Scan `Infected`                         | yes     | 422  | `cv_draft.rejected`            |
| Scan `Error` (timeout/unavailable)      | yes     | 503  | `cv_draft.scanner_unavailable` |
| Parser throws (malformed/encrypted PDF) | yes     | 422  | `cv_draft.unreadable`          |
| Time budget exceeded                    | yes     | 422  | `cv_draft.too_complex`         |
| No text                                 | no      | 200  | `cv_draft.no_text`             |
| Otherwise                               | no      | 200  | `cv_draft.extracted`           |

`cv_draft.rejected` deliberately does not say "virus": the SPA shows one "could not be processed"
message for rejected/unreadable and never a signature. 400 refusals use the existing
`RequestValidationException`. `Application/Common/Errors` has no 422 or 503 type today, so this
slice adds `UnprocessableException(code, message)` and `ServiceUnavailableException(code,
message)` next to `ConflictException`, mapped to 422 and 503 ProblemDetails by
`GlobalExceptionHandler` (503 also sets `Retry-After`). Messages are Spanish and generic.

Response:

```json
{
  "draftId": "0199…",
  "outcome": "cv_draft.extracted",
  "fields": {
    "firstName": { "value": "Lucía", "confidence": "high" },
    "email": { "value": "lucia…@…", "confidence": "high" }
  }
}
```

`fields` holds only the keys with a suggestion; the key set is fixed to the six form fields.
Responses carry `Cache-Control: no-store`.

### D5. Extraction behind one port

`Application/Abstractions/CvExtraction/ICvTextReader` (`Task<CvText> ReadAsync(Stream, kind,
bounds, ct)` returning ordered lines with page index and, for PDFs, font size) and
`ICandidateDraftExtractor` (`CandidateDraft Extract(CvText)`). Implementations live in
`Infrastructure/CvExtraction/`:

- `PdfCvTextReader` — PdfPig; groups words into lines per page (`page.GetWords()` → lines by
  baseline), keeps the letter point size so "largest text near the top" is computable.
  `ParsingOptions { UseLenientParsing = false }`; encrypted documents throw → `cv_draft.unreadable`.
- `DocxCvTextReader` — `ZipArchive` + `XmlReader` over `word/document.xml`, one line per `w:p`;
  heading/size info from `w:sz` on the run where present, else paragraph order only.
- `RuleBasedCandidateDraftExtractor` — pure, no I/O, fully unit-testable:
  - **email**: RFC-lite regex over the text; first match in document order; `high`.
  - **phone**: `PhoneNumberUtil.FindNumbers(text, "ES")` with `Leniency.VALID`; first valid match;
    formatted E.164 for non-ES, national format with spaces for ES; `high` if it appears in the
    first 25 % of lines, else `low`.
  - **name**: candidate lines = the 3 largest-font (PDF) or first non-empty (DOCX) lines in the top
    third of page 1 that are 2–5 alphabetic words, excluding lines that are headers from a stop
    list (`curriculum`, `vitae`, `cv`, `perfil`, …). Split: first word → `firstName`, remaining →
    `lastName`, except when the e-mail's local part (split on `.`, `_`, `-`, digits) matches two
    words, which then fixes the boundary. Corroborated by the e-mail → `high`, else `low`.
    Spanish compound given names ("María José") are the known weak spot and are what the corpus
    measures.
  - **postcode → province**: a 5-digit `0[1-9]|[1-4]\d|5[0-2]` token; its first two digits are
    the INE province code — deterministic.
  - **location**: the municipality on the postcode line or within ±1 line of it (accent- and
    case-insensitive match against the reference list), preferring the top 30 % of lines; if no
    postcode, a municipality match in the top 30 % → `low`. Municipality consistent with the
    postcode's province → `high`.
  - **province** without a postcode: a province name match in the top 30 % → `low`.

_Alternative:_ one class doing I/O and rules. Rejected: the rules are the thing being measured and
must run in unit tests and the corpus harness without PDFs.

### D6. Spanish reference data as an embedded resource

`Infrastructure/CvExtraction/Resources/es-municipalities.csv` (INE code, municipality name,
province code; ~8 100 rows, ~250 KB) and the 52 provinces with their official and common names
("Vizcaya"/"Bizkaia", "A Coruña"/"La Coruña"). Source: INE "Relación de municipios y sus códigos
por provincias", public data reusable under INE's terms (attribution recorded in
`docs/ktl-32/extraction-rules.md`). Loaded once into a normalised lookup (lower-case, accents
stripped) by a singleton. It is reference data, not personal data, and not a dependency.

### D7. New dependencies (principle 2)

- **`PdfPig` 0.1.16** (`UglyToad.PdfPig`, Apache-2.0, pure managed, no native code, ~34 M
  downloads). Reason: there is no PDF text extraction in the BCL; alternatives are native
  (pdfium/poppler wrappers — native binaries in the container) or AGPL/commercial (iText). Added
  to `Infrastructure` only.
- **`libphonenumber-csharp` 9.0.40** (Apache-2.0, port of Google's libphonenumber). Reason:
  finding and validating phone numbers in free text across formats (`+34`, `0034`, spaces, dots,
  foreign numbers) is exactly what it does; a hand regex would be the weakest field. Added to
  `Infrastructure` only.
- **Not added: `DocumentFormat.OpenXml`.** DOCX paragraphs are read with `ZipArchive` + `XmlReader`,
  which the inspector already uses; the SDK would add ~5 MB for one XML part.

Both pinned in `backend/Directory.Packages.props` with a comment pointing to this decision, as
KTL-16 did.

### D8. Audit, logging and personal data

- New catalogued type `CandidateAuditEvents.CvDraftExtracted = "candidate.cv_draft.extracted"`,
  added to `AuditEventTypes.All`. Subject = draft id (`Guid.CreateVersion7()`, `N` format), outcome
  = the outcome/refusal code, actor = internal user id. Written for every attempt that passed the
  permission check and got as far as the handler (success, no text, and scan/parse refusals), so
  an operator can count attempts and outcomes. No schema change: `AUD_` rows already take a free
  subject and outcome code. The audit viewer shows it like any other type (its label key goes into
  `es.json`).
- The endpoint logs one information line per attempt: draft id, outcome code, elapsed
  milliseconds. Never the filename, text or values. (It is the endpoint rather than the handler
  because the Application layer has no logging dependency; the endpoint mints the draft id and
  passes it in the command so the log line and the audit row carry the same id.) Parser exceptions are caught and logged by type name
  only (PdfPig exception messages can quote document content).
- An integration test (`CandidateDraftApiTests`) captures the host's log events and asserts that
  no filename or suggested value appears in any of them across the success and refusal paths.

### D9. Reliability measurement without storing personal data

- `backend/Tests/CvCorpus/` (git-ignored, added to `.gitignore`): `*.pdf`/`*.docx` sample CVs and
  a `expected.json` with the true values per file. Never committed.
- An xUnit test class `CvCorpusReport` in `IntegrationTests`, `[Trait("Category", "CvCorpus")]`,
  skipped when the folder is absent (so CI never needs it). It runs the real readers and extractor
  on each file and writes an aggregate table (per field: correct / wrong / missing, per
  confidence) to the test output and to `backend/Tests/CvCorpus/report.md` (also ignored). It
  prints no values, only file index and counts.
- `docs/ktl-32/extraction-test-sheet.md` describes how to assemble the corpus, run it
  (`dotnet test … --filter "Category=CvCorpus"`), and holds the summary counts from the operator's
  run — the data the decision "good enough vs. OCR/local model" is made on.

### D10. SPA: prefill that never overwrites

- `features/candidates/services/candidate-draft.service.ts` posts `FormData` through `api-transport`
  (as `document.service.ts` does) and returns the typed draft; the service lives in
  `services.ts` and is reached through `useServices()` (it is called from an event handler, not
  read during render).
- `CandidateForm` gets an optional `suggestion?: { nonce: number; fields: CvDraftFields }` prop. An
  effect keyed on `nonce` merges with `setDraft(current => …)`, filling a key only when
  `current[key].trim() === ''` — decided against the live state, so a keystroke racing the
  response is never overwritten. It records which keys it filled (and their confidence) in local
  state; a key leaves that set as soon as its value differs from the suggested value. The pure
  merge lives in `candidate-form.logic.ts` (`applySuggestion(draft, fields)` → `{ draft, filled }`).
- Each filled field shows a small `span.badge` «Sugerido del CV» (and «Revisar» for `low`), wired
  with `aria-describedby` so screen readers announce it; the input gets a `suggested` class for
  the Kepler highlight token.
- `components/cv-draft-picker.tsx` above the form in `candidate-create-page.tsx`: a labelled
  `input type="file" name="cvFile" accept=".pdf,.docx" data-testid="cv-draft-file"`, busy state
  while in flight, and a status line (`role="status"`) with the outcome message:
  extracted (with count of fields filled), `no_text` ("no se ha podido leer texto… rellénalo a
  mano"), refused/unreadable, scanner unavailable (retry), busy (retry). Errors go through
  `useErrorToast()` only for unexpected failures; known codes map to `es.json` keys via
  `TranslatableError`. The picker is rendered under the route's existing create-permission guard;
  no new permission is consulted in the UI.
- The form is dirty after a prefill (the existing dirty tracking already sees the change), which
  is correct: the user has unsaved values.
- All copy under `candidate.cvDraft.*` in `es.json`; no hardcoded JSX strings.

### D11. Tests

- **Unit (xUnit)**: extractor rules table-driven over `CvText` fixtures built in code with
  synthetic names (no real CVs); province-from-postcode; name/e-mail corroboration; phone formats;
  bounds; DOCX reader over an in-memory generated DOCX; PDF reader over a PDF generated in the test
  with PdfPig's `PdfDocumentBuilder` (text and image-only variants); handler with hand-written
  doubles for scanner/inspector/readers: permission-first, scan-before-parse (reader double
  asserts it is never called on non-clean verdicts), every refusal code, audit written without
  values. The log check is an integration test, because the endpoint writes the log line.
- **Integration (Testcontainers + `WebApplicationFactory`)**: 401 unauthenticated, 403 for
  `candidates.read`-only and `documents.upload`-only actors with a scanner double proving nothing
  was scanned; 200 happy path with `MarkerMalwareScanner`; infected marker → 422 and no
  `CND_`/document rows or storage objects; 429 when the limiter is saturated; audit row shape.
- **Frontend (Vitest)**: `applySuggestion` logic (never overwrites, trims, confidence); form
  renders badges and drops them on edit; picker states per outcome with a transport double.
- **E2E (Playwright)**: `tests/e2e/candidate-cv-draft.spec.ts` uploads a committed synthetic PDF
  fixture about a fictitious person (no real personal data), asserts the fields are filled and
  badged, then replaces the first name with a value carrying the `Date.now()` marker before saving,
  so the teardown purges the record. A second test types a first name before picking the CV and
  asserts it is kept.

## Risks / Trade-offs

- [Rule-based name splitting is unreliable for compound Spanish names] → confidence `low` unless
  corroborated, «Revisar» badge, and the corpus harness quantifies it; that number is the
  deliverable.
- [Synchronous scan ties up a request for seconds; ClamAV down blocks the feature] → busy state,
  503 with a retry message, concurrency cap; creating a candidate manually is unaffected.
- [Memory pressure from 20 MB buffers] → concurrency limit 4 with no queue.
- [Malicious PDF crafted to hang the parser after a clean scan] → page/character caps, time
  budget with cancellation, strict (non-lenient) parsing, parser exceptions caught.
- [PdfPig exception messages or stack traces could carry document text into logs] → catch and log
  the exception type only; `GlobalExceptionHandler` never sees parser exceptions.
- [Suggested values in the response are personal data in the browser] → same exposure as the
  typed form; `no-store`; nothing kept in `localStorage`.
- [INE list drifts (municipality merges)] → reference data only affects suggestions; refresh
  procedure noted in `docs/ktl-32/extraction-rules.md`.

- [The API image (Alpine, no ICU) runs .NET in globalization-invariant mode: named cultures
  throw and `string.Normalize(FormD)` does not strip accents] → found during manual testing;
  casing is invariant, accent folding uses an explicit Latin table, and the unit test project
  now sets `InvariantGlobalization` so tests run under the container's globalization.

## Migration Plan

No database migration and no new grants. Deploy is a normal API + SPA release; the
`CvDraft` options have safe defaults. Rollback: redeploy the previous images — no state to undo.
Before the Production release, an operator runs the corpus harness and records the counts in the
test sheet.

## Open Questions

- Threshold for calling extraction "good enough" (e.g. ≥90 % correct e-mail/phone, ≥70 % name) —
  a product decision taken after reading the corpus report; it does not change this slice.
