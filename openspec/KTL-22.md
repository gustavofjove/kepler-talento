# KTL-22 — Edit everything on the edit page; make the detail page read-only

## [original]

Separate viewing from editing on the candidate pages

The "Editar candidato" page can only edit some of the candidate data (the main fields), while
languages, programs, education, experience, skills, tags, custom notes and documents are edited
from the "Ver candidato" page. Everything editable should move to the edit page, and the view
page should only display the details and the documents, without the ability to edit.

## [enhanced]

**Status:** Proposed
**Depends on:** KTL-8 (candidate writes and relations), KTL-9 (documents), KTL-20 (CV preview),
KTL-21 (tags and custom notes)

### Summary

Today the two candidate pages split editing in a way users do not expect:

- `/app/candidates/:id/edit` renders only `CandidateForm`: the core record (name, contact,
  status, availability, location, dates, `Observaciones internas`) saved by one «Guardar», plus
  the tags section.
- `/app/candidates/:id` shows everything, **and** hosts the inline add/remove controls for
  languages, programs, education, experience, skills, tags, custom notes and document upload.

This ticket makes the detail page a read-only view and puts every editing control on the edit
page. It is a **frontend-only** reorganisation: no endpoint, contract, permission, table or
grant changes.

### User stories

- **As a recruiter**, I open «Editar» and find every part of the candidate I can change in one
  place, instead of having to learn that some data is edited from the view page.
- **As a recruiter or manager**, I open a candidate to read or review their profile and CV
  without add/remove buttons, forms or upload controls that I could trigger by accident.
- **As a recruiter creating a candidate**, after the first save I land where I can go straight
  on to add languages, skills, experience and the CV.

### Context — what exists today

| Concern               | Today                                                                                                                                                                | Evidence                                                                          |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| Edit page             | Renders only `CandidateForm`; after saving it navigates to the detail page                                                                                           | `frontend/src/app/features/candidates/pages/candidate-edit-page.tsx:24-35,54`     |
| Core form             | Core fields + «Guardar»; renders `CandidateTags` below the form when editing an existing candidate                                                                   | `frontend/src/app/features/candidates/components/candidate-form.tsx:139`          |
| Detail page           | Renders every relation section with `canEdit = usePermission('candidates.update')`                                                                                   | `frontend/src/app/features/candidates/pages/candidate-detail-page.tsx:24,142-171` |
| Relation sections     | Each add/remove persists immediately through `candidateRelationsService`, one API call per change                                                                    | `candidate-languages.tsx`, `-programs`, `-education`, `-experience`, `-skills`    |
| Tags and notes        | Gate editing internally on `usePermission('candidates.update')`; they take no `canEdit` prop                                                                         | `candidate-tags.tsx:17`, `candidate-notes.tsx:19`                                 |
| Documents             | List, download and upload in one component; upload gated on `documents.upload`                                                                                       | `candidate-documents.tsx:50-51`                                                   |
| CV preview            | Last block of the detail page, gated on `documents.download`                                                                                                         | `candidate-cv-preview.tsx`, KTL-20                                                |
| Routes                | `/candidates/new` requires `candidates.create`; `/candidates/:id/edit` requires `candidates.update`                                                                  | `frontend/src/app/app.tsx:57-58,69-70`                                            |
| Concurrency           | Every write sends the version of the aggregate the service last absorbed, so a relation write followed by a core save on the same page does not conflict with itself | `candidate.service.ts:207-213`                                                    |
| Legacy hardcoded copy | `candidate-documents.tsx`, `candidate-form.tsx` and `candidate-edit-page.tsx` are still listed                                                                       | `frontend/eslint.config.js:13-15`                                                 |

The split has existed since the original Angular app and survived the KTL-3 migration
unchanged; it is not a regression.

### Decisions taken (2026-09-23)

1. **The detail page is read-only.** It shows all candidate data, the document list with
   download, and the CV preview. It keeps the «Editar» link and the activate/deactivate action
   (a confirmed status change, not data editing), both gated as today.
2. **Every editing control lives on the edit page**: the core form, languages, programs,
   education, experience, skills, tags, custom notes and document upload/primary/replace
   actions.
3. **Save semantics stay as they are.** The core form keeps its single «Guardar»; relation
   sections, tags, notes and documents keep persisting each change immediately. They are
   rendered below the core form as visibly separate sections, and the page says clearly which
   changes need «Guardar» and which are saved at once. A single draft saved by one button was
   considered and rejected for this slice: it would need several API calls batched from the SPA
   with partial-failure and 409 handling halfway through, for little user benefit.
4. **No backend change.** API permission checks already guard every write; this ticket only
   moves where the controls are rendered. Hiding a control on the detail page is not a control
   and is not presented as one.

### Scope

#### A — Read-only detail page

- Pass `canEdit={false}` to the relation sections on `candidate-detail-page.tsx`, or better,
  give each section an explicit `readOnly` / `mode` prop so the detail page never depends on
  the user's permissions to hide editing controls. The design picks one shape and applies it
  to all eight sections, including tags and notes, which currently read the permission
  themselves.
