# Tasks — KTL-20 CV document preview

## 0. Create Feature Branch

- [x] 0.1 Create and check out `feat/KTL-20` from an up-to-date `main` (the change was described
      by `openspec/KTL-20.md`). Never commit to `main`.

## 1. Preview fetch path in the document service

- [x] 1.1 Add `openPreview(candidateId, documentId, fallbackFileName, signal?)` to
      `src/app/features/documents/services/document.service.ts`: same `transport.download()` call
      as `download()` against `/candidates/{id}/documents/{docId}/content`, with
      `timeoutMs: 60_000` and the caller's `AbortSignal`, returning the `ApiDownload` without
      touching the DOM. (design D1)
- [x] 1.2 Refactor `download()` to call `openPreview()` and then perform the anchor click and
      `revokeObjectURL`, so exactly one place fetches document bytes. Keep `download()`'s
      observable behaviour identical. (design D1)
- [x] 1.3 Confirm no authorization logic is added to the service — it fetches, the API decides.
      (proposal Impact; design "Authorization")

## 2. Preview logic helpers

- [x] 2.1 Create `src/app/features/candidates/components/candidate-cv-preview.logic.ts` with
      `isPreviewable(document)` (`availabilityState === 'Available' && mimeType === 'application/pdf'`)
      and `pickDefaultDocument(documents)` (primary previewable → newest previewable → primary
      → `undefined`, ties broken by document id). (design D4; spec "Format cannot be rendered
      natively")
- [x] 2.2 Add the availability/format → message-key mapping to the same file so the `.tsx` exports
      only the component (fast-refresh rule). Cover `Pending`, `Error`, `Refused`,
      `LegacyUnavailable` and the non-PDF case. (spec "Unclean document is never previewed")

## 3. Preview panel component

- [x] 3.1 Create `candidate-cv-preview.tsx`: hoisted `usePermission('documents.download')`,
      returns `null` without it or when the candidate has no documents. (spec "Preview requires
      the download permission")
- [x] 3.2 Render the `<select name="previewDocument" data-testid="preview-document-select">`
      picker (labelled «Documento», omitted when there is a single document) and the default
      selection from `pickDefaultDocument`. (design D4)
- [x] 3.3 Fetch the selected document's bytes with `openPreview` inside an effect guarded by an
      `AbortController`, only when `isPreviewable`; keep the fetched `Blob` in a
      `Map<documentId, Blob>` cache and create an object URL only for the active selection;
      re-selecting cached bytes issues no request.
      (design D5; spec "Clean previewable document is rendered")
- [x] 3.4 Revoke the active object URL on selection change, candidate change and unmount; abort
      the in-flight request on unmount and on selection change. Poll pending metadata so the panel
      renders a document as soon as scanning settles. (spec "Preview exposes no durable reference")
- [x] 3.5 Render the viewer as `<object type="application/pdf" data={objectUrl}
data-testid="cv-preview-viewer">` with an accessible name, and the explanation plus the
      «Descargar» link as its fallback children. No `sandbox`, no inline `style`. (design D2)
- [x] 3.6 Render the remaining states: loading (`aria-busy`), unsupported format
      (`data-testid="cv-preview-unsupported"`), each unavailable state, and the failure state with
      a «Reintentar» button plus `useErrorToast()`. No content request in the unsupported or
      unavailable states. (spec "Content delivery fails", "Unclean document is never previewed")
- [x] 3.7 Create `candidate-cv-preview.css` (plain CSS, Kepler tokens, no CSS Modules): full-width
      panel, viewer at least 600 px tall on desktop, shrinking with the viewport, no horizontal
      page scroll at 390 px, 44 × 44 px touch targets.

## 4. Wire the panel into the detail page

- [x] 4.1 Render `<CandidateCvPreview candidate={item} />` as the last child of `.page` in
      `src/app/features/candidates/pages/candidate-detail-page.tsx`, using `.span-all` rather than
      an inline `gridColumn`.
- [x] 4.2 Move every hardcoded Spanish literal in `candidate-detail-page.tsx` into
      `src/assets/i18n/es.json` and render it with `t()`, then remove the file from
      `LEGACY_HARDCODED_COPY` in `eslint.config.js`. The list only shrinks; never add a file.
- [x] 4.3 Add the new `candidate.profile.preview.*` keys (panel title, picker label, loading,
      unsupported, unavailable states, failure, «Reintentar», «Descargar») in correct Spanish with
      accents. Adding the English values to `en.json` is optional.
- [x] 4.4 Leave `candidate-documents.tsx` untouched. If a per-row preview button is added after
      all, migrate that file's copy and remove it from `LEGACY_HARDCODED_COPY` in this change.

## 5. Content Security Policy

- [x] 5.1 Add exactly `frame-src blob:; object-src blob:` to the `Content-Security-Policy` header
      in `nginx.conf`. Do not touch `default-src 'self'`, `script-src 'self'`, `connect-src` or
      `frame-ancestors 'none'`. (design D3)
- [x] 5.2 Run `docker compose build nginx && docker compose up -d nginx`, load a candidate with a
      PDF on `http://localhost:4200`, and confirm in the browser console that the PDF renders with
      no CSP violation. Record what was observed.

## 6. Review and update existing tests (MANDATORY)

- [x] 6.1 Review every existing spec touching the detail page, the documents component and the
      document service, and update those affected: `tests/unit/document.service.spec.ts`,
      `tests/unit/candidate-documents.spec.tsx`, `tests/unit/candidate-profile-sections.spec.tsx`,
      `tests/unit/i18n.spec.ts`.
- [x] 6.2 Extend `tests/unit/document.service.spec.ts`: `openPreview` hits the content path with
      the 60 s timeout and does not click an anchor; `download()` still clicks one.
- [x] 6.3 Extend `tests/unit/candidate-profile-sections.spec.tsx`: the panel is the last block of
      the detail page and is absent without `documents.download`.
- [x] 6.4 Extend `tests/unit/i18n.spec.ts`: the new keys resolve and none are orphaned.

## 7. New frontend tests

- [x] 7.1 Create `tests/unit/candidate-cv-preview.logic.spec.ts`: `isPreviewable` and
      `pickDefaultDocument` (primary previewable, primary not previewable, no primary, ties,
      empty list) and the message mapping.
- [x] 7.2 Create `tests/unit/candidate-cv-preview.spec.tsx` with `URL.createObjectURL` /
      `revokeObjectURL` stubbed: clean PDF renders the viewer, picker switches document and revokes
      the prior URL, cached bytes issue no second request, pending metadata settles into a preview,
      an equivalent metadata refresh does not restart the content request, loading state, non-PDF
      shows the unsupported state with no request, each unavailable state shows its message with
      no request, failure shows retry and a retry issues exactly one request, unmount aborts and
      revokes.
- [x] 7.3 Run `npx vitest run tests/unit/candidate-cv-preview.spec.tsx tests/unit/candidate-cv-preview.logic.spec.ts tests/unit/document.service.spec.ts`
      and inspect the output.

## 8. Security checks (MANDATORY — this slice touches personal data, permissions and storage)

- [x] 8.1 Add a `tests/security/` check asserting the served CSP grants `blob:` to `frame-src` and
      `object-src` only, keeps `default-src 'self'` and `script-src 'self'`, and still sets
      `frame-ancestors 'none'`. It must fail if the policy is widened further. (design D3)
- [x] 8.2 Extend `tests/e2e/secure-access.spec.ts`: a session without
      `documents.download` sees no preview panel, and a direct request to
      `/api/candidates/{id}/documents/{docId}/content` returns `401` when unauthenticated and `404`
      when authenticated without permission — fail closed for both callers.
- [x] 8.3 Run `npm run security:rls` and `npm run security:storage` and inspect the output; both
      must pass. No database grant changes are expected — confirm `ktl_runtime` privileges are
      untouched and that no migration was added.
- [x] 8.4 Confirm no storage key, host path or permanent URL appears in any response or in the
      DOM, and that logs contain no candidate personal data.

## 9. Run the suites (MANDATORY)

- [x] 9.1 Run `npm test` (unit + integration + security projects) and inspect the output.
- [x] 9.2 Run `npm run test:backend` with Docker running to confirm the backend is unaffected, and
      inspect the output. If any API code was touched after all, re-run and report the document
      download permission matrix in `Tests/IntegrationTests`.
- [x] 9.3 Verify the PostgreSQL and storage state is unchanged by this slice: no new tables,
      columns, indexes, constraints, migrations or grants. Legacy Supabase integration checks for
      unchanged paths still pass.

## 10. End-to-end (MANDATORY — actually execute these)

- [x] 10.1 Start the stack (`docker compose up --build`) and confirm a PDF fixture can be uploaded
      and reaches `Available`.
- [x] 10.2 Extend `tests/e2e/candidate-documents.spec.ts`: upload a PDF, wait for availability,
      reload the detail page, assert `[data-testid="cv-preview-viewer"]` has a `blob:` source and
      no CSP violation is logged; then select a non-PDF document and assert
      `[data-testid="cv-preview-unsupported"]`. Selectors must not hardcode Spanish text.
- [x] 10.3 **Run** `npx playwright test tests/e2e/candidate-documents.spec.ts` against the dev
      server on :4300 and inspect the output.
- [x] 10.4 **Run** `npx playwright test tests/e2e/secure-access.spec.ts tests/e2e/ux-accessibility.spec.ts`
      and inspect the output.
- [x] 10.5 Add the panel to the responsive coverage at 1280 px and 390 px (in
      `tests/e2e/navigation-responsive.spec.ts` or the profile spec) and **run** that spec.
- [x] 10.6 **Run** the full `npm run e2e` once and inspect the output.
- [x] 10.7 Restore seed data afterwards; do not run `docker compose down --volumes`.

## 11. Documentation (MANDATORY)

- [x] 11.1 Create `docs/ktl-20/document-preview.md`: which formats are previewable and why, the
      exact CSP delta with its justification and mitigation, the fact that a preview produces a
      `document.downloaded` audit entry, the blob cache, and the deferred server-side Word→PDF
      conversion option.
- [x] 11.2 Update `docs/ktl-9/documents.md` to cross-reference the preview and note that
      `/content` now serves both downloads and previews.
- [x] 11.3 Update `README.md` (in Spanish) to mention the CV preview on the candidate detail page,
      leaving commands, paths and identifiers untranslated.
- [x] 11.4 Confirm `openspec/specs/private-document-storage/spec.md` will receive the
      "Controlled inline preview" requirement at sync/archive time; do not hand-edit it now.

## 12. Gates before done

- [x] 12.1 Run `npm run lint` and `npm run format:check`; both must pass with
      `candidate-detail-page.tsx` removed from `LEGACY_HARDCODED_COPY`.
- [x] 12.2 Run `npm run build:all` (warnings are errors) and inspect the output.
- [x] 12.3 Confirm no new npm or NuGet dependency was added (`git diff package.json
package-lock.json backend/Directory.Packages.props` is empty).
- [x] 12.4 Re-read `proposal.md` and `specs/private-document-storage/spec.md` and confirm every
      scenario has covering evidence; record which command produced it.
