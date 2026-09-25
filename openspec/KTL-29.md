# KTL-29 — One candidate page with per-panel editing

## [original]

I'd like to combine the view and edit pages of the candidate details into a single page into one.

Most read-only panels like "Datos principales", "Formación", "Competencias", etc. will have an "Edit button" that will convert them into the editable planels that currently are displayed on the edit page, with a "Save button" for each one that will update the panel information only. For this ticket we won't modify the layout of the panels so the read-only and the edit version of them will remain the same although we may end up making change in future tickets.

## [enhanced]

**Status:** Proposed

### Summary

KTL-22 split the candidate UI in two:

- `/app/candidates/:id` (`CandidateDetailPage`) is strictly read-only for everyone.
- `/app/candidates/:id/edit` (`CandidateEditPage`, gated by `candidates.update`) owns every change.

On the edit page, the core record is saved with «Guardar». Every other section persists each
add, change and removal immediately.

HR users bounce between the two routes to fix one field. This ticket merges them. The detail page
becomes the only page for an existing candidate. Each editable panel gets its own «Editar» button,
which swaps that panel, in place, for the editor the edit page shows today.

- **Form panels** (Datos principales, Competencias, Formación, Experiencia) stage their changes and
  write them with the panel's «Guardar». «Cancelar» discards them.
- **Action panels** (Notas, Documentos) keep their immediate per-item actions behind the same
  toggle, and close with «Hecho».
- **One panel is in edit mode at a time.** Unsaved changes are protected when the user switches
  panel or leaves the page.

The panels keep their current read-only and editable layouts. The CV preview beside the sections
(KTL-28) stays visible while editing.

### User story

As an HR user reviewing a candidate, I want to edit one section of the profile in place and save
just that section, so that I can correct data while reading the CV, without switching to a
separate edit page or saving unrelated sections.

### Decisions

| Topic                        | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Routes                       | `/app/candidates/:id` is the single page. `/app/candidates/:id/edit` becomes a `<Navigate replace>` redirect to `/app/candidates/:id`, so bookmarks keep working. It moves under the `candidates.read` guard: a reader who follows an old link lands on the read-only page instead of `/app`. `/app/candidates/new` stays a create-only page with the core form.                                                                                                                                             |
| After creation               | The app always opens `/app/candidates/:id`. Creators with `candidates.update` see «Editar» on every panel. Creators without it see the read-only page, as today.                                                                                                                                                                                                                                                                                                                                             |
| Editable panels              | Datos principales, Competencias, Formación, Experiencia, Notas and Documentos. Auditoría never gets «Editar».                                                                                                                                                                                                                                                                                                                                                                                                |
| Who sees «Editar»            | The permission the API checks for that panel's writes. `candidates.update` covers Datos principales, Competencias, Formación, Experiencia and Notas. `documents.upload` covers Documentos. Holding `documents.upload` without `candidates.update` therefore now allows managing documents, which the edit route used to block. That matches the API and the seeded roles. Hiding the button is not the control: the API stays the authorization boundary.                                                    |
| Save model, form panels      | Changes stay as a draft in the panel. «Guardar» writes the panel through the existing endpoints. «Cancelar» discards the draft and returns to read-only. **Datos principales** sends one `PUT /api/candidates/{id}`. **Formación** and **Experiencia** send one whole-set `PUT` each (`/education`, `/experience`). **Competencias** sends one `PUT` per changed family (`/skills`, `/languages`, `/programs`, `/tags`), one after another, each absorbing the new version. Unchanged families are not sent. |
| Competencias partial failure | The families that saved stay saved and leave the draft. The panel stays in edit mode, showing the Spanish error on the families that failed, and «Guardar» retries only those. The rows are not atomic, and the ticket does not add a combined endpoint.                                                                                                                                                                                                                                                     |
| Adding entries while staging | Adding a language, program or skill still uses the lowest active level in catalog order, and changing it on the chip still works. Both now only edit the draft. Education and experience «Añadir» appends to the draft list. Removal removes from the draft. Nothing is written until «Guardar».                                                                                                                                                                                                             |
| Save model, action panels    | **Notas:** add, edit and retire each apply at once, as today. **Documentos:** upload, mark primary and remove each apply at once, as today. «Editar» reveals those controls and «Hecho» hides them. Nothing is staged.                                                                                                                                                                                                                                                                                       |
| One panel at a time          | Opening «Editar» on another panel closes the open one when it is clean. When it is dirty, the app asks «¿Descartar los cambios?» through `confirmDialogService`, and cancelling keeps the current panel open. A form panel is dirty when its draft differs from the saved data. An action panel is dirty when the new-note box has text, a note is being edited, or a file is selected and not uploaded.                                                                                                     |
| Leaving the page             | With a dirty panel open, React Router's `useBlocker` (the app uses `createBrowserRouter`) asks for confirmation before a route change, through `confirmDialogService`. A `beforeunload` listener covers closing or reloading the tab.                                                                                                                                                                                                                                                                        |
| Removed (inactive) candidate | The API refuses relation and note writes on a removed candidate (`CandidateGuards.Removed`), so Competencias, Formación, Experiencia and Notas hide «Editar» while `isActive` is false. A muted header hint explains why. Datos principales and Documentos keep their buttons, because their endpoints accept writes.                                                                                                                                                                                        |
| Concurrency                  | Unchanged mechanism: the candidate's `version` travels with the aggregate in `CandidateService`, and every successful write absorbs the new one. Saving panel A and then panel B never conflicts with itself. Another user's edit since the page loaded makes the save fail with 409, a Spanish message and the draft kept.                                                                                                                                                                                  |
| Page header                  | Name, e-mail and phone stay in the header. They are edited in the Datos principales form, as today, and the header updates after its «Guardar». «Dar de baja» / «Reactivar» stay in the header toolbar.                                                                                                                                                                                                                                                                                                      |
| Backend                      | No change. Every endpoint, command, table and grant already exists.                                                                                                                                                                                                                                                                                                                                                                                                                                          |

