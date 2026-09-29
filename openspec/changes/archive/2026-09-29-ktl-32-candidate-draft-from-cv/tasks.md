## 0. Create Feature Branch

- [x] 0.1 Create branch `feat/KTL-32` from an up-to-date `main` (leave the unrelated
      `search-filters.css` working-tree change out of this branch's commits)

## 1. Dependencies, options and reference data

- [x] 1.1 Pin `PdfPig` 0.1.16 and `libphonenumber-csharp` 9.0.40 in
      `backend/Directory.Packages.props` with a comment citing design D7; reference both from
      `backend/Infrastructure` only (no versions in `.csproj`)
- [x] 1.2 Add `CvDraftOptions` (`MaxConcurrent`, `MaxPdfPages`, `MaxCharacters`,
      `TimeBudgetSeconds`) bound from the `CvDraft` section with the design D3 defaults
- [x] 1.3 Add the INE municipality/province CSV as an embedded resource in
      `Infrastructure/CvExtraction/Resources/` and a singleton normalised lookup
      (accent/case-insensitive, province aliases); unit-test postcode prefix → province and
      alias matches (design D6)
- [x] 1.4 Add `/backend/Tests/CvCorpus/` to `.gitignore` (design D9)

## 2. Extraction core (spec: Suggestion rules and confidence; CV without a text layer)

- [x] 2.1 Define `ICvTextReader`, `CvText` (lines with page, order, optional font size) and
      `ICandidateDraftExtractor` / `CandidateDraft` in `Application/Abstractions/CvExtraction/`
- [x] 2.2 Implement `RuleBasedCandidateDraftExtractor`: e-mail and phone (libphonenumber, region
      `ES`), with table-driven unit tests over synthetic `CvText` fixtures
- [x] 2.3 Implement name detection, stop-list and e-mail corroboration / surname split, with unit
      tests for corroborated (`high`), uncorroborated (`low`) and compound-name cases
- [x] 2.4 Implement postcode → province, municipality and province-name matching with the
      contact-block preference and confidence rules, with unit tests
- [x] 2.5 Implement `PdfCvTextReader` (PdfPig, strict parsing, page and character caps, font size
      per line); unit-test with PDFs generated in-test (text, image-only → no text, encrypted →
      unreadable, over the page cap)
- [x] 2.6 Implement `DocxCvTextReader` (`ZipArchive` + `XmlReader`, DTD prohibited, character cap);
      unit-test with an in-memory generated DOCX

## 3. API operation (spec: CV draft extraction operation; authorization; formats; scan before parse; not stored nor logged; bounded work)

- [x] 3.1 Add `UnprocessableException`, `TooManyRequestsException` and `ServiceUnavailableException` to
      `Application/Common/Errors` and map them to 422 / 503 (+ `Retry-After`) ProblemDetails in
      `GlobalExceptionHandler` (429 → `Retry-After` too); unit-test the mapping
- [x] 3.2 Add `CandidateAuditEvents.CvDraftExtracted` (`candidate.cv_draft.extracted`) and
      register it in `AuditEventTypes.All`; add its label key for the audit screen in `es.json`
- [x] 3.3 Implement `CreateCandidateDraftCommand` / handler in
      `Application/Features/Candidates/CvDraft/`: permission guard, extension allowlist (PDF/DOCX),
      inspector, scan, reader + extractor under the time budget, audit event, one identifier-only
      log line, the refusal codes of design D4
- [x] 3.4 Unit-test the handler with hand-written doubles: permission checked first; reader never
      called for infected/error verdicts or rejected content; every refusal code; audit carries
      draft id and outcome only; a captured log sink contains no filename or suggested value
- [x] 3.5 Implement `CandidateDraftEndpoints` (`POST /api/candidates/draft-from-document`):
      permission check before any body read, size/multipart/missing/empty checks,
      `MultipartReader` into a capped `MemoryStream`, `no-store` header, `.WithName`,
      `.Produces*` metadata; register it in `Program.cs`
- [x] 3.6 Add the `CvDraftGate` concurrency gate (no queue), entered after the permission check,
      with a 429 `cv_draft.busy` ProblemDetails via `TooManyRequestsException` (design D3)
- [x] 3.7 Register readers, extractor, lookup and options in `Infrastructure/DependencyInjection.cs`;
      confirm `ProjectDependencyTests` still pass

## 4. Security and integration evidence

- [x] 4.1 Integration tests (Testcontainers): 401 unauthenticated and 403 for a
      `candidates.read`-only and a `documents.upload`-only actor, each with a scanner double proving
      nothing was scanned or parsed
- [x] 4.2 Integration tests: happy path with `MarkerMalwareScanner` returns the six-field contract
      and `cv_draft.extracted`; image-only PDF returns `cv_draft.no_text`; infected marker → 422;
      scanner error → 503; unsupported format / mismatch / oversize / missing / empty → 400
- [x] 4.3 Integration tests: after success and after each refusal there are no new candidate or
      document rows and no objects under the quarantine or available roots; exactly one
      `candidate.cv_draft.extracted` audit row with no personal data
- [x] 4.4 Integration test: saturating the limiter returns 429 `cv_draft.busy`
- [x] 4.5 Add a `frontend/tests/security/` check that the SPA reaches the draft only through the
      API transport and stores nothing in `localStorage`; run `npm run security:rls` and
      `npm run security:storage` and inspect the output

## 5. Frontend (spec: Create form applies a draft without overwriting; candidate-profile-pages delta)

- [x] 5.1 Add `candidate-draft.service.ts` (multipart via `api-transport`), the draft types, and wire the
      service in `core/di/services.ts`; map known refusal codes to `TranslatableError` keys
- [x] 5.2 Add `applySuggestion` to `candidate-form.logic.ts` (fill only empty/whitespace fields,
      report filled keys and confidence) with Vitest unit tests, including a typed value that must
      survive
- [x] 5.3 Extend `CandidateForm` with the `suggestion` prop (nonce-keyed functional merge), the
      `suggested` class, and `span.badge` markers «Sugerido del CV» / «Revisar» linked via
      `aria-describedby`, which disappear when the user edits the value
- [x] 5.4 Add `cv-draft-picker.tsx` (+ `.css` with Kepler tokens) with `name="cvFile"`,
      `data-testid="cv-draft-file"`, busy state and `role="status"` outcome messages; place it above
      the form in `candidate-create-page.tsx`
- [x] 5.5 Add all copy under `candidate.cvDraft.*` in `es.json` (Spanish, accented), and optionally
      `en.json`; no hardcoded JSX or attribute copy
- [x] 5.6 Vitest: picker states per outcome with a transport double; the create page renders the
      picker, the form and the post-save note only; badges render and clear on edit

## 6. Update affected existing tests

- [x] 6.1 Review and update existing unit specs touched by the change (`candidate-create-page`,
      `candidate-form`, `services.ts` doubles in `tests/unit/support/`, audit event type lists in
      backend tests and the audit screen specs) and run them

## 7. Run the suites

- [x] 7.1 Run `npm run test:backend` (Docker running) and inspect the output; confirm the
      PostgreSQL state assertions and storage-root assertions in 4.3 pass
- [x] 7.2 Run `npm test` from `frontend/` and inspect the output
- [x] 7.3 Run `npm run build:all` and confirm no warnings-as-errors

## 8. End to end

- [x] 8.1 Add a synthetic, fictitious CV (built in memory by `tests/e2e/support/synthetic-cv.ts`, no
      committed binary; its e-mail carries the `Date.now()` marker) and
      `tests/e2e/candidate-cv-draft.spec.ts` (prefill + badges; typed value kept; save with a
      `Date.now()`-marked name)
- [x] 8.2 Start the stack with `docker compose up --build` and run
      `npx playwright test tests/e2e/candidate-cv-draft.spec.ts` from `frontend/`; inspect the
      result and confirm the teardown purged the created candidate
- [x] 8.3 Run the candidate CRUD and navigation specs (`candidate-crud.spec.ts`) to confirm the
      create journey is unchanged without a CV

## 9. Reliability measurement

- [x] 9.1 Implement the skipped-when-absent `CvCorpusReport` integration test (Category
      `CvCorpus`) that prints per-field correct/wrong/missing counts by confidence, no values
- [x] 9.2 Write `docs/ktl-32/extraction-test-sheet.md` (corpus assembly, `expected.json` format,
      command, results table to fill); if a local corpus is available, run it and record the counts

## 10. Documentation

- [x] 10.1 Write `docs/ktl-32/cv-draft-contract.md` (endpoint, permission, request, response,
      outcome and refusal codes, limits) and `docs/ktl-32/extraction-rules.md` (rules, confidence,
      INE attribution and refresh procedure)
- [x] 10.2 Write `docs/ktl-32/release-notes.md` and update `README.md` (Spanish) with the new
      create-page behaviour and the `CvDraft` options

## 11. Lint and format

- [x] 11.1 Run `npm run lint` and `npm run format:check` from `frontend/` and fix any finding
