## Why

Recruiters retype the name, e-mail, phone and location of every new candidate from the CV they
already have open. KTL-32 adds a CV picker to «Nuevo candidato» that proposes those values, and —
just as importantly — is the experiment that tells us whether **rule-based, local, free
extraction** is reliable enough on real CVs or whether a later ticket needs OCR or a local model.

## What Changes

- New API operation `POST /api/candidates/draft-from-document` (multipart, one `file`). It
  requires `candidates.create`, checked before the body is read. It accepts PDF and DOCX only,
  applies the existing size limit and extension/content allowlist check, scans the bytes with
  ClamAV **synchronously** and parses them only after a `Clean` verdict. It returns a _draft_:
  suggested values for `firstName`, `lastName`, `email`, `phone`, `location`, `province`, each
  with a confidence of `high` or `low`, plus an opaque draft identifier and an outcome code.
- The file and the extracted text are held in memory for the request only: nothing is written to
  storage or the database, and neither the file, its name nor any extracted value is logged or
  audited. One audit event per attempt records actor, draft id and outcome code only.
- Infected, unscannable, encrypted, mismatched or unsupported files are refused with stable
  codes; a scanner that is unavailable or times out fails closed (no extraction). A PDF with no
  text layer returns an empty draft with the `cv_draft.no_text` outcome so the page can tell the
  user to fill the form by hand.
- The extraction work is bounded: a concurrency limit on the endpoint, a page cap for PDFs, a
  cap on extracted characters and an overall time budget.
- «Nuevo candidato» gets a CV picker above the form. The returned values fill **only empty
  fields**; typed values are never overwritten. Filled fields are marked «Sugerido del CV» and
  low-confidence ones «Revisar». Every value stays editable, and nothing is saved until the user
  submits the form as today.
- The uploaded CV is **not** attached to the candidate on save (open question in the brief,
  resolved as "not in this ticket"); the user uploads it from the candidate page as today.
- Reliability measurement: a git-ignored local corpus harness (sample CVs plus expected values
  that never leave the operator's machine) produces per-field hit/miss/wrong counts, and
  `docs/ktl-32/` gets the test sheet and the results summary with counts only.
- New NuGet dependencies, each justified in the design: `UglyToad.PdfPig` (PDF text) and
  `libphonenumber-csharp` (phone parsing). DOCX is read with the framework's `ZipArchive` and
  `XmlReader`, so `DocumentFormat.OpenXml` is **not** added. A Spanish municipality/province
  reference list (INE public data) ships as an embedded resource, not a package.

## Capabilities

### New Capabilities

- `candidate-cv-draft`: extracting a suggested candidate draft from an uploaded CV — the
  operation, its authorization, scan-before-parse gate, non-persistence and no-logging rules,
  per-field confidence, the empty draft for image-only PDFs, resource bounds, and how the create
  form applies a draft without overwriting typed values.

### Modified Capabilities

- `candidate-profile-pages`: "Candidate creation continues on the candidate page" — the create
  page content is no longer only the core form and the post-save note; it also offers the CV
  picker (shown only to holders of `candidates.create`, which the page already requires).

## Impact

- **Personal data: yes.** A CV and every value extracted from it are personal data. Principle 1
  is upheld by holding the bytes and text in request memory only, returning only the six
  suggested values, never logging or auditing file names or values (the redaction enricher stays
  in place; the handler logs identifiers and outcome codes only), and never storing the file.
- **Storage access / scanning: yes, but no stored objects.** Principle 3 is upheld by keeping the
  scan-before-parse gate (no parser touches bytes the scanner has not reported `Clean`) and
  failing closed on any non-clean verdict. No quarantine object, storage key or path exists for a
  draft, so none can leak.
- **Roles / permissions: no new permission.** The operation reuses `candidates.create`, checked
  first in the endpoint and again in the handler. No RLS or database grants change; no migration.
- **Audit:** one new catalogued event type (`candidate.cv_draft.extracted`) whose subject is the
  opaque draft id, never a candidate id or a value.
- **Backend:** `Application/Features/Candidates/CvDraft/`, `Infrastructure/CvExtraction/`,
  `Web/Features/Candidates/CandidateDraftEndpoints.cs`, `Directory.Packages.props`,
  `AuditEventTypes`, an embedded municipality resource.
- **Frontend:** `candidate-create-page.tsx`, `candidate-form.tsx` (prefill that respects typed
  values, suggestion markers), a new `cv-draft` API/service, `es.json` keys.
- **Docs:** `docs/ktl-32/` (API contract, extraction rules, test sheet, release notes), README.
- **Out of scope:** OCR, AI models or paid services, experience/education/skills/languages
  extraction, bulk creation from CVs, editing an existing candidate from a CV, attaching the CV on
  save, and a stored per-field "kept/edited" metric.