### Existing behavior and contract

- **Routes** (`frontend/src/app/app.tsx`): `candidates/:id` sits under `RequirePermission
candidates.read`, `candidates/:id/edit` under `candidates.update`, and `candidates/new` under
  `candidates.create`.
- **Detail page** (`candidate-detail-page.tsx`): renders every section with `readOnly`, plus
  an «Editar» link to `/edit` and the activate/deactivate button.
- **Edit page** (`candidate-edit-page.tsx`):
  - Renders `CandidateForm`, which submits the core draft.
  - Renders the sections without `readOnly`.
  - Serves `/new` when there is no id; the create branch navigates to `/edit` or the detail page.
- **Section components** each own their editing today and persist per item:
  - `candidate-competencies.tsx` → `candidate-relation-section.tsx` (`CatalogValuePicker`
    chips, `RelationWrite` pending/error state per chip).
  - `candidate-education.tsx`, `candidate-experience.tsx`: an add form and a remove button per
    row, calling `CandidateRelationsService.add*/remove*`.
  - `candidate-notes.tsx`: add, edit and retire per note, through `CandidateNotesService`.
  - `candidate-documents.tsx`: upload, mark primary and remove, through `DocumentService`,
    gated by `documents.upload`.
- **Services:**
  - `CandidateRelationsService` validates entries (duplicates, degree required, year range,
    date range, negative years) and calls `CandidateService.set*(id, fullList)`.
  - `CandidateService.update(id, draft)` sends the core record with the held version.
- **API** (`backend/Web/Features/Candidates/CandidateEndpoints.cs`, unchanged):
  - `PUT /api/candidates/{id}` requires `candidates.update`.
  - `PUT /api/candidates/{id}/{languages|programs|education|experience|skills|tags}` requires
    `candidates.update`. Each replaces the whole set against `Version` and returns 409 on a
    mismatch.
  - `POST|PUT /api/candidates/{id}/notes[/{noteId}[/active]]` requires `candidates.update`.
  - The document routes require `documents.upload` or `documents.download`.
  - `PUT /{id}/active` requires `candidates.delete`.
