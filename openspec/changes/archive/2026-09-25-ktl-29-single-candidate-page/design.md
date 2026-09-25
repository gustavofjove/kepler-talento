## Context

See `proposal.md` (Why) and the delta specs for the required behaviour. The current state that
shapes the approach:

- **Routes** (`frontend/src/app/app.tsx`):
  - `candidates/:id` → `CandidateDetailPage`, under `RequirePermission candidates.read`.
  - `candidates/:id/edit` → `CandidateEditPage`, under `candidates.update`.
  - `candidates/new` → `CandidateEditPage`, under `candidates.create`.
- **Section components** take a `readOnly` prop, and each persists per item today:
  - `CandidateCompetencies` → `CandidateRelationSection` (a `CatalogValuePicker` per family, with
    a pending/error `RelationWrite` per chip).
  - `CandidateEducation`, `CandidateExperience` (an add form, and a remove button per row).
  - `CandidateNotes`, `CandidateDocuments`.
- **`CandidateRelationsService`** validates each add/update/remove against the loaded aggregate,
  then calls `CandidateService.set<Family>(id, fullList)`.
- **`CandidateService`** holds the aggregate with its `version` and replaces it with each write's
  response. Sequential writes on one page therefore never conflict with each other (KTL-22).
- **API:** `PUT /api/candidates/{id}` plus one whole-set `PUT` per relation family, each taking
  `Version` and returning 409 on a mismatch. Notes and documents have their own per-item
  endpoints. Nothing on the backend changes.
- **Router:** React Router 7.18 with `createBrowserRouter`, so `useBlocker` is available. No page
  uses it yet.
- **Dialogs:** `confirmDialogService.confirm()` is the accessible confirmation used by every
  page.

## Goals / Non-Goals

**Goals:**

- One page component for an existing candidate, with a small, reusable per-panel edit contract.
- Staged drafts for the form panels, reusing today's editor markup unchanged.
- Validation that stays in the service layer and is reachable both per entry (on «Añadir» into a
  draft) and per list (on «Guardar»).
- A single owner for "which panel is open, and is it dirty", so switching and leaving can be
  guarded in one place.

**Non-Goals:**

- Visual redesign of the panels.
- A generic form library, or a new state-management approach.
- Backend work, including an atomic multi-family endpoint.
- Changing the position or preset pages, even though they have similar view/edit splits.

## Decisions

### D1. The page owns the edit state; panels report dirtiness

`CandidateDetailPage` holds `editing: PanelId | null` and `dirty: boolean`. It renders each panel
through a `CandidatePanel` shell (`components/candidate-panel.tsx`, with its pure helpers in
`candidate-panel.logic.ts`). The shell receives:

- `id`, `title` and `canEdit`;
- `mode`: `'form'` (Guardar/Cancelar) or `'actions'` (Hecho);
- `readView`, and `editView(api)`, where `api = { setDirty, close }`.

The shell renders the `h2`, the «Editar» button and the footer buttons, moves focus into the
panel on open, and returns it to «Editar» on close.

- **Why:** the one-panel-at-a-time rule and the leave guard need one owner that knows the dirty
  state. Pushing it into a service or context would outlive the page and complicate tests.
- **Alternative considered:** each panel owns its own toggle and publishes dirtiness through a
  context. That produces more wiring for the same result, and the "which one is open" invariant
  becomes distributed.

### D2. Form panels hold a local draft and save through the service

Datos principales, Educación, Experiencia and Competencias keep a `useState` draft, initialised
from the aggregate when edit mode opens. The existing editor markup is reused:

- chip pickers, add forms and remove buttons edit the draft;
- `CandidateForm` gets a `formId`, so the shell's «Guardar» submits it (`<button form=…>`) and the
  form keeps its own validation message.

Dirtiness is a structural comparison between the draft and the saved list, done in `.logic.ts`
helpers.

`CandidateRelationSection` loses the per-chip `RelationWrite` and `run()` machinery. It becomes
a controlled `items` / `onItemsChange` row over the family draft. `CandidateEducation` and
`CandidateExperience` do the same with their list draft.

