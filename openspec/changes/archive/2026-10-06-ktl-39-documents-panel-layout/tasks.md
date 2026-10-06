## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-39` from an up-to-date `main`

## 1. API: refuse marking an unavailable document primary

- [x] 1.1 Add `DocumentErrors.NotAvailableException()` in
      `backend/Application/Features/Documents/DocumentContract.cs` (`ConflictException` with the
      existing `document.not_available` code and a Spanish message) (design D1)
- [x] 1.2 In `SetPrimaryCandidateDocumentHandler` (`ManageCandidateDocuments.cs`), after the
      permission guard, load the document, throw `Missing()` if absent and `NotAvailableException()`
      when `!CanBeDownloaded`, before calling `SetPrimaryAsync`; no audit on refusal
- [x] 1.3 Add `Marking_an_unavailable_document_primary_is_refused` to
      `backend/Tests/IntegrationTests/CandidateApiTests.cs`: pending, infected or rejected,
      scan-failed and legacy-without-binary documents → 409, `code: "document.not_available"`,
      primary unchanged, no `document.primary.changed` audit event
- [x] 1.4 Add or confirm coverage that an unauthenticated caller and a caller without
      `documents.upload` are refused on `PUT …/primary` for an unavailable document before its
      availability is checked
- [x] 1.5 Review and update existing backend tests affected by the rule: make
      `Marking_a_second_uploaded_document_primary_demotes_the_first` mark the second document clean
      (`MarkClean` through the test `DbContext`) before the `PUT`; confirm
      `Concurrent_primary_designations_…` needs no change
- [x] 1.6 Run `npm run test:backend` (Docker running) and inspect the output; confirm in the
      PostgreSQL test database assertions that the refused designation left `IsPrimary` unchanged
      — Executed: 850 unit tests and 507 integration tests passed, including the five new refusal
      cases. The full run initially failed its encrypted-search benchmark; an early standalone
      retry measured p95 313.1 ms against its 300 ms budget.
      A subsequent baseline comparison reproduced the failure on `origin/main` (`b0b6835`):
      broad text matching p95 331.9 ms and notes matching p95 430.0 ms. KTL-39 measured broad text
      matching p95 318.2 ms. Another test runner was active during both comparison runs, so these
      establish that the failure also occurs without KTL-39 but do not establish idle-machine
      performance. Search code and the benchmark are unchanged by this change.
      After the HTS runner finished, clean `main` passed all eight cases (highest p95 233.9 ms).
      KTL-39's first standalone run had one timing spike (p95 466.8 ms for availability-date order);
      the controlled repeat passed all eight cases (highest p95 246.0 ms). Backend validation is
      complete using the full-suite functional results and the passing standalone benchmark.

## 2. Shared pieces

- [x] 2.1 Create `frontend/src/app/shared/components/icons.tsx`: move `DownloadIcon` from
      `row-cv-preview.tsx` and add `FileIcon`, `StarIcon`, `TrashIcon`, `UploadIcon`, `CloseIcon`
      (16px, 1px strokes on half-pixel coordinates, `aria-hidden`) (design D4)
- [x] 2.2 Import `DownloadIcon` from the shared module in `row-cv-preview.tsx`
- [x] 2.3 Add a `.visually-hidden` utility to `frontend/src/styles.css` (design D5)
- [x] 2.4 Change `.item-main` in `styles.css` to an inline wrapping row (design D7)

## 3. Documentos logic and copy

- [x] 3.1 Create `candidate-documents.logic.ts` with `documentStateChip`,
      `documentExplanationKey`, `documentDetails`, `formatFileSize` and `canMarkPrimary` (design D2)
- [x] 3.2 Add the new `candidate.profile.documents.*` keys to `frontend/src/assets/i18n/es.json`
      (and `en.json`): `details`, `detailsWithType`, `state.refused`, `state.legacy`,
      `action.download|markPrimary|remove`, `upload.title|drop|hint|choose|clear|submit`; remove keys
      that become unused (design D8)
- [x] 3.3 Add `tests/unit/candidate-documents.logic.spec.ts`: chip and tone per state (including a
      missing state), explanation per state, details with and without type, size boundaries, and
      `canMarkPrimary`

## 4. Documentos panel UI

- [x] 4.1 Rewrite the list in `candidate-documents.tsx` as `<ul>`/`<li>` rows with icon, filename
      (`title`), «Principal», details or explanation line, the `document-availability` state cell
      with `data-state` and `StatusChip`, and fixed action slots per mode and permission (design D3,
      D4)
- [x] 4.2 Move the "still scanning" notice and «Actualizar» into the pending row's details line
- [x] 4.3 On mark-primary failure show the error toast and refresh the list (design D6)
- [x] 4.4 Rebuild the upload form as the «Subir documento» subsection: hidden native input (keeps
      `id`, `name`, `data-testid`, `accept`, `ref`), drop area with drag highlight and
      «Seleccionar archivo», selected-file card with size, clear, `document-is-primary` and
      `document-upload`, and `document-upload-error` inside the subsection (design D5)