- **Existing copy:** `candidate.detail.edit` = «Editar», `candidate.form.save` = «Guardar»,
  `candidate.detail.cancel` = «Cancelar», `candidate.edit.saved` = «Datos principales
  guardados.», `candidate.edit.saveHint`.

### Acceptance criteria

1. **Single page.** Given an editor on `/app/candidates/:id`, then every section is shown
   read-only. Datos principales, Competencias, Formación, Experiencia, Notas and Documentos each
   show an «Editar» button. Auditoría does not.
2. **Old route redirects.** Given any user with `candidates.read`, when they open
   `/app/candidates/:id/edit`, then the URL becomes `/app/candidates/:id`.
3. **Panel enters edit mode in place.** Given an editor, when they press «Editar» on Formación,
   then that panel shows today's education editor, with «Guardar» and «Cancelar». Its position
   on the page and the other panels are unchanged. Focus moves to the panel's first field.
4. **Staged save.** Given Formación in edit mode, when the editor adds one entry and removes
   another, then nothing is sent to the API. After «Guardar», exactly one
   `PUT /api/candidates/{id}/education` carrying the resulting list is sent, the panel returns
   to read-only with the new entries, a success toast is announced, and focus returns to
   «Editar».
5. **Cancel discards.** Given a panel with unsaved changes, when the editor presses
   «Cancelar», then no request is sent and the panel shows the saved data read-only.
6. **Datos principales.** Given the core panel in edit mode, when the editor changes the e-mail
   and presses «Guardar», then one `PUT /api/candidates/{id}` is sent, the header shows the new
   e-mail, and «Datos principales guardados.» is announced. If both names are empty, the
   existing Spanish validation shows and nothing is sent.
7. **Competencias only sends changed families.** Given Competencias in edit mode, when the
   editor adds a skill and changes a language's level, then «Guardar» sends `PUT /skills` and
   `PUT /languages` one after the other, and does not send `/programs` or `/tags`.
8. **Competencias partial failure.** Given the second family write is refused, then the first
   family shows as saved. The panel stays in edit mode with the Spanish error on the refused
   family, and «Guardar» retries only that family.
9. **Lowest level still applies.** Given the language level catalog's first active value is
   A1, when the editor adds a language in edit mode, then the draft chip shows A1 and it
   persists as A1 on «Guardar».
10. **Action panels.** Given Notas in edit mode, when the editor adds a note, then it persists
    at once, as today, and «Hecho» returns the panel to read-only. The same applies to
    Documentos' upload, mark primary and remove.
11. **One panel at a time.** Given Formación in edit mode and unchanged, when the editor presses
    «Editar» on Experiencia, then Formación closes and Experiencia opens. If Formación has
    unsaved changes, a confirmation asks to discard them first. Choosing «Cancelar» keeps
    Formación open with its draft.
12. **Leaving with unsaved changes.** Given a dirty panel, when the editor follows a link or uses
    the breadcrumb, then a confirmation is shown, and cancelling keeps them on the page with the
    draft intact. When they reload or close the tab, the browser's leave prompt is shown.
13. **Conflict.** Given another user changed the candidate after the page loaded, when the
    editor saves any panel, then the API returns 409, a Spanish conflict message is shown, the
    draft is kept, and nothing is overwritten.
14. **Permissions.**
    - A user with only `candidates.read` sees no «Editar» and no form control.
    - A user with `candidates.update` and without `documents.upload` sees «Editar» on every
      panel except Documentos.
    - A user with `documents.upload` and without `candidates.update` sees «Editar» only on
      Documentos.
15. **Removed candidate.** Given a removed candidate, then Competencias, Formación, Experiencia
    and Notas show no «Editar», and the header explains that the candidate must be reactivated
    to edit those sections.
16. **Creation.** Given a user with `candidates.create`, when they save the create form, then
    the app opens `/app/candidates/:id`.
17. **Fail closed.** Unauthenticated or unauthorized direct calls to the candidate, relation,
    note and document write endpoints are still refused, as covered today.
18. **CV preview.** Given the KTL-28 split layout at 1920×1080, when any panel is in edit mode,
    then the CV preview stays visible beside it.

### Implementation scope

Frontend only. Paths are relative to `frontend/`.