- `CandidateDocuments` gets a read-only mode: list, availability states and download (still
  gated on `documents.download`), but no upload, primary or replace controls, regardless of
  `documents.upload`.
- The CV preview panel stays on the detail page, unchanged.
- Empty states stay («Sin CV adjunto.», «Sin idiomas…») so a read-only section is never blank.

#### B — Edit page hosts all editing

- `candidate-edit-page.tsx` renders, for an existing candidate: the core form, then the
  relation sections, tags, custom notes and documents in editable mode. `CandidateTags` moves
  out of `CandidateForm` into the page so the form is only the form.
- Layout follows the detail page's grid (`.grid.two`, `.panel`, `.span-all` for notes), so the
  two pages read as the same candidate in two modes.
- Saving the core form **on an existing candidate** stays on the edit page and confirms the
  save (toast or status line), instead of navigating away and discarding the sections the user
  may still be working on. A «Ver candidato» / «Volver» link returns to the detail page.
- The heading and the Spanish helper text explain the two save behaviours. Also replace the
  stale «Los cambios quedan preparados para auditoría y RLS.» subtitle.

#### C — Creating a candidate

- `/candidates/new` shows the core form only: the other sections need a candidate id.
- After the first successful save, navigate to `/app/candidates/:id/edit` (not the detail
  page), so the user continues with the relations and the CV. A short line under the new form
  says that languages, skills, experience and documents can be added after saving.

### Permissions

| Action                                   | Page   | Permission (unchanged)                 |
| ---------------------------------------- | ------ | -------------------------------------- |
| View candidate, relations, tags, notes   | Detail | `candidates.read`                      |
| Download / preview a document            | Detail | `documents.download`                   |
| Activate / deactivate                    | Detail | `candidates.update`                    |
| Reach the edit page                      | Edit   | `candidates.update` (route guard)      |
| Edit core fields, relations, tags, notes | Edit   | `candidates.update`                    |
| Upload / set primary / replace documents | Edit   | `documents.upload` **and** route guard |

**Consequence to record in the design.** Upload moves behind the edit route, so a role holding
`documents.upload` without `candidates.update` loses upload in the UI (the API still accepts
it). None of the seeded roles is in that situation (`rrhh_admin` and `rrhh_user` hold both;
`manager_reader`, `readonly` and `system_admin` hold neither — see
`backend/Infrastructure/Persistence/Migrations/20260916065233_AddIdentityTables.cs:34-60`), but
administrators can build custom roles with `roles.manage`. Recommended: accept it and document
it in the role administration docs; the alternative (letting upload-only users into the edit
route) is a decision for the design, not a silent default.

### Files to touch

All paths relative to `frontend/`.

- `src/app/features/candidates/pages/candidate-detail-page.tsx` — read-only rendering.
- `src/app/features/candidates/pages/candidate-edit-page.tsx` — hosts every section; save
  stays on the page when editing; post-create navigation to `/edit`.
- `src/app/features/candidates/components/candidate-form.tsx` — drop `CandidateTags`.
- `candidate-languages.tsx`, `candidate-programs.tsx`, `candidate-education.tsx`,
  `candidate-experience.tsx`, `candidate-skills.tsx`, `candidate-tags.tsx`,
  `candidate-notes.tsx`, `candidate-documents.tsx` — explicit read-only mode.
- `src/assets/i18n/es.json` (+ `en.json` optional) — new copy **and** the migrated copy of every
  legacy file this change touches.
- `eslint.config.js` — remove `candidate-documents.tsx`, `candidate-form.tsx` and
  `candidate-edit-page.tsx` from `LEGACY_HARDCODED_COPY`. The list only shrinks, and all three
  files are touched here. This is a real share of the work; do not skip it.

Conventions: `usePermission()` hoisted to the top of each component, `useErrorToast()` for
failures, `useCandidate()` for the aggregate, every `name=` and `data-testid` kept (e2e specs
bind to `candidate-languages`, `candidate-skills`, `candidate-documents`, `candidate-tags`,
`candidate-notes`, …), plain co-located CSS, no inline `style`.

### Spanish copy

Suggested keys (final wording in the design); rendered with `t()`, nothing hardcoded:

| Key                           | Value                                                                                         |
| ----------------------------- | --------------------------------------------------------------------------------------------- |
| `candidate.edit.titleEdit`    | Editar candidato                                                                              |
| `candidate.edit.titleNew`     | Alta de candidato                                                                             |
| `candidate.edit.mainData`     | Datos principales                                                                             |
| `candidate.edit.saveHint`     | Los datos principales se guardan con «Guardar». El resto de secciones se guardan al instante. |
| `candidate.edit.newHint`      | Guarda el candidato para añadir idiomas, formación, experiencia, habilidades y documentos.    |
| `candidate.edit.saved`        | Datos principales guardados.                                                                  |
| `candidate.edit.backToDetail` | Ver candidato                                                                                 |

### Acceptance criteria

