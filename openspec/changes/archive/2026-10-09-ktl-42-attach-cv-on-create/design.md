## Context

See proposal.md - Why. Today `CandidateCreatePage` sends the picked `File` to
`CandidateDraftService.extract` (`POST /api/candidates/draft-from-document`) and discards it; the
draft endpoint scans, reads and forgets it (KTL-32, requirement «CV and extracted content are
neither stored nor logged»). The candidate page already uploads documents through
`DocumentService.upload` (`POST /api/candidates/{candidateId}/documents`, `documents.upload`,
quarantine → ClamAV → `Clean`), whose response is the document in the `Pending` state; the
candidate page's documents panel already polls and shows that state. `ToastService` already
supports a `warning` type.

## Goals / Non-Goals

**Goals:**

- Attach the CV read on the create page in the same save, through the existing upload path.
- Keep the candidate when attaching fails, and say so.
- No backend, database, grant or dependency change.

**Non-Goals:**

- Holding the file on the server between draft and save.
- Making create + attach atomic, or adding a busy/double-submit guard to the create form (a
  pre-existing gap, unchanged here).
- Attaching CVs from the edit flow.

## Decisions

### D1. Re-upload from the browser after create, not a server-side hand-off

The page keeps the `File` it already has and, once `candidateService.create` returns the new id,
calls `documentService.upload({ candidateId, file, documentType: 'CV', isPrimary: true })`.

Alternative considered: the draft endpoint keeps the clean file under a short-lived token and the
create request references it. Rejected: it stores personal data before anyone decided to keep it,
reverses a KTL-32 requirement, and needs expiry, cleanup, a new table or storage area, grants and a
token-binding check to the actor — a large, security-sensitive slice to save one network transfer.
The cost of D1 is a second transfer and a second scan of a ≤ 20 MB file, which is accepted.

### D2. What the page holds, and when

`CandidateCreatePage` holds `heldCv: File | undefined` and `attachCv: boolean` (default `true`) in
`useState`. `heldCv` is set only when `extract` resolves for the current request (both a
draft with fields and a `cv_draft.no_text` draft), and is cleared when a new pick starts, so a
refused, failed or aborted pick leaves nothing held and a superseded one cannot win. Picking a new
CV resets `attachCv` to `true`. The state dies with the page, so leaving it drops the file. No
storage API (`localStorage`, IndexedDB) is used.

`no_text` CVs are held: they are still the candidate's CV (typically a scan), and the document
store accepts PDFs; only the field extraction failed.

### D3. Permission gating

The page calls `usePermission('documents.upload')` at the top. Without it, the checkbox is not
rendered and `save` never attempts the upload, so a user lacking the permission gets KTL-32
behaviour with no failure toast. This is a courtesy; the API still checks `documents.upload`
before validating the upload (existing behaviour, re-asserted by tests).

### D4. Save sequence and failure handling

```
save(draft):
  created = await candidateService.create(draft)      // failure → error toast, stay, keep heldCv
  if (heldCv && attachCv && canUpload):
    try   await documentService.upload(...)
    catch → toastService.show(t('candidate.cvDraft.attachFailed'), 'warning')
  await navigate(`/app/candidates/${created.id}`)
```

The upload awaits only the upload POST (fast: the scan runs asynchronously behind `Pending`), not
the scan result, so save time grows by one transfer. Navigation happens in both branches; the
candidate is never rolled back (candidates are only removed logically anyway, and losing typed data
to a document failure would be worse). The warning is generic and does not echo the API message,
because the user acts on it the same way whatever the cause; a detailed reason is visible when they
retry from the candidate page.

### D5. UI and copy

`CvDraftPicker` gains optional props `attach?: { checked: boolean; onChange(checked: boolean) }`
and renders, when set, a checkbox `name="attachCv"`, `data-testid="cv-draft-attach"`, labelled
«Adjuntar este CV al candidato». The page passes `attach` only when `heldCv && canUpload`. The
component stays presentational. New/changed `es.json` keys:

- `candidate.cvDraft.hint` → «PDF o DOCX de hasta 20 MB. Solo se rellenan los campos vacíos y nada
  se guarda hasta que pulses «Guardar». Si lo marcas, el CV se adjunta al candidato al guardarlo.»
- `candidate.cvDraft.attach` → «Adjuntar este CV al candidato»
- `candidate.cvDraft.attachFailed` → «El candidato se ha guardado, pero el CV no se ha podido
  adjuntar. Súbelo desde la ficha del candidato.»

### Stack, data, authorization and storage impact

- Stack: frontend only (React page + presentational component). No new dependency.
- Data model: none. Storage model: unchanged; the attached CV is an ordinary candidate document.
- Authorization: `candidates.create` for create and `documents.upload` for attach, both enforced by
  the API before validation; the SPA only hides the choice.
- Personal data: the file stays in page memory and is sent only to the API's upload endpoint. No
  logging of the file or filename was added on either side.

### Test strategy

- Unit (`tests/unit/candidate-create-page.spec.tsx`, service doubles via `ServicesProvider`):
  checkbox absent before a read, after a refusal, and without `documents.upload`; present and
  ticked after a draft and after `no_text`; save uploads the held file with `CV`/primary and the new
  id; unticked → no upload; second pick replaces the first; upload failure → warning toast and
  navigation; create failure → no upload, file kept.
- Unit (`cv-draft-picker`): renders the checkbox only with `attach`, and reports changes.
- E2E (`tests/e2e/candidate-cv-draft.spec.ts`): the existing journey now expects
  `cv-<marker>.pdf` in `candidate-documents`; a second case unticks and expects none. File names
  already carry the `Date.now()` marker.
- Security evidence: the backend integration suite already asserts the document upload is refused
  for unauthenticated and unauthorized actors; confirm it covers `POST .../documents` and add the
  case if it does not. `npm run security:rls` and `security:storage` stay green.

## Risks / Trade-offs

- [Candidate saved without the CV when the upload fails] → warning toast with the next step; the
  candidate page offers the upload.
- [Double scan doubles ClamAV work per CV-created candidate] → bounded by the same 20 MB limit and
  one human action per candidate; acceptable at internal-tool volume.
- [User attaches the wrong file (e.g. an agency summary)] → the choice is visible and can be
  cleared before saving; the document can be removed from the candidate page.
- [Holding up to 20 MB in memory] → one file, released with the page.

## Migration Plan

Frontend-only deploy; rollback is reverting the SPA build. No data to migrate.
