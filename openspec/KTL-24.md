# KTL-24 — Shared catalog value picker (redesign of the criteria and relation controls)

## [original]

Would it make sense using for a part of Candidate details the same form component as in Search,
Preset or Position? Maybe with some constraints but at the end it's the same: adding languages,
skills, etc.

I really need to redesign that component because it has a really bad UX.

## [enhanced]

**Status:** Proposed
**Depends on:** KTL-22 (edit page owns editing, merged), KTL-23 (breadcrumb, merged)
**Followed by:** KTL-25 (level criteria match "or higher"), which relabels the level shown on a
search chip and should land after this ticket.

### Summary

The same interaction exists in two places: pick a catalog value, optionally a level, then click
Add, and see the list of what was added. Neither place does it well.

| Where                                           | Component                                                                                                            | Used by                                                           |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Search criteria (skill, language, program, tag) | `features/search/components/criteria-group.tsx`, rendered four times by `search-criteria-form.tsx`                   | Advanced search, preset create/edit, position create/edit         |
| Candidate relations                             | `candidate-languages.tsx`, `candidate-skills.tsx`, `candidate-programs.tsx`, `candidate-tags.tsx` (four near-copies) | Candidate edit page (editable), candidate detail page (read-only) |

Problems today:

- **Three interactions per item** (value select, level select, Add button), repeated per family.
- **Native selects over long catalogs**: skills and programs cannot be filtered by typing.
- **Changing a level means removing and re-adding** the item.
- **Inconsistent presentation**: badge + × in search, badge + «Quitar» button on candidates, chips
  for candidate tags; the ANY/ALL select only appears after the first criterion, so the layout jumps.
- **Rarely used fields crowd the add row**: certification (languages) and years (programs).
- **Inconsistent outage handling**: only the languages section disables its controls when catalogs
  fail to load.

This ticket builds **one shared, presentational picker** in the new design and wires it into two
places: `CriteriaGroup` and a single table-driven candidate relation section that replaces the four
copies. It is a **frontend-only** change: no endpoint, contract, permission, table or grant changes.

### User stories

- **As a recruiter** editing a candidate, I type «ing», press Enter, pick «B2», and English B2 is
  saved, without hunting through a long dropdown or clicking a separate Add button.
- **As a recruiter**, I change a candidate's English from B2 to C1 by clicking the chip, instead of
  removing it and adding it again.
- **As a recruiter or hiring manager** building a search, preset or position, I add several skills
  quickly by typing, and set a level only where it matters.
- **As any user**, the control looks and behaves the same wherever I meet it.

### Decisions taken (2026-09-23)

1. **Design first, then share.** The shared component is built directly in the new design; the
   current markup is not refactored into a shared component first.
2. **Library: `react-aria-components`** (Adobe, 1.x, peer `react ^19`). It supplies the behaviour
   of every piece (combobox filtering and keyboard, tag list keyboard and removal, popover
   positioning and focus return, toggle groups) and leaves styling to plain CSS through `className`
   and `data-*` state attributes. Downshift was considered: smaller, but it covers only the combobox
   and chip list, leaving the popover to be built by hand. A hand-built control was rejected because
   keyboard, focus and popover behaviour is the costly and error-prone part. The design records
   this as the reason for the new runtime dependency.
3. **One presentational component, state and persistence stay in the hosts.** The picker holds no
   filter or candidate state and performs no API call; it takes items and callbacks (which may be
   async).
4. **Scope is the four catalog-value families** (skill, language, program, tag). Education and
   experience are multi-field forms, not value pickers, and are out of scope.
5. **Candidate save semantics are unchanged**: every add, change and remove persists immediately,
   as today. Changing a level or detail replaces the relation collection through the existing
   `PUT /api/candidates/{id}/<relation>` endpoints, so no backend change is needed.

### Target interaction