1. **Given** any user on `/app/candidates/:id`, **then** no add, remove, save, upload, set
   primary or replace control is rendered in any section, whatever their permissions. The
   «Editar» link and activate/deactivate appear only with `candidates.update`, as today.
2. **Given** a user with `documents.download` on the detail page, **then** documents can still
   be downloaded and the CV preview still works.
3. **Given** a user with `candidates.update` on `/app/candidates/:id/edit`, **then** they can
   edit the core fields, add and remove languages, programs, education, experience, skills and
   tags, add, edit and retire notes, and (with `documents.upload`) upload and manage documents,
   all from that page.
4. **Given** a relation change followed by a core-form «Guardar» on the edit page, **then** both
   persist and no 409 is raised by the user's own earlier write.
5. **Given** a successful «Guardar» on an existing candidate, **then** the user stays on the edit
   page with a confirmation, and nothing in the other sections is lost.
6. **Given** a new candidate saved for the first time, **then** the user lands on
   `/app/candidates/:id/edit` with every section available.
7. **Given** `/app/candidates/new`, **then** only the core form and the post-save hint are shown.
8. **Given** a user without `candidates.update`, **then** `/app/candidates/:id/edit` is still
   refused by the route guard, and direct API writes still answer 401/403 (re-asserted, not
   changed).
9. `candidate-documents.tsx`, `candidate-form.tsx` and `candidate-edit-page.tsx` are off
   `LEGACY_HARDCODED_COPY`, and `npm run lint` passes.
10. `npm run build:all`, `npm test`, `npm run e2e`, `npm run lint` and `npm run format:check`
    pass.

### Test coverage

**Frontend unit** (`tests/unit/`)

- `candidate-profile-sections.spec.tsx` — each section in read-only mode renders data and no
  controls, even with `candidates.update` and `documents.upload` granted; in edit mode, controls
  follow the permission as today.
- `candidate-documents.spec.tsx` — read-only mode keeps download, hides upload/primary/replace.
- New or extended edit-page spec — all sections present for an existing candidate, only the form
  for a new one; core save stays on the page; post-create navigation goes to `/edit`.
- `catalog-loading-states.spec.tsx` — update if it mounts sections through the detail page.
- `i18n.spec.ts` — new keys exist, nothing orphaned.

**E2E** (`tests/e2e/`, selectors by role, label or `data-testid`, no hardcoded Spanish)

- `candidate-profile.spec.ts`, `candidate-documents.spec.ts`, `candidate-tags-notes.spec.ts`,
  `candidate-api-cutover.spec.ts`, `secure-access.spec.ts` — they create a candidate and then
  edit relations, upload documents or add notes from the detail page. Move those interactions to
  the edit page and add an assertion that the detail page has no editing controls.
- `secure-access.spec.ts` — a `readonly` user reaching the detail page sees no controls and is
  refused the edit route.

### Non-functional requirements

- **Security and personal data.** No change to what is fetched, exposed or logged. The API stays
  the control for every write; the UI change is not presented as a security improvement.
- **Accessibility.** The edit page has one `h1` and a heading per section; the save-behaviour
  hint is associated with the page, and the core-save confirmation is announced (`role="status"`).
  Keyboard order runs core form → sections → documents.
- **Responsive.** One DOM tree for both widths; reuse the existing grid and `.span-all`; no
  `matchMedia` or `window.innerWidth`.
- **Performance.** The edit page loads the same aggregate the detail page does; no additional
  requests beyond the sections' existing ones.

### Out of scope

- A single draft with one «Guardar» for all sections (decision 3).
- Any backend, contract, permission or role change.
- Changing the content or layout of the CV preview (KTL-20).
- Unsaved-changes warnings when leaving the edit page with a dirty core form (worth a follow-up
  once the page holds more than the form).

### Decisions to make in the design

1. **Read-only prop shape.** `canEdit={false}` from the page vs. an explicit `readOnly` / `mode`
   prop on every section. Recommended: explicit prop, so tags and notes stop reading the
   permission on their own when they are rendered read-only.
2. **Upload-only roles.** Accept that `documents.upload` without `candidates.update` loses upload
   in the UI (recommended, documented), or widen the edit route guard.
3. **Core-save feedback.** Toast vs. inline status line, and whether the form re-snapshots from
   the saved aggregate after «Guardar».

### Risks

- **E2E churn.** Several specs drive relation and document edits through the detail page; they
  all move in this change, and a missed one fails only in Playwright.
- **Legacy copy migration.** Three files leave `LEGACY_HARDCODED_COPY`; it is easy to
  underestimate, and attribute copy (`aria-label`, `placeholder`, `title`) is not caught by lint.
- **Two save behaviours on one page.** Without clear copy, users may think «Guardar» also saves
  the sections, or that section changes need «Guardar». The hint and section layout carry this.

### Documentation to update

- `openspec/specs/candidate-management/spec.md` — where candidate data is viewed and where it is
  edited, and that relation edits persist immediately while core fields save with the form.
- `docs/ktl-22/` — short release note: what moved, the two save behaviours, the upload-role
  consequence.
- `README.md` (Spanish) — update any description of editing candidates from the detail page.
