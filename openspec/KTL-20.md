# KTL-20 — CV document preview on the candidate detail page

## [original]

Preview CV document

The candidate detail page will include a CV preview panel at the very bottom so that candidates with a CV in PDF format (and ideally in Word format too if that's possible) will display their details and the document preview on the same page

## [enhanced]

### User story

**As** a recruiter holding `view_candidates` and `documents.download`,
**I want** to read a candidate's CV inline at the bottom of their detail page,
**so that** I can assess the profile and the CV side by side without downloading the file,
opening an external viewer and leaving the application.

### Context — what exists today (KTL-9)

Documents are already uploaded, quarantined, scanned and served. This ticket adds **no new
backend capability**: it consumes the existing download endpoint from a new frontend panel.

| Aspect       | Today                                                                                                                                                                      |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Metadata     | `GET /api/candidates/{candidateId}/documents` → `CandidateDocumentResponse` (`mimeType`, `isPrimary`, `availabilityState`, …)                                              |
| Bytes        | `GET /api/candidates/{candidateId}/documents/{documentId}/content`, permission `documents.download`, `Cache-Control: private, no-store`, `nosniff`, attachment disposition |
| Frontend     | `DocumentService.download()` fetches the blob through `ApiTransport.download()`, creates an object URL and clicks a hidden anchor                                          |
| Detail page  | `candidate-detail-page.tsx` renders `CandidateDocuments` inside the two-column grid; there is no preview anywhere                                                          |
| Availability | Only `availabilityState === 'Available'` (clean scan) is downloadable; `Pending`, `Error`, `Refused`, `LegacyUnavailable` are not                                          |

Relevant code: [DocumentEndpoints.cs](../backend/Web/Features/Documents/DocumentEndpoints.cs),
[ManageCandidateDocuments.cs](../backend/Application/Features/Documents/ManageCandidateDocuments.cs),
[DocumentContract.cs](../backend/Application/Features/Documents/DocumentContract.cs),
[document.service.ts](../src/app/features/documents/services/document.service.ts),
[candidate-documents.tsx](../src/app/features/candidates/components/candidate-documents.tsx),
[candidate-detail-page.tsx](../src/app/features/candidates/pages/candidate-detail-page.tsx),
[api-transport.ts](../src/app/core/http/api-transport.ts), [nginx.conf](../nginx.conf),
spec [private-document-storage](specs/private-document-storage/spec.md),
docs [docs/ktl-9/documents.md](../docs/ktl-9/documents.md).

### Decisions taken during refinement

1. **PDF only.** `application/pdf` is the only previewed type. Browsers cannot render
   `.doc/.docx/.odt/.rtf` natively, and every alternative was rejected for this slice:
   client-side conversion (a new npm dependency that renders untrusted candidate content as
   HTML — an XSS surface) and server-side conversion (a LibreOffice container, a second place
   where candidate binaries are materialized). Word, images and every other allowed format show
   a Spanish explanation plus the existing download action. Server-side conversion is **not**
   ruled out forever; it needs its own ticket and its own dependency justification.
2. **Primary CV by default, with a picker.** The panel opens on the document flagged
   `isPrimary`; if none is flagged, on the most recently uploaded previewable document. A
   selector lets the user preview any other **available** document.
3. **Bytes go through the transport, never through `iframe src="/api/…"`.** The API sets
   `X-Frame-Options: DENY` and `default-src 'none'; frame-ancestors 'none'` on its own
   responses, so pointing a frame at the endpoint would be blocked and would bypass the
   transport's correlation id, timeout and error mapping. The panel fetches the blob with
   `DocumentService` and renders a same-origin `blob:` URL.
4. **Narrow CSP change.** The SPA's CSP is `default-src 'self'` with no `frame-src` or
   `object-src`, so a `blob:` frame is blocked today. `nginx.conf` gains exactly
   `frame-src blob:; object-src blob:`. `frame-ancestors 'none'`, `script-src 'self'` and the
   rest are unchanged, and `default-src` is not widened. The change's design must record this
   delta and why it is the minimum.
5. **Preview is a download.** It requires `documents.download` in the UI and
   `documents.download` in the API, and it produces the existing `document.downloaded` audit
   entry, because the bytes really do leave the server. A separate `document.previewed` action
   would need a new endpoint or parameter and is out of scope.
6. **No new npm or NuGet dependency, and no backend change.** If the slice turns out to need
   one, it stops and gets a documented reason in the design first.

### Functional description

A new **«Vista previa del CV»** panel is appended as the last block of
`/app/candidates/:id`, full width (`.span-all`), below the existing two-column grid — so it is
the last thing on the page, as the brief asks.

**Visibility**

- Rendered only when the user holds `documents.download`. Hiding it is convenience,
  not the control: the API check stays.
- Hidden entirely when the candidate has no documents at all (the «Documentos» section already
  says «Sin CV adjunto.»).

**States**

| State                                        | What the panel shows                                                                                                 |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| A clean PDF is selected                      | The document rendered in the browser's native viewer, plus the file name and the «Descargar» action                  |
| Loading the bytes                            | «Cargando la vista previa…» with `aria-busy`                                                                         |
| Selected document is not a PDF               | «La vista previa solo está disponible para archivos PDF. Descarga el archivo para abrirlo.» + «Descargar»            |
| Selected document is not available yet       | «El documento está en análisis. La vista previa estará disponible cuando termine.» (reuses the availability wording) |
| Document rejected / scan error / legacy      | The existing explanation for that state; no preview, no download                                                     |
| Fetch failed (network, 403, 404, timeout)    | «No se ha podido cargar la vista previa.» + a «Reintentar» button; the error also goes through `useErrorToast()`     |
| Candidate has documents but none previewable | The fallback message, with the picker still listing them                                                             |

**Picker**

- A `<select name="previewDocument" data-testid="preview-document-select">` labelled «Documento»,
  listing every document with the same label the documents list uses (file name + «Principal»
  badge text). It is omitted when there is only one document.
- Changing the selection aborts the in-flight fetch, revokes the previous object URL and loads
  the new document.

**Rendering and resource handling**

- The blob is fetched once per document and cached for the lifetime of the page; re-selecting a
  document already fetched does not hit the API again.
- The object URL is revoked on unmount, on selection change and on candidate change. The fetch
  uses an `AbortController` tied to the effect.
- `ApiTransport`'s 15 s default timeout is too tight for a 20 MB PDF; the preview call passes an
  explicit larger `timeoutMs` (60 s, matching upload).
- The viewer is at least 600 px tall on desktop and shrinks with the viewport; the panel never
  causes horizontal page scroll at 390 px.

### Frontend files

| File                                                                         | Change                                                                                                                                                                                                                   |
| ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/app/features/candidates/components/candidate-cv-preview.tsx` (new)      | The panel: picker, states, viewer, download action                                                                                                                                                                       |
| `src/app/features/candidates/components/candidate-cv-preview.css` (new)      | Plain co-located CSS, Kepler tokens, no CSS Modules, no inline `style`                                                                                                                                                   |
| `src/app/features/candidates/components/candidate-cv-preview.logic.ts` (new) | Pure helpers: `isPreviewable(document)`, `pickDefaultDocument(documents)`, the state→message mapping. Kept out of the `.tsx` so fast refresh keeps working                                                               |
| `src/app/features/documents/services/document.service.ts`                    | Add `openPreview(candidateId, documentId, fallbackFileName, signal?)`: returns the `ApiDownload` without clicking an anchor, with `timeoutMs: 60_000`. `download()` is refactored to reuse it so there is one fetch path |
| `src/app/features/candidates/pages/candidate-detail-page.tsx`                | Render `<CandidateCvPreview candidate={item} />` as the last child of `.page`; hoist `usePermission('documents.download')` to the top                                                                                    |
| `src/assets/i18n/es.json` (+ `en.json` optional)                             | New keys under `candidate.profile.preview.*` **and** the migrated copy of `candidate-detail-page.tsx`                                                                                                                    |
| `eslint.config.js`                                                           | Remove `src/app/features/candidates/pages/candidate-detail-page.tsx` from `LEGACY_HARDCODED_COPY` — the list only shrinks, and this change touches the file. The new files never go on it                                |
| `nginx.conf`                                                                 | `Content-Security-Policy`: add `frame-src blob:; object-src blob:`, nothing else                                                                                                                                         |

`candidate-documents.tsx` stays untouched, which keeps its own legacy-copy migration out of this
slice. If a per-row «Previsualizar» button is added after all, that file's copy must move to
`es.json` and leave `LEGACY_HARDCODED_COPY` in the same change.

Conventions to respect: React function component, `useState`/`useEffect`, `usePermission()`
hoisted (never `authService.hasPermission()`), `useErrorToast()` for failures, `useCandidate()`
for the aggregate, services reached through `useServices()`, every control keeps a `name=` and a
`data-testid`, and all copy comes from `t()`.

### API and data contract

**No endpoint, handler, command, query, entity, table, migration or grant changes.** The panel
reuses:

| Method | Route                                                          | Permission           | Use                              |
| ------ | -------------------------------------------------------------- | -------------------- | -------------------------------- |
| GET    | `/api/candidates/{candidateId}/documents`                      | `candidates.read`    | Metadata for the picker          |
| GET    | `/api/candidates/{candidateId}/documents/{documentId}/content` | `documents.download` | The bytes rendered in the viewer |

- `mimeType` from the metadata decides previewability; the API already rejected uploads whose
  extension and detected content disagree, so the frontend does not sniff content.
- Fail-closed behaviour is unchanged: the content route returns `401` for an unauthenticated
  caller and `404` for an authenticated caller without permission; only `Clean` documents open.
- No storage key, path or internal identifier is introduced anywhere in the UI. The `blob:` URL
  is same-origin, opaque and revoked.

### Acceptance criteria

**Preview**

- **Given** a candidate whose primary document is a clean PDF, **when** a user with
  `documents.download` opens the detail page, **then** «Vista previa del CV» is the
  last block on the page and renders that PDF without leaving the page.
- **Given** a candidate with several available documents, **when** the user picks another one in
  «Documento», **then** the viewer shows that document and the previous object URL is revoked.
- **Given** a candidate with no primary flag, **then** the most recently uploaded previewable
  document is selected by default.
- **Given** a selected `.docx` (or any non-PDF) document, **then** no viewer is rendered, the
  Spanish fallback message is shown, and «Descargar» still works.
- **Given** a document in `Pending`, `Error`, `Refused` or `LegacyUnavailable` state, **then** no
  bytes are requested and the matching explanation is shown.
- **Given** the content request fails, **then** «No se ha podido cargar la vista previa.» and a
  «Reintentar» button are shown, and a retry re-issues exactly one request.
- **Given** the user navigates away while the preview is loading, **then** the request is aborted
  and no state update happens after unmount.

**Authorization**

- **Given** a user without `documents.download`, **then** the panel is not rendered.
- **Given** an unauthenticated or unauthorized caller hitting the content route directly,
  **then** it receives `401` or `404`, respectively, with no bytes and no metadata leak (unchanged
  behaviour, re-asserted).

**CSP**

- **Given** the production bundle behind nginx, **then** the response CSP contains
  `frame-src blob:` and `object-src blob:`, still contains `frame-ancestors 'none'` and
  `script-src 'self'`, and `default-src` is still `'self'`.
- **Given** the rendered page, **then** the browser console reports no CSP violation while a PDF
  is previewed.

**UI quality**

- **Given** a 390 px viewport, **then** the panel fits, the page does not scroll horizontally and
  the viewer is still usable.
- **Given** a keyboard user, **then** the picker, the viewer and «Descargar» are reachable in a
  sensible tab order with visible focus, and the viewer element carries a descriptive `title`.
- All new copy is Spanish with correct accents, comes from `es.json`, and `npm run lint` passes
  with `candidate-detail-page.tsx` removed from `LEGACY_HARDCODED_COPY`.

### Test coverage

**Frontend (Vitest + Testing Library)**

- New `tests/unit/candidate-cv-preview.spec.tsx`: default selection (primary, then newest),
  picker switching, PDF vs non-PDF branches, each availability state, loading state, error +
  retry, abort on unmount, object URL revoked, and the panel absent without the permission.
  Stub `URL.createObjectURL`/`revokeObjectURL` — jsdom does not implement them.
- New `tests/unit/candidate-cv-preview.logic.spec.ts`: `isPreviewable`, `pickDefaultDocument`
  (ties, no primary, empty list), and the state→message mapping.
- `tests/unit/document.service.spec.ts`: `openPreview` hits the content path with the longer
  timeout and does **not** click an anchor; `download()` still does.
- `tests/unit/candidate-profile-sections.spec.tsx`: the panel is the last block and is gated by
  the permission.
- `tests/unit/i18n.spec.ts`: the new `candidate.profile.preview.*` keys exist and nothing is
  orphaned.

**E2E (Playwright, selectors without Spanish text)**

- Extend `tests/e2e/candidate-documents.spec.ts`: upload a PDF fixture, wait for it to become
  available, reload the detail page, and assert `[data-testid="cv-preview-viewer"]` exists with a
  `blob:` source and no CSP violation in the console. Then select a non-PDF document and assert
  `[data-testid="cv-preview-unsupported"]`. Restore seed data afterwards.
- `tests/e2e/navigation-responsive.spec.ts` or the profile spec: the panel at 1280 and 390 px
  with no horizontal page scroll.
- `tests/e2e/ux-accessibility.spec.ts`: the viewer has an accessible name and the picker a label.

**Security evidence**

- `tests/security/` gains a check that the served CSP grants `blob:` only to `frame-src` and
  `object-src` and still denies framing (`frame-ancestors 'none'`).
- `tests/e2e/secure-access.spec.ts`: a session without `documents.download` sees no
  preview panel, and a direct request to the content route returns `404`.
- `npm run security:rls` and `npm run security:storage` must still pass (not affected).
- No backend test changes are expected; if the slice ends up touching the API, the download
  permission matrix in `Tests/IntegrationTests` must be re-run and reported.

### Documentation

- New `docs/ktl-20/document-preview.md`: which formats are previewable and why, the exact CSP
  delta and its justification, the fact that a preview is audited as `document.downloaded`, and
  the follow-up option of server-side Word→PDF conversion.
- `docs/ktl-9/documents.md`: cross-reference the preview and note that `/content` now serves both
  downloads and previews.
- `openspec/specs/private-document-storage/spec.md`: add a requirement that inline preview uses
  the same permission and clean-scan gate as download, renders from an opaque same-origin object
  URL, and never exposes a permanent URL or storage key.
- `README.md` (in Spanish): mention the CV preview on the candidate detail page.

### Non-functional requirements

- **Security / personal data:** authorization stays in the API and runs before anything else; no
  storage key, path or permanent URL reaches the browser; the object URL is revoked; the response
  stays `private, no-store` so the PDF is not written to the HTTP cache; the CSP is widened by
  exactly two directives and `default-src` is untouched; the file name is the only personal-data
  string rendered, and nothing new is logged.
- **Performance:** at most one content request per document per page visit, issued only for clean
  PDFs; bytes are already capped at 20 MB; no polling; the candidate detail page's existing
  requests are unchanged.
- **Accessibility:** labelled picker, descriptive `title` on the viewer, `aria-busy` while
  loading, error text in a `role="alert"` region, visible focus, 44 × 44 px touch targets, and a
  «Descargar» fallback for anyone whose browser cannot render the embedded viewer.
- **Responsive:** one DOM tree for both widths; the panel uses the existing grid and `.span-all`
  utility, never inline `style` or a `matchMedia` branch.
- **Copy:** Spanish with correct accents through `t()`; file sizes via `formatNumber` if shown.

### Out of scope

- Server-side or client-side conversion of Word/ODT/RTF to PDF (separate ticket if wanted).
- Image preview (JPG/PNG/TIFF/BMP) — explicitly excluded by decision 1.
- A custom viewer: paging, zoom, rotation, text search, thumbnails, annotations or printing
  controls beyond what the browser provides.
- Previewing from the candidate list, the search results or the documents list rows.
- A distinct `document.previewed` audit action or any change to the audit schema.
- Text extraction, CV parsing or indexing the PDF content for search.

### Open for design

- **Viewer element.** `<object type="application/pdf">` with a `<p>` fallback inside, versus an
  `<iframe>`. Chromium's built-in viewer is sensitive to `sandbox` on a frame, so the design must
  pick one, state whether `sandbox` is applied, and record what was verified in a real browser —
  `frame-src` and `object-src` are both granted so either works at the CSP level.
- Whether blobs for already-viewed documents stay cached for the whole page visit or only the
  current selection, if memory on candidates with several large PDFs turns out to matter.
