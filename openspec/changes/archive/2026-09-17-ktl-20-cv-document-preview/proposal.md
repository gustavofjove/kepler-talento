# KTL-20 — CV document preview on the candidate detail page

## Why

Assessing a candidate today means reading the detail page and then downloading the CV to open it
in an external viewer, losing the profile context and leaving a copy of personal data on the
reviewer's disk for every profile they screen. Recruiters want the profile and the CV on one
screen. Everything needed to serve the bytes already exists (KTL-9): the only missing piece is a
panel that renders them inline.

## What Changes

- **New «Vista previa del CV» panel** as the last block of `/app/candidates/:id`, full width,
  rendering the candidate's CV in the browser's native viewer without leaving the page.
- **PDF only.** `application/pdf` is the only previewed type. Word, ODT, RTF, plain text and
  images show a Spanish explanation and keep the existing «Descargar» action. No conversion
  pipeline and no viewer library is introduced.
- **Primary CV by default, with a picker.** The panel opens on the document flagged `isPrimary`,
  falling back to the most recently uploaded previewable document, and offers a selector for any
  other available document.
- **Preview is gated exactly like download.** The panel is rendered only for holders of
  `documents.download`, the bytes come from the existing permission-checked
  `/content` endpoint, and only `Clean` / `Available` documents are rendered.
- **Narrow CSP widening** in `nginx.conf`: `frame-src blob:; object-src blob:` are added so a
  same-origin object URL can be framed. `default-src 'self'`, `script-src 'self'` and
  `frame-ancestors 'none'` are unchanged.
- **`DocumentService` gains `openPreview()`**, returning the fetched blob instead of clicking a
  download anchor, with a 60 s timeout suited to a 20 MB file. `download()` is refactored to
  reuse it so there stays one fetch path.
- **No backend change**: no endpoint, handler, command, query, entity, table, migration or grant
  is added or modified. **No new npm or NuGet dependency.**
- `candidate-detail-page.tsx` leaves `LEGACY_HARDCODED_COPY`; its copy moves to `es.json`
  alongside the new `candidate.profile.preview.*` keys.

Not breaking: no existing contract, route or stored data changes.

## Capabilities

### New Capabilities

_None._ The change consumes the existing document capability from a new UI surface.

### Modified Capabilities

- `private-document-storage`: add a requirement covering **controlled inline preview** — that
  rendering a document in the browser is subject to the same permission and clean-scan gates as
  a download, is served from the same non-cacheable response, uses an opaque same-origin object
  URL that is revoked, and never yields a permanent URL or storage key. Previewing is audited as
  a download because the bytes leave the server.

## Impact

**Personal data, storage access and authorization** — this change touches all three, and
principles 1 and 3 are upheld as follows:

- Authorization is unchanged and stays in the API. `GET /api/candidates/{id}/documents/{docId}/content`
  already requires `documents.download`, returning `401` for unauthenticated callers and `404`
  for authenticated callers without permission. Hiding the panel is convenience, not the control.
- The clean-scan gate is unchanged: `Pending`, `Error`, `Refused` and `LegacyUnavailable`
  documents are never fetched.
- No storage key, host path or permanent URL reaches the browser. The `blob:` URL is same-origin,
  opaque, and revoked on selection change, candidate change and unmount.
- The response keeps `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`, so
  the CV is not written to the HTTP cache.
- The frame must not point at the API: API responses carry `X-Frame-Options: DENY` and
  `default-src 'none'`, so the bytes are fetched through `ApiTransport` and rendered from the
  object URL.
- Nothing new is logged; the file name is the only personal-data string the panel renders.
- The existing `document.downloaded` audit entry is produced by a preview. This is accepted and
  documented rather than silently inherited: audit volume on candidates with a PDF will rise.
- RLS policies and role definitions are **not** touched.

**Code**

- Frontend: new `candidate-cv-preview.tsx` / `.css` / `.logic.ts` under
  `src/app/features/candidates/components/`; `candidate-detail-page.tsx`;
  `document.service.ts`; `src/assets/i18n/es.json`; `eslint.config.js`.
- Infrastructure: `nginx.conf` (CSP only).
- Tests: new `tests/unit/candidate-cv-preview.spec.tsx` and
  `candidate-cv-preview.logic.spec.ts`; updates to `tests/unit/document.service.spec.ts`,
  `candidate-profile-sections.spec.tsx`, `i18n.spec.ts`,
  `tests/e2e/candidate-documents.spec.ts`, `secure-access.spec.ts`, `ux-accessibility.spec.ts`,
  and a CSP assertion under `tests/security/`.
- Docs: new `docs/ktl-20/document-preview.md`; updates to `docs/ktl-9/documents.md`,
  `README.md`, and `openspec/specs/private-document-storage/spec.md`.

**Out of scope**: conversion of Word/ODT/RTF to PDF (server- or client-side), image preview, a
custom viewer (paging, zoom, search, annotations), previewing from the list or search results, a
distinct `document.previewed` audit action, and CV text extraction or indexing.