- **Why:** the ticket keeps the layouts. Controlling the existing components from a draft is the
  smallest change that makes «Guardar» and «Cancelar» meaningful.
- **Alternative considered:** keep immediate writes and make «Guardar» a no-op close. The user
  rejected this, because it gives no cancel and no per-panel save.

### D3. `CandidateRelationsService` moves to whole-list saves with exposed validators

The per-item `add*`, `update*` and `remove*` methods are replaced by:

- `saveLanguages`, `savePrograms`, `saveSkills`, `saveTags`, `saveEducation` and
  `saveExperience`, each `(candidateId, entries)`. They validate the whole list with today's
  rules and `TranslatableError` keys (duplicates, degree required, end year 1950…current+1,
  date range, negative years), normalise it (for example, `endDate` cleared when `isCurrent`),
  then call `CandidateService.set<Family>`.
- Pure per-entry validators (`validateLanguageEntry(list, entry)`, and so on), exported from the
  same service module. The panels call them on «Añadir», so a duplicate or invalid entry never
  enters the draft.

Draft entries get client ids from `crypto.randomUUID()` as today. The API treats unknown ids as
new rows (`CandidateRelationWrite.Existing`), and it keeps the ids of existing rows, so an edited
entry keeps its identity and migration provenance.

- **Why:** AGENTS.md says validation stays in the service layer. Whole-list save matches the API
  contract exactly.
- **Alternative considered:** validating in the components. The repository rules forbid it.

### D4. Competencias saves changed families sequentially

`candidate-competencies.logic.ts` gets `changedFamilies(saved, draft)`. «Guardar» awaits
`save<Family>` for each changed family, in the display order. Each call absorbs the new version
before the next is sent.

- A failure is recorded per family, and the loop continues with the remaining families.
- A family that succeeded is removed from the pending set. Its saved state becomes the new
  baseline, so a retry sends only the failures.
- The panel closes only when the pending set is empty.
- **Why sequential and not parallel:** every family write carries the same aggregate version.
  Parallel writes would conflict with each other (409).
- **Alternative considered:** a new atomic endpoint that sets all four families. It is out of
  scope (no backend change), and per-family recovery is acceptable for this ticket.

### D5. Notas and Documentos keep their logic; `readOnly` comes from edit mode

`CandidateNotes` and `CandidateDocuments` keep their immediate actions. The panel passes
`readOnly={!editing}` and a `onDirtyChange` callback. The dirty conditions are:

- **Notas:** the new-note text is non-empty, or `editing !== null`.
- **Documentos:** `selectedFile` is set.

`CandidateDocuments` keeps its own document state and scan polling across mode switches, because
the shell hides only the controls and does not remount the list. The read body and the edit body
are the same component instance, with a different `readOnly`.

### D6. Panel switching and leaving are guarded in the page

- **«Editar» on panel B:** if nothing is open, or the open panel is clean, B opens. Otherwise the
  page awaits `confirmDialogService.confirm({ … danger: true })`. Confirming discards the draft
  (the panel unmounts its edit view) and opens B.
- **Leaving within the app:** `useBlocker(dirty)`. When the blocker is `blocked`, the page asks
  through the same dialog and then calls `proceed()` or `reset()`.
- **Reload or close:** a `beforeunload` listener is registered only while `dirty`, calling
  `event.preventDefault()`.
- **Why `useBlocker`:** it is the router's supported mechanism for data routers, and it covers
  breadcrumb, navigation and back/forward in one place. `window.confirm` would bypass the
  accessible dialog.

### D7. Permissions per panel mirror the API

| Panel                                                          | Permission                           |
| -------------------------------------------------------------- | ------------------------------------ |
| Datos principales, Competencias, Educación, Experiencia, Notas | `usePermission('candidates.update')` |
| Documentos                                                     | `usePermission('documents.upload')`  |

Both are hoisted to the top of the page. The removed-candidate rule, `!item.isActive`, disables
«Editar» for Competencias, Educación, Experiencia and Notas, which the API refuses via
`CandidateGuards.Removed`. The header then shows a muted hint.

- This is a UI convenience that mirrors the API. The API remains the control (principle 3).
  `candidates.update` stays out of the documents path, because the document endpoints check only
  `documents.upload`.

