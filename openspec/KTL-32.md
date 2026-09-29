## [original]

Pre-fill the new candidate form from an uploaded CV

Add a file input to the "Nuevo candidato" page (`/app/candidates/new`). When the user picks a CV (PDF, and DOCX if cheap), the API extracts candidate data from it and returns a **draft**. The form fills only the fields that are still **empty**; anything the user has already typed is never overwritten. Nothing is saved until the user submits the form as usual, and every pre-filled value stays editable.

This ticket exists mainly to **measure how reliable rule-based extraction is** on real CVs, before deciding whether it is good enough or needs something stronger (OCR, a local model).

Fields to attempt, matching the form's inputs:

- `firstName`, `lastName`: the largest text near the top of page 1, cross-checked against the e-mail's local part. Splitting given names from surnames is the weakest step.
- `email`: regex.
- `phone`: `libphonenumber-csharp`, with Spain as the default region.
- `location`, `province`: matched against a list of Spanish municipalities and provinces, preferring the contact block and postcode lines over the experience section.
- Stretch goal, only if cheap: skills and languages matched against the existing catalogs.

Approach (no AI, no third-party service, free):

- Extraction runs **in the API**, never in the browser. Text comes from `UglyToad.PdfPig` (Apache-2.0, pure .NET), and `DocumentFormat.OpenXml` if DOCX is supported. Each new NuGet package needs its reason recorded in the design.
- The file must be **scanned by ClamAV and reported `Clean` before it is parsed**. The existing document upload (`/api/candidates/{candidateId}/documents`) needs a candidate id, which a new candidate does not have yet, so this needs its own endpoint, for example `POST /api/candidates/draft-from-document`. Whether the scan runs synchronously in the request or the draft is polled is a design decision.
- The endpoint requires `candidates.create`, checked before anything else (fail closed).
- The file and the extracted text are **not persisted and never logged**: they are personal data. The response carries only the suggested field values, each with a confidence level (for example `high`/`low`), so the form can highlight the values the user should check.
- Scanned PDFs with no text layer return an empty draft and a localized message telling the user to fill the form in by hand.

Open question: should the uploaded CV also be attached to the candidate as a document when the form is saved, or does the user upload it again from the candidate page? The simplest option for this ticket is not to attach it and to leave that for a follow-up.

For the reliability test, each extraction should leave a way to compare suggested values with what the user finally saved (for example a per-field "kept / edited / empty" count), **without storing any personal values**. If that is too much for this ticket, a manual test sheet over a set of sample CVs in `docs/ktl-32/` is enough. Sample CVs must never be committed.

Out of scope: OCR, AI models or paid parsing services, experience and education extraction, bulk import from CVs, and editing an existing candidate from a CV.