```
Idiomas                                         Coincidir: [Cualquiera | Todos]   ← search only
┌──────────────────────────────────────────────────────────────────────────┐
│ [Inglés · C1 ×] [Francés · B2 ×] [Alemán · Cualquier nivel ×]            │
│ Añadir idioma…▏                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

- **Type to add.** A combobox filters the family's active catalog values by substring,
  case-insensitively. Enter or a click adds the highlighted value. Values already present are not
  offered.
- **Chips.** Each item is a chip showing value and level. Arrow keys move between chips; Delete or
  Backspace removes the focused chip; each chip also has a remove button with an accessible name
  («Quitar Inglés»).
- **Level and details on the chip.** Activating a chip opens a popover with:
  - the level family as a toggle group in catalog order (a `Select` when the family has more than
    six active levels);
  - the family's detail fields: certification for languages, years of experience for programs.
- **Search hosts**: a new chip starts at «Cualquier nivel», so adding is one step; the level can be
  set afterwards from the chip.
- **Candidate hosts**: the level is required, so adding a value opens the popover at once. The item
  is saved only when a level is chosen; closing the popover without one discards it. Saves show a
  pending state on the chip; a failure keeps the chip in an error state with the message and a retry.
- **ANY/ALL** (search only) is a two-option toggle in the group header, always visible, disabled
  while the group has fewer than two chips.
- **Tags** use the same component with no level family, so the popover never opens.
- **Read-only** (candidate detail page): chips only, with no input, remove buttons or popover.
- **Catalog outage**: every host shows `CatalogStatusNotice` and disables the input the same way.
- **Deactivated values** already held stay visible as chips but are never offered in the input
  (existing `candidate-management` rule for tags, applied to every family).

### UI changes (`frontend/`)

**New shared component** in `src/app/features/catalogs/components/`:

- `catalog-value-picker.tsx` + `catalog-value-picker.css` (plain CSS, Kepler tokens, no inline
  styles); pure helpers in `catalog-value-picker.logic.ts`.
- Indicative props (final shape in the design):

  ```ts
  interface CatalogValuePickerProps<T extends PickerItem> {
    idPrefix: string; // stable ids, names and test ids per host and family
    label: string; // translated family label
    valueFamily: CatalogFamily;
    levelFamily?: CatalogFamily;
    levelRequired: boolean; // candidate: true, search: false
    anyLevelLabel?: string; // search only
    items: T[];
    readOnly?: boolean;
    disabled?: boolean; // catalog outage
    renderDetails?: (item, onChange) => ReactNode; // certification / years
    onAdd(item): void | Promise<void>;
    onChange(item): void | Promise<void>;
    onRemove(item): void | Promise<void>;
    headerAction?: ReactNode; // the ANY/ALL toggle
  }
  ```

**Hosts:**

| File                                                                             | Change                                                                                                                                              |
| -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `features/search/components/criteria-group.tsx`                                  | Rebuilt on the picker; ANY/ALL toggle as `headerAction`; drafts state leaves `search-criteria-form.tsx`                                             |
| `features/search/components/search-criteria-form.tsx`                            | Drop the per-kind `drafts` state and `addCriterion`; keep the "adding an existing value replaces its level" rule as an update                       |
| `features/candidates/components/candidate-relation-section.tsx` (new)            | One section driven by a table (family, level family, details renderer, service methods), replacing the four copies                                  |
| `candidate-languages.tsx`, `-skills.tsx`, `-programs.tsx`, `-tags.tsx`           | Removed, or reduced to thin wrappers over the new section if that keeps call sites and specs simpler (design decides)                               |
| `features/candidates/services/candidate-relations.service.ts`                    | Add `updateLanguage`, `updateSkill`, `updateProgram` (replace one entry, same validation and duplicate rules)                                       |
| `features/candidates/pages/candidate-edit-page.tsx`, `candidate-detail-page.tsx` | Render the new section; `data-testid`s of the sections (`candidate-languages`, `candidate-skills`, `candidate-programs`, `candidate-tags`) are kept |
| `src/app/core/…` (app root)                                                      | `I18nProvider locale="es-ES"` from react-aria so its few internal strings are Spanish                                                               |

**Copy** (`src/assets/i18n/es.json`, flat keys; `en.json` optional). Indicative:

| Key                            | Spanish                        |
| ------------------------------ | ------------------------------ |
| `catalogPicker.addPlaceholder` | Añadir {{label}}…              |
| `catalogPicker.noResults`      | No hay coincidencias.          |
| `catalogPicker.remove`         | Quitar {{value}}               |
| `catalogPicker.edit`           | Editar {{value}}               |
| `catalogPicker.level`          | Nivel                          |
| `catalogPicker.anyLevel`       | Cualquier nivel                |
| `catalogPicker.levelMissing`   | Elige un nivel para guardarlo. |
| `catalogPicker.saving`         | Guardando…                     |
| `catalogPicker.retry`          | Reintentar                     |
| `search.criteria.mode.label`   | Coincidir                      |

Existing keys that become unused (`search.criteria.*.add`, `candidate.profile.*.add`,
`candidate.profile.action.remove`, …) are removed. No file is added to `LEGACY_HARDCODED_COPY`.

### Data contract and API

None. `SearchFilters` keeps its shape (`{ value, level }` criteria plus a mode per family), so stored
presets and position requirements are unaffected. Candidate relations keep using the existing
collection `PUT` endpoints and their optimistic concurrency.

### Acceptance criteria

```gherkin
Scenario: Adding a search criterion in one step
  Given the criteria editor on the search, preset or position page
  When the user types part of a skill name in the skill picker and presses Enter
  Then a chip for that skill appears with "Cualquier nivel"
  And the value is no longer offered in the picker

Scenario: Setting a search criterion's level from its chip
  Given a skill chip with "Cualquier nivel"
  When the user opens the chip and chooses a level
  Then the chip shows that level and the filter value carries it

Scenario: ANY/ALL toggle is stable
  Given a criteria group with fewer than two chips
  Then its mode toggle is visible and disabled
  When a second chip is added
  Then the toggle becomes enabled

Scenario: Adding a candidate language requires a level
  Given a user with candidates.update on the candidate edit page
  When they add "Inglés" from the language picker
  Then the level popover opens and nothing is saved yet
  When they choose "B2"
  Then the language is saved through the API and the chip shows "Inglés · B2"

Scenario: Discarding an unfinished candidate item
  Given the level popover is open for a newly added language
  When the user closes it without choosing a level
  Then no chip remains and no write is sent

