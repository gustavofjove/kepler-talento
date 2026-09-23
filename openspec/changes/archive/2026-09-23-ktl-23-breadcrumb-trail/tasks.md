Requirement reference: `specs/primary-navigation/spec.md`, requirement «Breadcrumb trail on
sub-pages» (R). Design decisions are cited as D1–D8. Paths are relative to `frontend/` unless stated.
Prerequisite: KTL-22 (`ktl-22-edit-page-owns-editing`) is merged to `main`.

## 0. Create Feature Branch

- [x] 0.1 Confirm KTL-22 is merged, then create and switch to branch `feat/KTL-23` from an
      up-to-date `main`

## 1. Shared Breadcrumb component (R: structure, accessibility, narrow viewport; D1–D3, D7)

- [x] 1.1 Add the `breadcrumb.*` keys to `src/assets/i18n/es.json`: `ariaLabel` «Ruta de
      navegación», `candidates` «Candidatos», `positions` «Posiciones», `admin` «Admin», `presets`
      «Presets», `edit` «Editar», `newCandidate` «Nuevo candidato», `newPosition` «Nueva posición»
      and `newPreset` «Nuevo preset». Add the `en.json` values
- [x] 1.2 Create `src/app/shared/components/breadcrumb.tsx` exporting
      `BreadcrumbItem { label; to?; current?; testId? }`. It renders `<nav className="breadcrumb" aria-label data-testid="breadcrumb"><ol>`. An item
      with `current` is a `<span aria-current="page">` even when `to` is set; an item with only `to`
      is a `<Link>`; any other item is a plain `<span>`. Every label carries a `title` with the full
      label
- [x] 1.3 Create `src/app/shared/components/breadcrumb.css`: a wrapping flex list in small muted
      token colours with the shared focus ring, a rotated-border chevron on `li + li::before` (no
      text `content`), and per-label ellipsis truncation with `max-width`. No media queries and no
      inline styles
- [x] 1.4 Add `tests/unit/breadcrumb.spec.tsx` covering the landmark name, the `<ol>`/`<li>`
      structure, link, current and plain-text rendering (including `current` winning over `to`),
      `testId` pass-through and the `title` carrying the full label

## 2. Candidate pages (R: candidate trails, loading, failure, permission rule; D4–D6)

- [x] 2.1 In `pages/candidate-detail-page.tsx`, hoist `usePermission('candidates.read')`. Render
      `Candidatos` › full name (current) in the loaded state, and `Candidatos` alone in the loading,
      error and not-found states. Wrap the early returns in fragments so `candidate-detail-error`
      stays on the same element, and keep the «Volver» buttons
- [x] 2.2 In `pages/candidate-edit-page.tsx`, render `Candidatos` › full name
      (`testId: 'candidate-edit-view'`, linking to the detail page) › `Editar` (current) for an
      existing candidate; `Candidatos` alone while loading or failed; and `Candidatos` ›
      `Nuevo candidato` (current) on `/new`. Link `Candidatos` and the name only when
      `candidates.read` is held
- [x] 2.3 Remove the «Ver candidato» toolbar button from the edit page. Delete
      `candidate.edit.backToDetail` from `es.json` and `en.json` after confirming with a grep that
      nothing else references it

## 3. Position pages (R: position trails, unsaved edit; D4, D5)

- [x] 3.1 In `position-detail-page.tsx`, render `Posiciones` › title (current) once loaded. In the
      loading return, render `Posiciones` alone before the status paragraph
- [x] 3.2 In `position-form-page.tsx`, add `storedTitle` state set in the load effect and hoist
      `usePermission('positions.read')`. Render `Posiciones` › stored title (linking to the detail
      page) › `Editar` (current) when editing, `Posiciones` › `Nueva posición` (current) when
      creating, and `Posiciones` alone while not `ready`. Keep «Cancelar»

## 4. Preset edit page (R: preset trails, non-activatable Admin; D4, D5)

- [x] 4.1 In `admin/presets/preset-edit-page.tsx`, add `storedName` state set in the load effect.
      Render `Admin` (plain) › `Presets` (link to `PRESETS_ROUTE`) › stored name (current) when
      editing, or › `Nuevo preset` (current) when creating; `Admin` › `Presets` alone while loading
      or after a failed load

