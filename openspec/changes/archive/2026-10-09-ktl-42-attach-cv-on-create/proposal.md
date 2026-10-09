## Why

KTL-32 lets a recruiter pre-fill «Nuevo candidato» from a CV, but deliberately left the file
unattached (the brief's open question, deferred). In practice the recruiter saves the candidate and
then uploads the very same file again from the candidate page. That second step is redundant, easy
to forget, and leaves candidates without the CV they were created from. KTL-42 closes the open
question.

## What Changes

- The create page keeps the last CV the picker read successfully, **in browser memory only**, while
  the user stays on the page. Picking another CV replaces it; a CV the draft endpoint refused, or
  that could not be read, is not kept.
- Once a CV has been read, a checkbox «Adjuntar este CV al candidato», ticked by default, appears
  under the picker, only for a user who also holds `documents.upload`.
- On a successful create with the box ticked, the page uploads the kept file through the
  **existing** `POST /api/candidates/{candidateId}/documents` as the candidate's primary `CV`
  document, then opens the candidate page as today. Quarantine, ClamAV scan, permission check and
  private storage are exactly those of a manual upload.
- If the attach fails, the created candidate is kept: the page still opens it and shows a warning
  toast saying the CV was not attached and can be uploaded from the candidate page.
- With the box unticked, or no CV read, behaviour is unchanged from KTL-32.
- The picker hint copy changes to describe the new behaviour.
- **No backend change**: no new endpoint, permission, table, grant or dependency. The draft
  endpoint keeps scanning, reading and forgetting the file.

User value: one action creates the candidate with its CV attached. Actor: a recruiter holding
`candidates.create` (and `documents.upload` to attach). Key entities: candidate, candidate
document (existing). Success criteria: with the box ticked, saving yields a candidate whose
primary document is the picked CV, with no second upload by the user; a failed attach never loses
the candidate; a user without `documents.upload` never sees the box.

Assumptions: the user picks the CV to pre-fill because it is the candidate's CV, so attaching is
the right default; a CV is small enough (≤ 20 MB, already enforced) to hold in memory; the file is
scanned twice (once by the draft endpoint, once by the document upload), which is accepted.

Edge cases: the user picks a second CV (the second one is attached); the user unticks the box; the
draft refused the file (nothing to attach); the upload is refused (unsupported content, scanner
down, 403); the user leaves the page without saving (the file is dropped with the page state); the
candidate create itself fails (nothing is uploaded).

### Personal data, storage and roles

The change touches **personal data and document storage**, but only through existing, already
secured paths. Principle 1: the file never leaves the browser except in the user-initiated upload
to the API, is held only in page state, and is not logged on either side; the draft endpoint still
stores nothing. Principle 3: the binary goes through the standard document upload, so it lands
outside the webroot under an opaque key, stays quarantined until ClamAV reports `Clean`, and is
reachable only through permission-checked responses. `documents.upload` is checked by the API
before dispatch; hiding the checkbox is a courtesy, not the control. No role, permission, RLS
policy or grant changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `candidate-cv-draft`: «Create form applies a draft without overwriting» no longer says the CV is
  never attached; a new requirement states when and how the read CV is attached on save, and what
  happens when attaching fails or the user lacks `documents.upload`.

## Impact

- Frontend only: `candidate-create-page.tsx`, `cv-draft-picker.tsx` (+ `.css`), `es.json`
  (`en.json` optional), and the KTL-32 unit and e2e specs
  (`tests/unit/candidate-create-page.spec.tsx`, `tests/e2e/candidate-cv-draft.spec.ts`, whose last
  assertion currently checks the CV is **not** attached).
- Uses existing `DocumentService.upload` and `ToastService`; no new service or dependency.
- API, database, storage and security gates: unchanged.
- Docs: `openspec/specs/candidate-cv-draft/spec.md` (via this change's delta), `docs/ktl-42/`
  release note, and the README line about «Rellenar desde un CV» if it mentions the CV is not
  attached.
