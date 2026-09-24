## Why

Adding a catalog value (a skill, language, program or tag, optionally with a level) is the most
repeated editing gesture in the app, and it is clumsy everywhere. It takes three interactions per
item. Long catalogs sit behind native dropdowns that cannot be filtered by typing. A level cannot be
changed without removing and re-adding the item. It also looks different on each screen. The same
interaction is implemented twice: once as the search `CriteriaGroup` (shared by search, presets and
positions) and four more times as near-identical candidate relation sections. KTL-24 redesigns it
once, as a shared picker, and wires it into both places so every screen gets the same, better control.

## What Changes

- A new shared **catalog value picker**: a chip field with a type-to-filter input. The pieces:
  - At rest, one line: label, chips and a (+) add button. (+) turns into a focused input with every
    value not yet held already listed; typing narrows the list.
  - Enter or a click adds the highlighted value as a chip; values already present are not offered.
  - Chips show value and level. They are removable with a button or with Delete/Backspace when
    focused.
  - Activating a chip opens a popover to set its level (in catalog order) and the family's detail
    fields: certification for languages, years of experience for programs.
  - Tags have no level, so their chips open no popover.
- **Search, preset and position criteria** use the picker:
  - A new criterion starts at «Cualquier nivel», and its level can be set from the chip.
  - The ANY/ALL choice becomes a toggle next to the family label, shown only from two criteria.
  - The filter value shape is unchanged.
- **Candidate edit page**: the four relation sections (languages, skills, programs, tags) become one
  table-driven section on the picker.
  - Adding a value with a level family opens the level popover. The entry is saved only once a
    level is chosen; closing without one discards it.
  - The level and details of an existing entry can be **changed in place**.
  - Each save shows pending and error states on its chip, with a retry.
- **Candidate detail page** shows the same chips read-only.
- A catalog outage disables every picker the same way, with the existing catalog notice.
- New runtime dependency **`react-aria-components`** for combobox, tag list, popover and toggle
  behaviour (justified in the design).
- **BREAKING (UI and test hooks only):** the native `select`s and Add buttons disappear. Their
  `name`s and `data-testid`s (`skillDraft`, `tagLevelDraft`, `add-skill`, `skillMode`,
  `candidate-tag-select`, `select[name="language"]`, …) are replaced by one picker test-id scheme.
  Section-level `data-testid`s (`candidate-languages`, `candidate-skills`, `candidate-programs`,
  `candidate-tags`) are kept.

**Actors.**

- Recruiters (`rrhh_user`, `rrhh_admin`) editing candidates and searching.
- Anyone holding `positions.manage` editing position requirements.
- Administrators holding `presets.manage` editing presets.
- Any reader on the candidate detail page.

**Key entities.** None new. The picker edits search criteria (`{ value, level }` plus a mode per
family) and candidate relation entries (language, skill, program, tag), all resolved against the
existing business catalogs.

**Assumptions.**

- The candidate relation collection `PUT` endpoints accept a changed entry in place, as they replace
  the whole collection. No backend change is needed.
- Level families are short enough to present as a toggle group; a longer family falls back to a
  select.

**Edge cases.**

- A newly added candidate value whose level popover is closed without a choice: nothing is written.
- Re-adding a search value already present: the value is not offered; its level is changed from the
  chip instead.
- A value or level deactivated in the catalog but still held: shown as a chip, never offered.
- A relation write refused by the API (conflict, validation, authorization): the chip shows the
  error. Other chips and the core form are unaffected.
- Catalogs fail to load: inputs disabled with the notice. Existing chips still render.
- Narrow viewports: chips wrap and the popover stays inside the viewport at 390 px.

**Success criteria.**

1. Adding a search criterion without a level takes one interaction after typing (Enter). Adding a
   candidate entry takes two (Enter, then a level).
2. A candidate entry's level can be changed without removing it.
3. The search, preset and position pages expose identical pickers (same accessible names and test
   identifiers) for identical filters.
4. Every add, change and remove is operable by keyboard alone.
5. Only one picker implementation exists in the frontend. The four candidate relation components
   and the old `CriteriaGroup` markup are gone.
6. Build, unit, e2e, security, lint and format checks pass.

## Capabilities

### New Capabilities

- `catalog-value-picker`: the shared interaction for choosing catalog values with an optional or
  required level and detail fields. Covers type-to-filter adding, chips, in-place level and detail
  editing, read-only and disabled states, async pending/error states, keyboard operation, stable
  test identifiers, localization and narrow-viewport behaviour.

### Modified Capabilities

- `saved-search-presets`: «Shared criteria editor and summary» and «Tag criteria are edited and
  summarized like other criteria». The shared editor's multi-value families use the catalog value
  picker. "Same field names" becomes "same accessible names and test identifiers". The mode control
  is shown only from two criteria. The summary appears in the editor only while
  it is collapsed.
- `candidate-profile-pages`: «Edit page owns every candidate change». Relation entries can be
  changed in place, and an entry with a level family is saved only once its level is chosen.
  «Candidate detail page is read-only» already forbids every editing control, so it is unchanged.

## Impact

- **Frontend only:**
  - new `frontend/src/app/features/catalogs/components/catalog-value-picker.tsx`, `.css` and
    `.logic.ts`
  - rebuilt `features/search/components/criteria-group.tsx`; `search-criteria-form.tsx` loses its
    draft state
  - new `features/candidates/components/candidate-relation-section.tsx` replacing
    `candidate-languages.tsx`, `candidate-skills.tsx`, `candidate-programs.tsx` and
    `candidate-tags.tsx`
  - `candidate-relations.service.ts` gains `updateLanguage`, `updateSkill` and `updateProgram`
  - `candidate-edit-page.tsx` and `candidate-detail-page.tsx`
  - app root: react-aria `I18nProvider`
  - `es.json` (new `catalogPicker.*` keys; unused `*.add` / remove keys removed)
- **Dependency:** `react-aria-components` (runtime), justified in the design under principle 2.
- **Tests:**
  - new picker unit spec
  - updated `search-criteria-form`, `candidate-profile-sections`, `candidate-tags` and
    `catalog-loading-states` unit specs
  - a shared e2e helper, and rewritten selectors in `advanced-search`, `candidate-profile`,
    `candidate-api-cutover`, `candidate-tags-notes` and `catalogs-crud`
  - selectors in `tests/security/ktl-21-tags-notes.spec.ts`
- **No** backend, endpoint, contract, migration, grant or role change. Stored presets and position
  requirements are untouched.
- **Personal data, RLS, storage, roles.**
  - The picker renders only relation values the page already loads. No new request, log line, URL
    or storage path carries them (principle 1).
  - The change touches no RLS policy, grant, storage access or role definition.
  - Hiding editing controls on the read-only page is not a control: the route guard and the API stay
    the authorization boundary and fail closed as today (principle 3).
- **Docs:**
  - `saved-search-presets` and `candidate-profile-pages` specs (through deltas), and the new
    `catalog-value-picker` spec
  - `docs/ktl-24/release-notes.md`
  - `README.md` only if it describes adding languages or skills