## 5. Review and update existing unit tests

- [x] 5.1 Update `tests/unit/candidate-edit-page.spec.tsx`: the `candidate-edit-view` href
      assertion still passes on the breadcrumb link; add the trail for an existing candidate and for
      `/new`, the loading trail (`Candidatos` only, still a link), the no-`candidates.read` case
      (text, no link), and that no separate «Ver candidato» button remains
- [x] 5.2 Add a candidate detail page unit spec (none mounts the page today) covering the trail in
      the loaded, loading, error and not-found states
- [x] 5.3 Extend `tests/unit/position-pages.spec.tsx` with the detail and form trails: the title
      link to the detail page, the stored title unchanged after typing in the title field, and the
      `/new` trail
- [x] 5.4 Extend `tests/unit/preset-edit-page.spec.tsx`: `Admin` is plain text and `Presets` links
      to the list, the stored name is unchanged after typing, and the `/new` and load-failure trails
- [x] 5.5 Add a unit assertion that representative top-level pages (candidate list, position list,
      preset list, dashboard) render no `breadcrumb` test id
- [x] 5.6 Review any other unit spec that asserts the edit page toolbar or the removed key, and run
      `tests/unit/i18n.spec.ts` to confirm the new keys resolve

## 6. Run unit and integration suites

- [x] 6.1 Run `npm test` from `frontend/`, inspect the output and fix failures
- [x] 6.2 Run `npm run build:all` and confirm it passes with warnings as errors
- [x] 6.3 Confirm there is no backend, PostgreSQL schema or storage change: `git status` shows no
      `backend/` diff and no migration. Run `npm run test:backend` (Docker running) to confirm the
      untouched API suites still pass

## 7. End-to-end

- [x] 7.1 Start `docker compose up` (repository root) so the Playwright dev server on :4300 can
      proxy the API
- [x] 7.2 Extend `tests/e2e/candidate-crud.spec.ts` so it goes list → detail → edit, then uses
      `candidate-edit-view` back to the detail page, then the `breadcrumb` «Candidatos» link back to
      the list. Use `data-testid` selectors only, with no Spanish text. Run the spec and inspect the
      report
- [x] 7.3 Extend `tests/e2e/positions.spec.ts` so it goes position detail → edit, then the
      breadcrumb back to the detail page and to the list. Run the spec
- [x] 7.4 Extend `tests/e2e/advanced-search-presets.spec.ts` (or the preset e2e that opens the edit
      page) so the breadcrumb returns to the preset list. Run the spec
- [x] 7.5 Add a 390 px check to `tests/e2e/navigation-responsive.spec.ts`: a candidate detail page
      with a long name has no horizontal page scroll, and the breadcrumb is visible. Run the spec
- [x] 7.6 Run `candidate-profile.spec.ts` and `secure-access.spec.ts` unchanged to prove
      `candidate-edit-view` still works and is still absent for `readonly`. Then run the full
      `npm run e2e` and inspect the report
- [x] 7.7 Restore seed data afterwards: retire or deactivate the candidates, positions and presets
      the specs created, as the existing specs do

## 8. Security evidence (fail closed)

- [x] 8.1 Confirm with the existing `secure-access.spec.ts` and route-guard unit specs that direct
      URL access to candidate, position and preset sub-pages is still refused for unauthorized
      profiles. The breadcrumb adds no route and changes no guard
- [x] 8.2 Grep the diff to confirm the candidate name is not written to `document.title`, the URL,
      `console` or any log call

## 9. Lint and format

- [x] 9.1 Run `npm run lint`: no hardcoded-copy errors, and `LEGACY_HARDCODED_COPY` is unchanged
      apart from removals
- [x] 9.2 Run `npm run format:check` and fix any formatting

## 10. Documentation

- [x] 10.1 Write `docs/ktl-23/release-notes.md`: which pages gain a trail, the removal of the «Ver
      candidato» button (test id kept), the permission-dependent link rule, and that list filters
      are not restored
- [x] 10.2 Check `README.md` (Spanish) for any description of the edit page's «Ver candidato»
      button or of returning to lists, and update it if found
- [x] 10.3 Run `openspec validate ktl-23-breadcrumb-trail --strict` and fix any finding
