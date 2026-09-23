## Why

Candidate editing is split across two pages in a way users do not expect: «Editar candidato» only
changes the core record and tags, while languages, programs, education, experience, skills, custom
notes and documents are edited inline on «Ver candidato». Users look for those controls on the edit
page and conclude they have disappeared, and anyone reviewing a profile works next to add/remove
buttons and upload controls they can trigger by accident. KTL-22 separates viewing from editing:
the detail page becomes read-only and the edit page owns every change.

## What Changes

- The candidate detail page (`/app/candidates/:id`) becomes **read-only** for every user: it shows
  all candidate data, the relation collections, tags, custom notes, the document list with
  download, and the CV preview, but no add, remove, edit, retire, upload, set-primary or replace
  control, whatever the user's permissions. It keeps the «Editar» link and activate/deactivate,
  gated as today.
- The candidate edit page (`/app/candidates/:id/edit`) hosts **every** editing control: the core
  form, then languages, programs, education, experience, skills, tags, custom notes and documents.
  `CandidateTags` moves out of `CandidateForm` into the page.
- Save semantics are unchanged and made explicit: the core form saves with «Guardar»; relation
  sections, tags, notes and documents persist each change immediately. The page says so.
- Saving the core form of an existing candidate **stays on the edit page** with a confirmation
  instead of navigating to the detail page.
- Creating a candidate shows only the core form; after the first save the user lands on
  `/app/candidates/:id/edit` to continue with relations and the CV.
- `candidate-documents.tsx`, `candidate-form.tsx` and `candidate-edit-page.tsx` move their copy to
  `es.json` and leave `LEGACY_HARDCODED_COPY`.
- **BREAKING (UI only):** users who edit relations, notes or documents from the detail page must
  now use the edit page. No API or contract change.

**Actors.** Recruiters (`rrhh_user`, `rrhh_admin`) who create and maintain candidates; readers
(`manager_reader`, `readonly`) who only view them.

**Key entities.** The candidate aggregate and its relation collections, tags, custom notes and
documents — all unchanged.

**Assumptions.** Every write already takes its concurrency token from the aggregate the service
last absorbed (`candidate.service.ts` `versionOf`), so a section write followed by a core save on
the same page does not conflict with itself. No seeded role holds `documents.upload` without
`candidates.update`.

**Edge cases.**

- A custom role with `documents.upload` but not `candidates.update` can no longer reach an upload
  control, because the edit route requires `candidates.update`. The API still accepts the upload.
- A core draft with unsaved changes while the user adds a skill: the skill persists at once; the
  draft stays in the form until «Guardar».
- A concurrent edit by someone else still answers 409, as today.
- A candidate with no relations, notes or documents shows the existing empty states on both pages.

**Success criteria.**

1. On the detail page no editing control is rendered for any user, including one holding every
   permission.
2. Every editable part of a candidate can be changed from `/app/candidates/:id/edit`.
3. A new candidate lands on its edit page after the first save.
4. A core save on the edit page keeps the user on the page and loses nothing.
5. The three files above are off `LEGACY_HARDCODED_COPY`, and build, unit, e2e, lint and format
   checks pass.

## Capabilities

### New Capabilities

- `candidate-profile-pages`: how the SPA presents a candidate: a read-only detail page, an edit
  page that owns every editing control and states its two save behaviours, and the create flow
  continuing on the edit page.

### Modified Capabilities

None. Every API-level requirement in `candidate-management`, `candidate-notes` and
`private-document-storage` (authorization, concurrency, relation writes, preview) is unchanged.

## Impact

- **Frontend only:** `frontend/src/app/features/candidates/pages/candidate-detail-page.tsx` and
  `candidate-edit-page.tsx`; `components/candidate-form.tsx`, `candidate-languages.tsx`,
  `-programs`, `-education`, `-experience`, `-skills`, `-tags`, `-notes` and `-documents` (new
  read-only mode); `frontend/src/assets/i18n/es.json`; `frontend/eslint.config.js`.
- **Tests:** unit specs for the profile sections, documents and edit page; e2e specs
  `candidate-profile`, `candidate-documents`, `candidate-tags-notes`, `candidate-api-cutover` and
  `secure-access` move their editing steps to the edit page.
- **No** backend, endpoint, contract, migration, grant, dependency or role change.
- **Personal data, RLS, storage, roles.** The change reads and displays the same personal data as
  today and adds no request, field, log line or storage path (principle 1). It touches no RLS
  policy, grant, storage access or role definition. Authorization stays in the API, which keeps
  refusing unauthenticated and unauthorized writes; hiding controls on the detail page is a UX
  decision, not a control (principle 3). The upload-only role consequence is recorded in the
  design and the docs.
- **Docs:** the new `candidate-profile-pages` spec, `docs/ktl-22/`, and `README.md`.
