## Context

See `proposal.md` (Why) and `specs/candidate-profile-pages/spec.md` for the requirements. The
brief is `openspec/KTL-22.md`.

Current state that shapes the approach (paths relative to `frontend/src/app/features/candidates/`):

- `pages/candidate-detail-page.tsx` renders all eight sections. The five relation sections
  (`candidate-languages`, `-programs`, `-education`, `-experience`, `-skills`) receive
  `canEdit = usePermission('candidates.update')` from the page. `candidate-tags` and
  `candidate-notes` take no such prop and call `usePermission('candidates.update')` themselves.
  `candidate-documents` calls `usePermission('documents.upload' | 'documents.download')` itself.
- `pages/candidate-edit-page.tsx` renders only `CandidateForm`, which appends `CandidateTags` for an
  existing candidate. `save` calls `candidateService.update`/`create` and then always navigates to
  the detail page.
- Every write takes its version through `candidateService.versionOf(id)`, which reads the aggregate
  the service last absorbed. Relation writes absorb the returned aggregate, so a later core
  `update` on the same page sends the fresh version. The core form keeps its own draft in
  `useState`, snapshotted once from `candidate`; absorbing a relation write does not reset it.
- `/candidates/:id/edit` is guarded by `RequirePermission permission="candidates.update"` and
  `/candidates/new` by `candidates.create` (`app.tsx`).
- Toasts render inside `role="status" aria-live="polite"` (`core/layout/app-layout.tsx`), so a toast
  already meets the "announced confirmation" requirement.
- `candidate-documents.tsx`, `candidate-form.tsx` and `candidate-edit-page.tsx` are on
  `LEGACY_HARDCODED_COPY`; touching them moves their copy to `es.json`.

## Goals / Non-Goals

**Goals:**

- Make "read-only" a property of where a section is rendered, not of who is looking.
- Reuse the existing section components on both pages instead of forking view and edit copies.
- Keep every service, API call, concurrency path and permission check exactly as it is.

**Non-Goals:**

- A unified draft with one «Guardar» (rejected, see Decision 3).
- An unsaved-changes guard on the edit page.
- Any backend, contract, migration, grant, role or dependency change.

## Decisions

### 1. Sections take an explicit `readOnly` prop

Every section component (`CandidateLanguages`, `CandidatePrograms`, `CandidateEducation`,
`CandidateExperience`, `CandidateSkills`, `CandidateTags`, `CandidateNotes`,
`CandidateDocuments`) gets `readOnly?: boolean`, default `false`. Inside, the component computes

```ts
const canUpdate = usePermission('candidates.update'); // hoisted, top of the component
const editable = !readOnly && canUpdate;
```

(`CandidateDocuments`: `canManage = !readOnly && usePermission('documents.upload')`; download
keeps `documents.download` in both modes.) The relation sections' `canEdit` prop is removed and
replaced by this, so all eight sections follow one rule.

_Alternative considered:_ pass `canEdit={false}` from the detail page. Rejected: it covers only the
five relation sections, and tags, notes and documents would still show controls to editors unless
they also got a prop. A single `readOnly` prop gives the detail page one guarantee that does not
depend on the user's permissions, which is what the first spec requirement asks for.

_Alternative considered:_ separate read-only display components. Rejected: it duplicates the markup,
`data-testid`s and empty states that unit and e2e specs bind to, and the two copies would drift.

### 2. Detail page renders every section with `readOnly`

`candidate-detail-page.tsx` passes `readOnly` to all eight sections and stops computing `canEdit`
for them. It keeps `usePermission('candidates.update')` only for the «Editar» link and
activate/deactivate. `CandidateCvPreview` is unchanged. In read-only mode `CandidateDocuments` still
observes `Pending` documents (a read), so the availability state on the detail page still updates
after an upload made on the edit page.

### 3. Save semantics stay; the edit page explains them

The core form keeps its own «Guardar». Sections keep persisting each change through their existing
services. A single draft was rejected: it would batch up to eight writes from the SPA, each bumping
the candidate version, with partial-failure and 409 handling in the middle of a sequence. That
reverses the per-section design KTL-8/KTL-21 chose, for little user benefit. The page shows a hint
(`candidate.edit.saveHint`) under the heading instead.

### 4. Edit page composition

`candidate-edit-page.tsx`, for an existing candidate, renders:

1. The heading, the save hint and a «Ver candidato» link back to the detail page.
2. `CandidateForm` in a panel titled «Datos principales». `CandidateTags` is removed from the form.
3. A `.grid.two` of section panels, in the detail page's order: languages, programs, education,
   experience, skills, tags, then notes (`.span-all`), then documents.