### D8. Routes

- `candidates/:id/edit` becomes a child of the `candidates.read` guard, rendering a tiny
  `CandidateEditRedirect` (`<Navigate to={`/app/candidates/${id}`} replace />`).
- The `candidates.update` route group is removed.
- `CandidateEditPage` is renamed `CandidateCreatePage`, keeps only the create branch, and always
  navigates to `/app/candidates/:id` after saving.
- The breadcrumb `Editar` trail disappears with the page.

### D9. Copy and test ids

New `es.json` keys:

- `candidate.panel.edit` («Editar»), `candidate.panel.save` («Guardar»),
  `candidate.panel.cancel` («Cancelar»), `candidate.panel.done` («Hecho»).
- Accessible-name keys with `{{section}}` interpolation.
- A per-panel saved toast, one whole sentence each.
- `candidate.panel.discardTitle`, `candidate.panel.discardMessage`,
  `candidate.panel.discardConfirm` («Descartar»), `candidate.panel.keepEditing` («Seguir
  editando»).
- `candidate.panel.leaveTitle` and `candidate.panel.leaveMessage`.
- `candidate.detail.removedHint`.

`candidate.edit.saveHint` is removed, and `candidate.edit.newHint` is reworded.

Test ids: `candidate-panel-<id>-edit`, `-save`, `-cancel` and `-done`, where `<id>` is one of
`main`, `competencies`, `education`, `experience`, `notes` or `documents`. Every existing
`data-testid` and `name=` stays, except `candidate-edit-view` and `candidate-edit-hint`, which go
with the edit page.

### Stack, data, authorization and storage impact

| Area          | Impact                                                                                                                             |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Stack         | Frontend only, with no new dependency.                                                                                             |
| Data model    | None: no migration, table or grant.                                                                                                |
| Authorization | Unchanged API checks. The UI mirrors them per panel (D7).                                                                          |
| Storage       | Unchanged. Uploads and downloads go through the same permission-checked API routes, and drafts are never persisted in the browser. |

### Test strategy

- **Unit (Vitest and Testing Library):**
  - The panel shell (focus, footer, accessible names).
  - The page's switching, leave and permission matrix.
  - Each form panel's staged add, remove, cancel and save.
  - Competencias `changedFamilies` and the partial failure.
  - The whole-list validators of `CandidateRelationsService`.
  - The create page's navigation.
  - The `/edit` redirect.
- **E2e (Playwright):**
  - Rewrite the journeys that relied on `/edit`.
  - A new panel-editing journey (save, cancel, switch guard, leave guard).
  - A 390px pass.
- **Security:** `secure-access.spec.ts` asserts that a reader sees no `candidate-panel-*-edit`,
  and that `/edit` redirects to the read-only page. The existing direct-write refusals must stay
  green.
- **Backend:** regression run only.

## Risks / Trade-offs

- **[Competencias is not atomic]** A partial save leaves some families written. → Per-family
  errors, retry only the failures, keep the panel open, and state this in the spec.
- **[Behaviour change for relation editing]** Users used to instant saves may leave with unsaved
  chips. → Dirty-state guards on switch, route change and tab close, plus explicit
  Guardar/Cancelar.
- **[Broad e2e churn]** Nine e2e specs navigate through `/edit`. → The redirect keeps URLs valid.
  Update the URL assertions and selectors in one task group, and run each touched spec.
- **[Blocker interplay with post-save navigation]** The create page navigates after saving. →
  Only the candidate page registers the blocker, and only while `dirty`, so saving clears it
  first.
- **[Document polling across mode switches]** Remounting would restart polling. → Keep one
  `CandidateDocuments` instance and toggle `readOnly` (D5).
- **[Lost per-chip retry UX]** KTL-24's per-chip pending/error state goes away. → It is replaced
  by per-family errors on save. Existing specs for chip retry are removed alongside.

## Migration Plan

This is a frontend-only deploy.

- **Rollback:** revert the change. The API contract is untouched, so old and new bundles work
  against the same backend.
- **Bookmarks:** old `/edit` bookmarks redirect after deploy.
- **Data:** no data migration.
