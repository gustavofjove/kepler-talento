## Why

Users on a candidate, position or preset sub-page (detail, create, edit) have no consistent way back
to the list the page belongs to. Candidate detail has no route back once loaded. Position detail and
preset pages have none at all. Candidate edit and position forms each use a different one-off button.
Users fall back on the browser's Back button or the top navigation. KTL-23 adds one shared,
accessible breadcrumb trail to every sub-page. It follows KTL-22, which reshapes the candidate edit
page header this change replaces.

## What Changes

- A breadcrumb trail is rendered above the heading of every sub-page:
  - candidate detail, new and edit (`/app/candidates/:id`, `/new`, `/:id/edit`)
  - position detail, new and edit (`/app/positions/:id`, `/new`, `/:id/edit`)
  - preset new and edit (`/app/admin/presets/new`, `/:id/edit`)
- The trails are, for example, «Candidatos › Nombre Apellido › Editar», «Posiciones › Título» and
  «Admin › Presets › Nombre del preset». The last segment is the current page and is never a link.
- Top-level pages (Dashboard, the Candidatos, Búsqueda, Posiciones and Presets lists, Catálogos,
  Usuarios, Roles, Importación, Auditoría) get no breadcrumb. The primary navigation already marks
  them.
- «Admin» is plain text, because the Admin navigation parent has no destination. A segment whose
  destination the viewer may not open is also plain text.
- Record segments show the loaded record (candidate name, position title, preset name), never an
  unsaved draft value. They are left out while the record is loading.
- List segments link to the plain list route. They do not restore list filters, sort or page.
- The trail is static: it does not change with the page the user arrived from.
- **BREAKING (UI only):** the candidate edit page's «Ver candidato» header button is removed. The
  candidate-name segment of the breadcrumb replaces it and keeps its `data-testid`
  (`candidate-edit-view`). The position form keeps its «Cancelar» button.

**Actors.** Recruiters (`rrhh_user`, `rrhh_admin`) moving between candidate lists and profiles.
Anyone holding `positions.read` or `positions.manage` working with positions. Administrators holding
`presets.manage`.

**Key entities.** None new. The trail reads the candidate aggregate, position and saved-search
preset that each page already loads.

**Assumptions.**

- KTL-22 is merged before implementation starts.
- Every dynamic label is already loaded on its page, so the trail needs no extra request.

**Edge cases.**

- A record that is still loading, failed to load or was not found: the parent segment is still
  shown.
- A very long candidate name or position title: it is truncated visually, and the full text stays
  available.
- A custom role holding `candidates.update` without `candidates.read`: the «Candidatos» and name
  segments render as text.
- A title or name edited but not yet saved: the trail keeps the saved value.

**Success criteria.**

1. Every sub-page listed above shows its trail, and every non-final segment the viewer may open
   navigates to its destination in one activation.
2. No top-level page renders a breadcrumb.
3. The trail is a named navigation landmark with an ordered list and the current page marked for
   assistive technology. It is keyboard-operable and causes no horizontal scroll at 390 px.
4. Existing unit and e2e specs that use `candidate-edit-view` pass unchanged. Build, unit, e2e, lint
   and format checks pass.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `primary-navigation`: adds a requirement for a breadcrumb trail on sub-pages, covering:
  - which pages have one and which do not
  - the trail for each page
  - the non-link Admin segment
  - the permission-dependent link rule
  - loaded-record labels
  - accessibility and narrow-viewport behaviour

  Existing requirements are unchanged.

## Impact

- **Frontend only:**
  - new `frontend/src/app/shared/components/breadcrumb.tsx` and `breadcrumb.css`
  - `features/candidates/pages/candidate-detail-page.tsx` and `candidate-edit-page.tsx`
  - `features/positions/position-detail-page.tsx` and `position-form-page.tsx`
  - `features/admin/presets/preset-edit-page.tsx`
  - `frontend/src/assets/i18n/es.json` (new `breadcrumb.*` keys)
- **Tests:**
  - new unit spec for the component
  - candidate edit, position pages and preset edit unit specs
  - e2e `candidate-crud`, `positions` and `advanced-search-presets` specs
  - a narrow-width check
- **No** backend, endpoint, contract, migration, grant, dependency or role change.
- **Personal data, RLS, storage, roles.**
  - The candidate name in the trail is already shown in the page heading. No new field, request,
    log line, URL, document title or storage path carries it (principle 1).
  - The change touches no RLS policy, grant, storage access or role definition.
  - Rendering a segment as text instead of a link is a courtesy. Route guards and the API remain
    the authorization control and fail closed as today (principle 3).
- **Docs:** `primary-navigation` spec (through this delta) and `docs/ktl-23/release-notes.md`.
  `README.md` only changes if it describes the removed «Ver candidato» button.