For `/candidates/new` it renders the heading, `CandidateForm` and the `candidate.edit.newHint` line.
The form keeps `key={candidateId || 'new'}`, so it remounts when the route changes from new to edit.

### 5. Save flow and navigation

- **Existing candidate:** `await candidateService.update(id, draft)`, then
  `toastService.show(t('candidate.edit.saved'), …)` and stay on the page. The form's draft already
  equals what was saved, so it is not re-snapshotted. The toast region is `role="status"`, so the
  confirmation is announced without a second live region.
- **New candidate:** `await candidateService.create(draft)`, then navigate to
  `/app/candidates/${id}/edit` when `usePermission('candidates.update')` holds (hoisted at the top
  of the page), otherwise to `/app/candidates/${id}`. This covers a custom role with
  `candidates.create` but not `candidates.update` without sending it to a route the guard refuses.
- Errors still go through `useErrorToast()`.

### 6. Authorization model

Unchanged. The API remains the control for every write, and route guards remain
`candidates.create` / `candidates.update`. The SPA gains no permission logic beyond hoisted
`usePermission` reads that decide which controls to render; `readOnly` narrows rendering further and
never widens it.

**Consequence (documented, accepted):** document upload is only reachable on the edit page, so a
custom role holding `documents.upload` without `candidates.update` loses upload in the UI. No seeded
role is in that situation (`AddIdentityTables` migration: `rrhh_admin` and `rrhh_user` hold both;
`manager_reader`, `readonly` and `system_admin` hold neither). Widening the edit route guard to
"update OR upload" was rejected: the page would then also have to handle an actor who can reach it
but edit nothing but documents, which complicates every section for a hypothetical role. The
release note and the role administration docs say that upload requires both permissions in practice.

### 7. Copy and lint

New keys under `candidate.edit.*` (title, hints, saved, back link, main-data title). While touching
`candidate-form.tsx`, `candidate-edit-page.tsx` and `candidate-documents.tsx`, all their JSX and
attribute copy moves to flat keys: `candidate.form.*`, `candidate.edit.*` and
`candidate.profile.documents.*`. The Spanish values stay exactly as rendered today. The three
files come off `LEGACY_HARDCODED_COPY`. Existing plain `Error`s with Spanish text inside services
stay; the components only migrate what they render.

### Stack, data and storage impact

- **Stack:** none. No npm or NuGet dependency.
- **Data model / migrations / grants:** none.
- **Storage:** none. Documents are uploaded and downloaded through the same `DocumentService` calls.
  No path, key or new URL reaches the page.

### Test strategy

- **Unit (Vitest):** in `candidate-profile-sections.spec.tsx`, `candidate-tags.spec.tsx`,
  `candidate-notes.spec.tsx` and `candidate-documents.spec.tsx`, each section with
  `readOnly` renders data and no controls even when every permission is granted; without
  `readOnly`, controls still follow the permission. A new `candidate-edit-page.spec.tsx` covers:
  all sections present for an existing candidate, only form + hint for new, a core save staying on
  the page and showing the toast, and post-create navigation to `/edit` or detail depending on
  `candidates.update`. `candidate-form.spec.tsx` no longer expects tags. `i18n.spec.ts` covers the
  new keys.
- **E2E (Playwright):** `candidate-profile`, `candidate-documents`, `candidate-tags-notes`,
  `candidate-api-cutover` and `secure-access` perform their edits on `/edit`. Add assertions that the
  detail page exposes no section form or upload input for an admin, and that a `readonly` user is
  refused `/edit`. Selectors stay on `data-testid`, role and `name`.
- **Security evidence:** no API surface changes, so the existing API authorization tests stand. The
  e2e and unit checks above show the UI fails closed (a reader gets no controls and no edit route),
  and `npm run security:rls` / `security:storage` are run to confirm nothing regressed.

## Risks / Trade-offs

- [E2E specs drive edits from the detail page and fail only in Playwright] → Grep `tests/e2e` for
  every section `data-testid` and `/app/candidates/` navigation before starting; run the full
  `npm run e2e`, not only the touched specs.
- [Legacy-copy migration is larger than it looks, and lint misses attribute copy] → Review
  `placeholder`, `aria-label` and `title` in the three files by hand; unit specs resolve text from
  `es.json`.
- [Users confuse the two save behaviours] → Hint under the heading, core form in its own titled
  panel, sections visually separate.
- [Upload-only custom role loses upload in the UI] → Documented; the API is unchanged, so an admin
  can grant `candidates.update` or a later ticket can revisit it.
- [A dirty core draft is lost if the user navigates away] → Same as today; tracked as a follow-up,
  out of scope.

## Migration Plan

Frontend-only; ships with the next SPA build. No data migration. Rollback is reverting the change.
The Compose `nginx` image must be rebuilt for 4200 to show it.
