## Why

KTL-22 split each candidate across two routes:

- a strictly read-only detail page (`/app/candidates/:id`);
- an edit page (`/app/candidates/:id/edit`) that owns every change.

HR users now switch pages to correct a single field. On the edit page, some sections save
immediately and the core record saves on demand, so users cannot tell what has been written.

This change merges the two pages into one candidate page. Each panel is edited in place and saved
on its own, next to the CV preview KTL-28 put beside the sections. Source brief:
`openspec/KTL-29.md`.

## What Changes

- **BREAKING (route):** `/app/candidates/:id` becomes the only page for an existing candidate.
  `/app/candidates/:id/edit` becomes a redirect to it, reachable with `candidates.read`, so
  bookmarks keep working.
- Datos principales, Competencias, Formación, Experiencia, Notas and Documentos each get an
  «Editar» button. It swaps that panel, in place, for the editor the edit page shows today.
  Auditoría stays read-only.
- **Form panels** (Datos principales, Competencias, Formación, Experiencia) stage changes in a
  draft:
  - «Guardar» writes only that panel through the existing endpoints.
  - «Cancelar» discards the draft.
  - Competencias writes only the families that changed, one after another. A partial failure
    keeps the failed families in the draft for retry.
- **BREAKING (behaviour):** relation entries (languages, programs, skills, tags, education,
  experience) no longer persist on each add, change or removal. They persist when the panel is
  saved.
- **Action panels** (Notas, Documentos) keep their immediate per-item actions behind «Editar»,
  closed with «Hecho».
- **One panel at a time:**
  - Switching panels with unsaved changes asks the user to confirm discarding them.
  - Leaving the page with unsaved changes (route change, reload or tab close) asks for
    confirmation.
- «Editar» follows the permission the API checks for that panel: `candidates.update`, or
  `documents.upload` for Documentos. A holder of `documents.upload` without `candidates.update`
  can therefore manage documents, which the old edit route blocked.
- On a removed (inactive) candidate, Competencias, Formación, Experiencia and Notas offer no
  «Editar», because the API refuses those writes. The page says why.
- The create page stays create-only. After the first save it always opens `/app/candidates/:id`.
- The breadcrumb loses the `Candidatos › name › Editar` trail.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `candidate-profile-pages`: the read-only detail page and the separate edit page give way to one
  candidate page with per-panel edit mode:
  - staged save and cancel for form panels, and immediate actions for Notas and Documentos;
  - one panel at a time, with unsaved-change protection;
  - the `/edit` redirect;
  - per-panel permissions, and removed-candidate behaviour;
  - creation landing on the candidate page.

  The layout, CV preview and localization requirements are restated for the single page.

- `primary-navigation`: the breadcrumb table and scenarios drop the candidate edit trail.
- `catalog-value-picker`: the family row scenario names the candidate page in edit mode instead
  of the removed candidate edit page.

## Impact

- **Frontend (`frontend/`):**
  - `src/app/app.tsx` (routes).
  - `features/candidates/pages/candidate-detail-page.tsx` becomes the single page.
    `candidate-edit-page.tsx` becomes a create-only page.
  - A new panel shell component.
  - `candidate-form.tsx`, `candidate-competencies.tsx`, `candidate-relation-section.tsx`,
    `candidate-education.tsx`, `candidate-experience.tsx`, `candidate-notes.tsx` and
    `candidate-documents.tsx`.
  - `services/candidate-relations.service.ts` moves from per-item to whole-list saves.
  - `src/assets/i18n/es.json`.
  - Unit, e2e and security specs that open `/edit`.
- **Backend, database, grants, storage:** no change. Every endpoint used already exists:
  - `PUT /api/candidates/{id}`;
  - `PUT /api/candidates/{id}/{languages|programs|education|experience|skills|tags}`;
  - the note and document routes.
- **Dependencies:** none added.
- **Personal data and authorization:**
  - The page shows and edits the same candidate personal data as before, through the same API
    calls. No new field is exposed and nothing is logged or stored in the browser.
  - Drafts live only in component state.
  - Principle 1 holds: minimum exposure, no new data path.
  - Principle 3 holds: every write is still authorized by the API before dispatch, and «Editar»
    visibility is a convenience that mirrors the API permission.
  - No RLS policy, storage rule or role definition changes.
- **Actors:**
  - Readers (`candidates.read`).
  - Editors (`candidates.update`).
  - Document managers (`documents.upload`).
  - Creators (`candidates.create`).
- **Key entity:** the candidate aggregate and its version token. Every panel save carries the
  held version and absorbs the new one, so saving panels one after another never conflicts with
  itself, while another user's edit still yields 409.
- **Assumptions:** the API's per-family whole-set endpoints are adequate for staged saves. No
  atomic multi-family endpoint is needed, because partial failure is recoverable per family.
- **Edge cases:**
  - A concurrent edit returns 409 and the draft is kept.
  - The catalogs are unavailable: Competencias can't save and shows one notice.
  - The candidate is removed.
  - The user is dirty and navigates away.
  - A file is selected but not uploaded.
- **Success criteria:**
  - An editor corrects any one section without leaving `/app/candidates/:id`.
  - Saving a form panel sends only that panel's request(s).
  - No edit is silently lost on panel switch or navigation.
  - `/edit` links still resolve.
  - Readers see no editing control, and the direct-write refusals stay green.