- [x] 4.5 Create `candidate-documents.css`: row grid and areas, action slots, icon-button and
      danger-hover styles, `@container (max-width: 520px)` layout, drop area and selected card,
      using Kepler tokens only
- [x] 4.6 Check by hand on the dev server (port 4300) both modes at 1280px and 390px, including
      Experiencia and Formación entries

## 5. Frontend tests

- [x] 5.1 Review and update `tests/unit/candidate-documents.spec.tsx`: per-file accessible names or
      test ids; chips by state and no chip for available; download only with permission and
      availability; star only on available non-primary documents in edit mode; nothing in
      read-only; mark-primary refusal shows the toast and refreshes; upload error inside the
      subsection; choose, drop and clear a file with the matching dirty reports; upload as primary;
      keep the abort-on-unmount case
- [x] 5.2 Review other unit specs touched by the change (`candidate-cv-preview.spec.tsx`, the row CV
      button, `candidate-detail-page.spec.tsx`, Experiencia/Formación panels) and update any that
      break
- [x] 5.3 Run `npm test` from `frontend/` and inspect the output

## 6. End-to-end

- [x] 6.1 Update `tests/e2e/candidate-documents.spec.ts`, `secure-access.spec.ts` and
      `security-ops.spec.ts` to assert `document-availability` through `data-state` instead of
      Spanish text; extend the documents journey to check the drop area and selected-file card
      by test id, and keep the 1280/390 no-horizontal-scroll checks
- [x] 6.2 With `docker compose up` running (rebuild the API image so it includes task 1), run
      `npx playwright test tests/e2e/candidate-documents.spec.ts tests/e2e/secure-access.spec.ts
tests/e2e/security-ops.spec.ts` from `frontend/` and inspect the results
- [x] 6.3 Run the full `npm run e2e` and confirm the global teardown removed the
      `Date.now()`-marked candidates and documents the run created

## 7. Specs and documentation

- [x] 7.1 Update `docs/ktl-9/documents.md`: 409 `document.not_available` on `PUT …/primary`, and
      the «Estados para la interfaz» table with the new chip texts and no label for `Available`
- [x] 7.2 Update `docs/CORPORATE_IDENTITY_Kepler.md`, _Badges y estados_: document chips are shown
      only for unavailable states, with their tones
- [x] 7.3 Run `openspec validate ktl-39-documents-panel-layout --strict` and fix any finding

## 8. Quality gates

- [x] 8.1 Run `npm run lint` and `npm run format:check` from `frontend/` and fix any finding
- [x] 8.2 Run `npm run build:all` from `frontend/` (tsc, Vite build, `dotnet build` with warnings as
      errors) and inspect the output

## Validation evidence (2026-10-06)

- `npm test`: 84 files passed; 787 tests passed and one existing test skipped. A final targeted
  run of the document, translation, row preview and profile-section suites passed all 64 tests.
- Backend: 850 unit tests passed; 507 of 508 integration tests passed. The new unavailable-primary
  cases verify 409 with `document.not_available`, unchanged primary state in PostgreSQL, no primary
  audit, and authorization refusal before availability for unauthenticated and reader actors.
- Targeted Playwright documents and security suites: all 11 tests passed against the rebuilt API.
- Full `npm run e2e`: 103 passed and one sign-in fixture timed out during a Vite reload. That
  remaining test passed with `npx playwright test --last-failed`. The full-run teardown purged
  73 marked candidates and 23 stored files; the retry removed its one marked candidate.
- Visual review on port 4300: read-only, edit and selected-file layouts at 1280px and 390px,
  including all document states, long filenames, Formación and Experiencia. Synthetic review
  fixtures were removed by teardown; the temporary review spec was removed.
- `npm run build:all`: passed; .NET reported zero warnings and zero errors. Vite retains its
  existing large-chunk advisory.
- `npm run security:rls` and `npm run security:storage`: all boundary checks passed. No schema,
  grant, role or storage change was introduced.
- `openspec validate ktl-39-documents-panel-layout --strict`: passed.
- Final `npm run lint`, `npm run format:check` and `git diff --check`: passed.
- Generated search/position performance reports were restored to their prior contents to keep
  this change scoped to KTL-39. Search performance code and its budget were left unchanged.

## Backend benchmark resolution

The unchanged benchmark was run after the separate HTS test process exited, with no other
test runners active. The xUnit timing collection prevents competition inside its own test
process, but cannot isolate other processes. The standalone command was:

```sh
dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~EncryptedSearchPerformanceTests" --logger "console;verbosity=normal"
```

Clean `main` and KTL-39 both passed all eight cases, with highest p95 values of 233.9 ms and
246.0 ms respectively. One earlier standalone KTL-39 run still spiked to 466.8 ms in a single
case, so competing tests do not explain every observed failure. The recorded results show timing
variability; if it recurs, track benchmark environment stabilization or search performance in a
separate ticket. The 300 ms requirement, benchmark assertions and search implementation are
unchanged. Task 1.6 is complete, with the successful standalone retry explicitly recorded rather
than presenting the initial full-suite result as entirely green.
