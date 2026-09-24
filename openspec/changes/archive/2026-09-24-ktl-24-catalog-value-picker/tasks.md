Requirement references:

- `specs/catalog-value-picker/spec.md` (P)
- `specs/saved-search-presets/spec.md` (S)
- `specs/candidate-profile-pages/spec.md` (C)

Design decisions are cited as D1–D8. Paths are relative to `frontend/` unless stated.

## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-24` from an up-to-date `main`

## 1. Dependency and app setup (D1)

- [x] 1.1 `npm install react-aria-components` (from `frontend/`). Confirm it lands in `dependencies`
      with a React 19-compatible peer range and that `package-lock.json` changes accordingly
- [x] 1.2 Wrap the app in react-aria's `I18nProvider locale="es-ES"` in `src/main.tsx`
- [x] 1.3 Record `npm run build` bundle sizes before and after (for the release note)

## 2. Picker logic and copy (P: type-to-filter; D2, D6)

- [x] 2.1 Create `src/app/features/catalogs/components/catalog-value-picker.logic.ts`:
      `PickerItem` type, `filterOptions(options, query, items)` (NFD, diacritics stripped, lower
      case, excludes present values, keeps catalog order) and the test-id helpers from D3
- [x] 2.2 Add `tests/unit/catalog-value-picker.logic.spec.ts`: accent and case folding, exclusion,
      order, empty query
- [x] 2.3 Add the `catalogPicker.*` keys and `search.criteria.mode.label` to `es.json` (and
      `en.json`), with correct accents

## 3. Picker component (P: every requirement; D1–D3)

- [x] 3.1 Build `catalog-value-picker.tsx`: `ComboBox` input with filtered `ListBox`, chips via
      `TagGroup`, remove button with an accessible name, and Delete/Backspace removal
- [x] 3.2 Add the chip editor: `DialogTrigger` + `Popover` with a level `ToggleButtonGroup` (a
      `Select` above six levels) and the `renderDetails` slot. Focus returns to the chip on close.
      Tags open no editor
- [x] 3.3 Implement `levelMode`. Optional: commit at once with `anyLevelLabel`. Required: hold one
      uncommitted item, open its editor, call `onAdd` only once a level is chosen, and discard it on
      close
- [x] 3.4 Implement `readOnly` (chips only), `disabled` (chips and a disabled input), and the item
      `status`/`error` rendering with a retry calling `onRetry`
- [x] 3.5 Write `catalog-value-picker.css`: Kepler tokens, `data-*` state selectors, wrapping chips,
      a popover width constrained to the viewport, and the shared focus ring. No inline styles and
      no media queries
- [x] 3.6 Add `tests/unit/catalog-value-picker.spec.tsx` covering every P scenario with `user-event`
      (keyboard-only flow included). Query popover content through `screen`

## 4. Search host (S: shared editor, tag criteria; D4)

- [x] 4.1 Rebuild `features/search/components/criteria-group.tsx` as an adapter: map
      `CriteriaFilter` ↔ `PickerItem`, `levelMode="optional"`, and a mode `ToggleButtonGroup` as
      `headerAction` (`data-testid="search-<kind>-mode"`), shown only from two criteria
- [x] 4.2 In `search-criteria-form.tsx`, remove `drafts`, `addCriterion` and `EMPTY_DRAFTS` (from
      `search-criteria.logic.ts`). Wire add, change and remove as immutable `onFiltersChange` updates
- [x] 4.3 Render `SearchCriteriaSummary` inside the form only while collapsed
- [x] 4.4 Check the three hosts (advanced search, preset edit, position form) render identical
      pickers, and that editing a filter still does not re-run the search

## 5. Candidate host (C: edit page owns every change; D5)

- [x] 5.1 Add `updateLanguage`, `updateSkill` and `updateProgram` to
      `candidate-relations.service.ts`: replace by id, apply the duplicate check (excluding itself)
      and the non-negative years check, then call the existing `set<Relation>()`
- [x] 5.2 Create `candidate-relation-section.logic.ts` with the four-kind definition table
      (families, `idPrefix`, section `testId`, `toItem`/`fromItem`, details, service methods)
- [x] 5.3 Create `candidate-relation-section.tsx`: the heading, empty state, per-item
      `status`/`error` map, retry of the last intent, `readOnly`, and `disabled` from
      `useCatalogStatus()` with `CatalogStatusNotice`. It also holds the certification and years
      detail renderers (labelled, `name="certification"` / `name="yearsExperience"`)
- [x] 5.4 Use the section on `candidate-edit-page.tsx` and (read-only) `candidate-detail-page.tsx`,
      keeping the `candidate-languages`, `candidate-skills`, `candidate-programs` and
      `candidate-tags` test ids
- [x] 5.5 Delete `candidate-languages.tsx`, `candidate-skills.tsx`, `candidate-programs.tsx`,
      `candidate-tags.tsx` and `candidate-tags.css` (moving any still-needed styles into the picker
      CSS)