- **Routing** (`src/app/app.tsx`):
  - Replace the `candidates/:id/edit` route with a redirect component that is child of the
    `candidates.read` guard, and remove the `candidates.update` group.
  - Keep `candidates/new`.
- **Pages** (`src/app/features/candidates/pages/`):
  - `candidate-detail-page.tsx` becomes the single page. It holds `editingPanel` state (one
    panel id or `null`) and a dirty flag reported by the open panel. It wires the
    switch-confirmation, the `useBlocker` guard and the `beforeunload` guard.
  - Reduce `candidate-edit-page.tsx` to the create flow, and rename it
    `candidate-create-page.tsx` so the name matches what it does.
  - After creation, navigate to `/app/candidates/:id`.
- **Shared panel shell:** a small `candidate-panel.tsx` with pure helpers in a sibling
  `.logic.ts`. It renders the panel `h2`, the «Editar» button, and either the read-only or the
  edit body with the «Guardar»/«Cancelar» or «Hecho» footer. It keeps the layouts as they are.
- **Datos principales:**
  - The read-only body stays the current `dl.prop-list`.
  - The edit body is `CandidateForm`, with its submit replaced by the panel's «Guardar».
  - Add an `onCancel` and a dirty callback.
- **Form panels:** `candidate-education.tsx`, `candidate-experience.tsx`,
  `candidate-competencies.tsx` and `candidate-relation-section.tsx` switch from per-item writes
  to a local draft list.
  - Keep the chip, add-form and row markup.
  - `RelationWrite` pending and retry state per chip gives way to per-family save errors.
- **Action panels:** `candidate-notes.tsx` and `candidate-documents.tsx` keep their logic. Their
  `readOnly` prop is driven by the panel's edit state, and they report dirtiness.
- **Services:** validation stays in the service layer.
  - Replace the per-item `add*/update*/remove*` methods of `CandidateRelationsService` with
    whole-list savers (`saveEducation`, `saveExperience`, `saveLanguages`, `savePrograms`,
    `saveSkills`, `saveTags`). Each validates the full list with today's rules and
    `TranslatableError` keys, then calls `CandidateService.set*`.
  - Expose pure per-entry validators, so «Añadir» in a draft can reject a bad entry before
    «Guardar».
  - Remove per-item methods left unused.
- **Copy** (`src/assets/i18n/es.json`, `feature.section.element` keys):
  - «Editar» and «Hecho» buttons.
  - Accessible names «Editar {{section}}», «Guardar {{section}}» and «Cancelar la edición de
    {{section}}».
  - A per-panel success toast, one whole sentence per panel, for example «Formación guardada.».
  - The discard and leave confirmations (title, message, «Descartar», «Seguir editando»).
  - The removed-candidate hint.
  - Replace `candidate.edit.saveHint` and `candidate.edit.newHint`: the create page's note now
    says the other sections are edited on the candidate page after saving.
  - Remove keys that no longer render.
  - No file joins `LEGACY_HARDCODED_COPY`. Any listed file that is touched leaves it.
- **Test ids:**
  - Keep every existing `data-testid` and `name=`: `candidate-education`, `candidate-notes`,
    `candidate-documents`, `document-file`, `candidate-<family>-picker/input/add`,
    `candidate-cv-preview`, `candidate-active`, `breadcrumb-candidates`.
  - Add `candidate-panel-<panel>-edit`, `-save`, `-cancel` and `-done`, where `<panel>` is one
    of `main`, `competencies`, `education`, `experience`, `notes` or `documents`.
  - `candidate-edit-view` and `candidate-edit-hint` go away with the edit page. Update the
    specs that use them.
- **No backend, migration, grant or `ktl_runtime` change.** No new npm dependency.

### Verification