Scenario: Changing a candidate level in place
  Given a candidate with "Inglés · B2"
  When the user opens that chip and chooses "C1"
  Then the collection is saved with C1 and no remove-then-add is needed

Scenario: Failed save
  Given the API refuses a relation write
  Then the chip shows an error with a retry action and the message from errorText
  And other chips are unaffected

Scenario: Read-only detail page
  Given any user on the candidate detail page
  Then languages, skills, programs and tags render as chips with no input, remove button or popover

Scenario: Catalog outage
  Given catalogs fail to load
  Then every picker on the page shows the catalog notice and its input is disabled

Scenario: Same editor on every search host
  Given the search page, the preset edit page and the position edit page with identical filters
  Then the criteria pickers expose the same accessible names and test identifiers

Scenario: Keyboard operation
  Then a user can add, change the level of, and remove an item using only the keyboard
```

### Test coverage

- **Unit** (`frontend/tests/unit/`, Vitest + Testing Library + `user-event`), locating by role and
  accessible name:
  - New `catalog-value-picker.spec.tsx`: filtering, add on Enter, already-present values hidden,
    chip keyboard removal, popover level change, required level discards on close, read-only,
    disabled, async pending and error states.
  - Update `search-criteria-form.spec.tsx` (the `tagDraft` / `tagLevelDraft` assertions become
    "the tag picker has no level control"), `candidate-profile-sections.spec.tsx`,
    `candidate-tags.spec.tsx` (`candidate-tag-select`), `catalog-loading-states.spec.tsx`.
  - Service: `candidate-relations` update methods keep duplicate and negative-years validation.
  - jsdom caveat: react-aria popovers render in a portal; tests query `screen`, not the host container.
- **E2E** (`frontend/tests/e2e/`, Playwright, `data-testid` or role selectors, no Spanish text).
  These bind to the native selects and must be rewritten through one shared helper
  (`tests/e2e/support/catalog-picker.ts`):
  - `advanced-search.spec.ts` (`skillDraft`, `add-skill`, `skillMode`, `tagDraft`, `tagLevelDraft`)
  - `candidate-profile.spec.ts`, `candidate-api-cutover.spec.ts` (`select[name="language"]`,
    `select[name="level"]`, `select[name="skill"]`)
  - `candidate-tags-notes.spec.ts` (`candidate-tag-select`, `tagDraft`, `add-tag`)
  - `catalogs-crud.spec.ts` (`select[name="language"]` used to check a new value is offered)
  - `navigation-responsive.spec.ts` or a new spec: pickers and popover at 390 px without
    horizontal scroll.
- **Security** (`frontend/tests/security/ktl-21-tags-notes.spec.ts`): keep its assertions; update
  selectors only. No new endpoint, so no new API authorization tests; existing ones must still pass.
- **Backend:** no changes.

### Non-functional requirements

- **Security and personal data:** no new data paths. The picker renders only what the host passes;
  candidate relation values are already on the page. No logging of values.
- **Dependency:** `react-aria-components` is the only new runtime dependency; justified in the
  design (principle 2). Import only the components used so the bundle carries nothing else.
- **Accessibility:** react-aria's combobox, tag group and popover semantics; every control has a
  Spanish accessible name; focus returns to the input after adding and to the chip after the popover
  closes.
- **Responsive:** one DOM tree; chips wrap; the popover stays within the viewport at 390 px; no
  `matchMedia` or `innerWidth`.
- **Performance:** filtering is client-side over already-loaded catalogs; no extra requests.

### Documentation

- `openspec/specs/saved-search-presets/spec.md`: «Shared criteria editor and summary» and «Tag
  criteria are edited and summarized like other criteria» — the shared editor is now the picker;
  "same field names" becomes "same accessible names and test identifiers".
- `openspec/specs/candidate-profile-pages/spec.md`: relation entries can be changed in place, and a
  level is required before an entry is saved.
- `docs/ktl-24/` — short release note with the new interaction and the new dependency.
- `README.md` (Spanish): only if it describes adding languages or skills.

### Out of scope

- Education and experience sections.
- Search level semantics ("B2 o superior") — KTL-25.
- Changing `SearchCriteriaSummary` content, the preset list or the position detail page.
- A single draft saved by one button on the candidate edit page (rejected in KTL-22).

### Decisions to make in the design

1. **Criteria summary inside the editor.** With chips showing every criterion, the summary above the
   expanded editor repeats them. Recommended: show it in the editor only while collapsed; keep it
   unchanged on the preset list and dialog.
2. **Keep or remove the four candidate section files** (thin wrappers vs. direct use of the new
   section).
3. **Test identifier scheme** for the picker (`<idPrefix>-input`, `<idPrefix>-chip`, …) shared by all
   hosts, so the e2e helper is one function.

### Risks

- **E2E churn:** five e2e specs and one security spec change selectors; a missed one fails only in
  Playwright.
- **Portal rendering in jsdom:** popover content is outside the host container; specs written
  against `container.querySelector` will miss it.
- **Required-level flow:** discarding an unfinished item on close must never send a partial write.