- [x] 5.6 Remove copy keys left unused (`search.criteria.*.add`, `search.criteria.option.select`,
      `candidate.profile.*.add`, `candidate.profile.action.remove`,
      `candidate.profile.option.select`, …) after grepping for each

## 6. Review and update existing unit tests

- [x] 6.1 Update `tests/unit/search-criteria-form.spec.tsx`: the `tagDraft`/`tagLevelDraft`
      assertions become "the tag picker offers no level"; add add/level/mode/summary-collapsed cases
- [x] 6.2 Replace `tests/unit/candidate-tags.spec.tsx` and the relation parts of
      `candidate-profile-sections.spec.tsx` with `tests/unit/candidate-relation-section.spec.tsx`.
      Cover the four kinds, required-level discard with no service call, in-place level change,
      pending and error with retry, read-only on the detail page, and outage disabling every kind
- [x] 6.3 Extend `tests/unit/candidate-relations.service.spec.ts` (or the nearest service spec) for
      the update methods and their validation
- [x] 6.4 Update `tests/unit/catalog-loading-states.spec.tsx`, `search-criteria-summary.spec.tsx`
      and any spec importing a deleted component. Run `tests/unit/i18n.spec.ts` for missing or
      orphaned keys

## 7. Run unit and integration suites

- [x] 7.1 Run `npm test` from `frontend/`, inspect the output and fix failures
- [x] 7.2 Run `npm run build:all` and confirm it passes with warnings as errors
- [x] 7.3 Confirm there is no backend, PostgreSQL schema or storage change: `git status` shows no
      `backend/` diff and no migration. Run `npm run test:backend` (Docker running) to confirm the
      untouched API suites still pass. After a candidate relation change in e2e (section 8),
      confirm through the API response that the collection holds exactly the expected entries

## 8. End-to-end

- [x] 8.1 Start `docker compose up` (repository root) so the dev server on :4300 can proxy the API
- [x] 8.2 Create `tests/e2e/support/catalog-picker.ts` with `addValue`, `setLevel`, `removeValue`
      and `chips`, using only test ids and roles
- [x] 8.3 Rewrite `tests/e2e/advanced-search.spec.ts` (`skillDraft`, `add-skill`, `skillMode`,
      `tagDraft`, `tagLevelDraft`) on the helper. Run the spec and inspect the report
- [x] 8.4 Rewrite `tests/e2e/candidate-profile.spec.ts` and `candidate-api-cutover.spec.ts`
      (`select[name="language"|"level"|"skill"]`) on the helper. Add an in-place level change and a
      required-level discard. Run both specs
- [x] 8.5 Rewrite `tests/e2e/candidate-tags-notes.spec.ts` (`candidate-tag-select`, `tagDraft`,
      `add-tag`) and `catalogs-crud.spec.ts` (a new language is offered by typing its name). Run
      both specs
- [x] 8.6 Add a 390 px check to `tests/e2e/navigation-responsive.spec.ts`: the candidate edit page
      with several chips and an open editor has no horizontal page scroll. Run the spec
- [x] 8.7 Grep `tests/` for `Draft"`, `add-skill`, `add-language`, `add-program`, `add-tag`,
      `candidate-tag-select` and `select[name="language"]`. Nothing may remain. Then run the full
      `npm run e2e` and inspect the report
- [x] 8.8 Restore seed data afterwards: retire the candidates and presets the specs created, as the
      existing specs do

## 9. Security evidence (fail closed)

- [x] 9.1 Update selectors in `tests/security/ktl-21-tags-notes.spec.ts`, keeping every assertion,
      and run `npm run test:security`
- [x] 9.2 Run `secure-access.spec.ts` to confirm a `readonly` profile still gets no picker controls
      on the detail page and is refused the edit route. Confirm the existing API authorization
      suites (relations `PUT` 401/403) pass unchanged in 7.3
- [x] 9.3 Grep the diff to confirm no relation value or search term is written to `console`, the URL
      or `document.title`. Run `npm run security:rls` and `npm run security:storage` to confirm the
      gates are unaffected

## 10. Lint and format

- [x] 10.1 Run `npm run lint`: no hardcoded-copy errors, and `LEGACY_HARDCODED_COPY` unchanged
      apart from removals
- [x] 10.2 Run `npm run format:check` and fix any formatting

## 11. Documentation

- [x] 11.1 Write `docs/ktl-24/release-notes.md`: the new interaction (type to add, chips, level on
      the chip), the required-level rule on candidates, the new dependency with its reason and
      bundle-size delta, and the replaced test identifiers
- [x] 11.2 Check `README.md` (Spanish) for descriptions of adding languages, skills, programs or
      tags, and update any found
- [x] 11.3 Run `openspec validate ktl-24-catalog-value-picker --strict` and fix any finding