- **Unit** (`tests/unit/`, Vitest and Testing Library, doubles through `<ServicesProvider>`):
  - `candidate-detail-page.spec.tsx`: «Editar» visibility per permission combination and for a
    removed candidate. One panel at a time. The discard confirmation. The leave guard
    (`useBlocker`) blocks when dirty and passes when clean. Focus moves into the panel and
    back.
  - `candidate-profile-sections.spec.tsx`, `candidate-relation-section.spec.tsx`,
    `candidate-notes.spec.tsx`, `candidate-documents.spec.tsx`:
    - Staged add, change and remove send nothing until «Guardar».
    - «Guardar» sends one whole-list call per changed family.
    - «Cancelar» restores the saved data.
    - A partial Competencias failure keeps the failed families in the draft.
  - `candidate-relations.service.spec.ts`: whole-list validation (duplicates, degree required,
    year and date range, negative years) with `TranslatableError` keys.
  - `candidate-edit-page.spec.tsx` becomes a create-page spec: after creation it navigates to
    `/app/candidates/:id`, and the `/edit` redirect lands on the detail route.
- **E2e** (`tests/e2e/`): every record keeps its `Date.now()` marker. Selectors use roles and
  test ids.
  - Update `candidate-crud`, `candidate-profile`, `candidate-api-cutover`,
    `candidate-documents`, `candidate-tags-notes`, `catalogs-crud`, `candidate-list-operations`,
    `security-ops` and `navigation-responsive`, which today expect or open `/edit`.
  - Add a flow that edits and saves each form panel, cancels one, and hits the leave guard.
  - Check the 390px no-horizontal-scroll case with a panel in edit mode.
- **Security** (`tests/e2e/secure-access.spec.ts` and `tests/security/`):
  - The `readonly` user sees no `candidate-panel-*-edit` and no form, and `/edit` redirects to
    the read-only page instead of `/app`.
  - The existing direct-write refusals (401/403) stay green.
- **Backend:** no change expected. Run `npm run test:backend` as a regression check.
- **Gates:** `npm test`, `npm run lint`, `npm run format:check`, `npm run build:all`,
  `npm run e2e`.

### Documentation

- Delta spec for `openspec/specs/candidate-profile-pages/spec.md`:
  - **Replace:** "Candidate detail page is read-only", "Edit page owns every candidate change",
    "Edit page states its save behaviours" and "Candidate creation continues on the edit page"
    give way to requirements for the single page:
    - per-panel edit mode, one panel at a time;
    - staged save and cancel for form panels, and immediate actions for Notas and Documentos;
    - unsaved-change protection;
    - the `/edit` redirect;
    - creation landing on the candidate page.
  - **Modify:** "Detail page keeps viewing and status actions", "Competencias panel and stacked
    sections" (one page order, including Auditoría) and "CV preview beside the candidate
    sections" (no separate edit page).
- `docs/ktl-29/release-notes.md`: the route change, the new save model, and the permission
  nuance for `documents.upload`.
- `README.md` (Spanish) wherever it describes the view and edit pages.

### Non-functional requirements

- **Security and personal data:**
  - No new data path and no new endpoint.
  - «Editar» mirrors the permission the API checks, and the API stays the control.
  - Drafts live only in component state. Nothing goes to `localStorage` or the logs.
- **Concurrency:** every write carries the held version. A 409 never loses the user's draft.
- **Accessibility:**
  - «Editar», «Guardar» and «Cancelar» have accessible names that include the section.
  - On entering edit mode, focus goes to the panel's first field. On save or cancel, it
    returns to that panel's «Editar».
  - The save confirmation uses the existing live-region toast.
  - The confirmations are the existing accessible dialog.
  - One `h1` for the page and one heading per panel. Everything is keyboard operable.
- **Responsive:** same DOM at every width, and no horizontal scroll at 390px with a panel open.
  The KTL-28 split and sticky CV preview are unchanged.
- **Performance:** a saved panel refreshes from the write's response, as today, with no extra
  aggregate fetch.

### Observed, out of scope

- The header's «Dar de baja» / «Reactivar» is shown to holders of `candidates.update`, but the
  API requires `candidates.delete`. Worth a follow-up ticket.

### Out of scope

- Visual redesign of the read-only or editable panel layouts. This is planned for later tickets.
- Modal or overlay editing. It was considered and rejected because it would hide the CV preview.
- A combined atomic endpoint for the Competencias families.
- Staging note or document actions.
- Changes to the create page beyond its hint and its post-save destination.
