## Context

See `proposal.md` (Why) and the specs for the required behaviour. This section covers only the
current code that shapes the approach.

- **Search side (one implementation).** `features/search/components/criteria-group.tsx` renders a
  value `select`, an optional level `select`, an Add button, a mode `select` (only once a criterion
  exists) and a list. `search-criteria-form.tsx` renders it four times from `CRITERIA_GROUPS`
  (`criteria-group.model.ts`). It owns a per-kind `drafts` state and `addCriterion`, which replaces
  the level when the same value is added again. The form is controlled: every change goes out through
  `onFiltersChange`. The search page, preset edit page and position form page all host it.
- **Candidate side (four copies).** `candidate-languages.tsx`, `-skills.tsx`, `-programs.tsx` and
  `-tags.tsx` each hold their own draft and error state. They call `candidateRelationsService`
  add/remove methods, which validate (duplicate value, negative years) and then replace the whole
  collection through `candidateService.set<Relation>()` →
  `PUT /api/candidates/{id}/<relation>`, with the aggregate's version for optimistic concurrency.
  Each takes `readOnly` (KTL-22). Only languages disables its inputs on a catalog outage.
- **Relation shapes differ.** `CandidateLanguage { id, language, level, certification? }`,
  `CandidateSkill { id, skill, level }`, `CandidateProgram { id, program, level, yearsExperience? }`,
  `CandidateTag { id, tag }`. Search criteria are `{ value, level }`.
- **Catalogs.** `useCatalogs().activeNames(family)` returns active names in catalog order;
  `useCatalogStatus()` / `CatalogStatusNotice` report loading failures.
- **Frontend rules** (AGENTS.md): plain co-located CSS, Kepler tokens, no inline styles, `t()` for all
  copy, `name=`/`data-testid` are load-bearing for Playwright, one DOM tree for every width, pure
  helpers in `*.logic.ts`. No new runtime dependency without a documented reason.

## Goals / Non-Goals

**Goals:**

- One picker component, purely presentational, reused unchanged by every host.
- Hosts keep their own state and persistence semantics: search stays a controlled filter value,
  candidates keep save-on-each-action.
- A single test-identifier scheme, so e2e specs drive every picker through one helper.

**Non-Goals:**

- Changing `SearchFilters`, relation DTOs, endpoints or concurrency behaviour.
- Restyling `SearchCriteriaSummary` or the preset list.
- Using react-aria anywhere outside the picker in this change.

## Decisions

### 1. `react-aria-components` for behaviour, our CSS for looks

The picker composes `ComboBox` (input + `ListBox` in a `Popover`), `TagGroup`/`TagList`/`Tag` (the
chips), `DialogTrigger` + `Popover` + `Dialog` (the chip editor), `ToggleButtonGroup` (levels; mode
toggle) and `Select` (levels when a family has more than six). All are unstyled. We style them in
`catalog-value-picker.css` through `className` and react-aria's `data-*` state attributes
(`[data-focused]`, `[data-selected]`, `[data-disabled]`, `[data-pending]`), using Kepler tokens.

**Dependency justification (principle 2).** The costly and error-prone part of this control is its
behaviour, not its markup:

- combobox filtering, highlighting and Enter/Escape handling
- arrow-key movement and Delete/Backspace removal across chips
- popover positioning that flips and stays inside a 390 px viewport
- focus moving into the editor and back to the chip
- touch behaviour

react-aria provides all of it, tested across browsers, with no styling opinion and no global state.
The package is 1.x (1.21 at time of writing) with a React `^19` peer. Only the imported components
reach the bundle.

**Alternatives considered.**

- _Downshift_ (`useCombobox` + `useMultipleSelection`): smaller, but it covers only the input and
  chip list. The anchored, focus-managed popover, the fiddliest piece, would still be hand-built.
- _Hand-built_: rejected for the reasons above. It would be the largest and riskiest part of the
  change, with nothing to show for it in the UI.
- _Native `datalist`_: no control over filtering, rendering or keyboard, and it cannot express
  "already added".

react-aria's few internal strings (for example screen-reader announcements) are localized by an
`I18nProvider locale="es-ES"` wrapped once around the app in `main.tsx`. Every visible string is
passed in from `es.json` through `t()`.

### 2. Picker API: items in, intents out

```ts
interface PickerItem {
  key: string; // stable: relation id, or the normalized value for criteria
  value: string;
  level: string; // '' = any level (optional-level pickers only)
  details?: Record<string, string | number | undefined>;
  status?: 'pending' | 'error';
  error?: string;
}

interface CatalogValuePickerProps {
  idPrefix: string; // e.g. 'search-skill', 'candidate-language'
  label: string; // names the chip list; heads the picker unless labelHidden
  labelHidden?: boolean; // candidate sections already show a heading
  addLabel: string; // input's accessible name and placeholder, «Añadir idioma…»
  emptyText?: string; // shown instead of the chips while there are none
  valueOptions: string[]; // active names, catalog order (host passes activeNames)
  levelOptions?: string[]; // absent ⇒ no levels (tags)
  levelMode?: 'optional' | 'required';
  anyLevelLabel?: string;
  items: PickerItem[];
  readOnly?: boolean;
  disabled?: boolean;
  headerAction?: ReactNode; // search: mode toggle
  detailText?: (item: PickerItem) => string | undefined; // chip text: certification, years
  renderDetails?: (draft: PickerItem, set: (next: PickerItem) => void) => ReactNode;
  onAdd(item: PickerItem): void; // hosts may start async work and reflect it via status
  onChange(item: PickerItem): void;
  onRemove(item: PickerItem): void;
  onRetry?(item: PickerItem): void;
}
```

- The picker holds only **transient UI state**: the input text, which chip's editor is open, and the
  one uncommitted item of a `required` picker while its level is being chosen. It never holds the
  item list, so the host's state remains the single source.
- Callbacks are **synchronous intents**. Async hosts show progress by passing `status`/`error` on
  items. This avoids promise plumbing inside the picker and lets the candidate host own retries and
  concurrency.
- **Required-level flow:** `onAdd` is not called until a level is picked. Closing the editor clears
  the uncommitted item. This is enforced inside the picker, so no host can send a partial write.
- Filtering is a pure helper in `catalog-value-picker.logic.ts`: it compares NFD-normalized,
  diacritic-stripped, lower-cased names, excludes values present in `items`, and keeps catalog
  order. It is unit-tested without React.
- **Commit points in the editor:** choosing a level commits the whole draft and closes; a detail
  field commits on Enter or when focus leaves the details. The editor is deliberately not a
  `<form>`: it is portalled, and React would bubble its submit into the host's own form (the
  search form runs the search on submit).
- **Closing the option list:** with a controlled selection, react-aria leaves the list open after
  a choice and offers no controlled `isOpen` on `ComboBox`. After an optional-level add the picker
  remounts the combobox (a `key` bump) and refocuses its input. A required-level add needs nothing
  extra: focus moves into the editor, and the blur closes the list. Once a draft's editor closes,
  its chip is gone, so the picker returns focus to the input itself unless the user has already
  put it elsewhere.
- **Opening on click:** a click on the input opens the full list through react-aria's combobox
  state. `menuTrigger="focus"` is not used, because it would also fire on the programmatic
  refocus after each add and reopen the list that was just closed.

**Alternative considered:** a generic `T` item type with accessor props. Rejected: every host already
has to map to `{ value, level }`, and a fixed `PickerItem` keeps the component and its tests simple.

### 3. Test identifiers and names

Derived from `idPrefix`, identical on every host:

| Element              | Identifier                                              |
| -------------------- | ------------------------------------------------------- |
| Picker root          | `data-testid="<idPrefix>-picker"`                       |
| Add button (+)       | `data-testid="<idPrefix>-add"`                          |
| Input (while adding) | `data-testid="<idPrefix>-input"`, `name="<idPrefix>"`   |
| Chip                 | `data-testid="<idPrefix>-chip"`, `data-value="<value>"` |
| Chip remove          | `data-testid="<idPrefix>-remove"`                       |
| Editor               | `data-testid="<idPrefix>-editor"`                       |
| Level option         | role `radio` in a group named «Nivel», or the `Select`  |
| Mode toggle (search) | `data-testid="<idPrefix>-mode"`                         |

Search prefixes: `search-skill`, `search-language`, `search-program`, `search-tag` (same on the
search, preset and position pages). Candidate prefixes: `candidate-language`, `candidate-skill`,
`candidate-program`, `candidate-tag`. Section wrappers keep `candidate-languages`, `candidate-skills`,
`candidate-programs` and `candidate-tags`.

A shared Playwright helper, `frontend/tests/e2e/support/catalog-picker.ts`, provides
`addValue(page, prefix, name, level?)`, `setLevel`, `removeValue` and `chips` (plus `openInput`,
`offered` and `addFirstOffered`). Every rewritten spec uses it.

**Compact layout (revised after review).** At rest a picker is one wrapping flex row: label, the
host's `headerAction`, the chips, then a (+) button. (+) is replaced in place by the input, focused
and with the full list open; after an add the input stays for the next value. Escape with the list
closed, or focus settling outside the input and its list, returns to (+). An editable picker shows
no empty-state text; a read-only one keeps it. Without a controlled `isOpen` on `ComboBox`, the
list is opened through `ComboBoxStateContext`: from the input's focus event when revealed, and on a
click once shown.

### 4. Search host

- `CriteriaGroup` becomes a thin adapter. It maps `CriteriaFilter[]` to `PickerItem`s (key = the
  normalized value) and back. It passes `levelMode="optional"` and
  `anyLevelLabel={t('search.criteria.level.any')}`, plus a `ToggleButtonGroup` mode control as
  `headerAction`, rendered only from two criteria (the mode has no effect below that). A fixed
  label width lines the four family rows up.
- `search-criteria-form.tsx` loses `drafts`, `EMPTY_DRAFTS` and `addCriterion`. Add, change and
  remove become immutable updates through `onFiltersChange`, as before. The old "re-adding replaces
  the level" rule is gone because present values are not offered; changing the level on the chip is
  now the only path.
- **Summary inside the editor:** `SearchCriteriaSummary` renders in the form only while collapsed
  (resolves the brief's open decision; see the `saved-search-presets` delta). Hosts without
  collapsing (preset and position pages) no longer show it above the editor. The preset list and
  dialog still use it unchanged.

### 5. Candidate host: one table-driven section

`candidate-relation-section.tsx` is driven by a definition table in
`candidate-relation-section.logic.ts`:

```ts
{ kind: 'language', testId: 'candidate-languages', idPrefix: 'candidate-language',
  valueFamily: 'language', levelFamily: 'language_level',
  toItem, fromItem, details: 'certification',
  add: 'addLanguage', update: 'updateLanguage', remove: 'removeLanguage' }
```

- The page renders four `<CandidateRelationSection kind=… />`. The four old files are **deleted**
  (resolves the brief's decision 2): wrappers would keep four files alive for nothing, and every spec
  that imports them changes anyway.
- The section keeps a per-item `status`/`error` map keyed by item key. It sets `pending`, awaits the
  service, then clears the entry or stores `errorText(err, t)`. `onRetry` replays the last intent for
  that key. Pending adds render as chips from the intent until the aggregate absorbs the write; the
  service's absorbed aggregate then becomes the item source again.
- `candidateRelationsService` gains `updateLanguage`, `updateSkill` and `updateProgram`. Each
  replaces the entry with the same id, applies the existing validation (duplicate value excluding
  itself, non-negative years) and calls the same `set<Relation>()`. No backend change: the `PUT`
  replaces the collection.
- Details renderers: a text input for certification and a number input for years, each with a
  label and `name` (`certification`, `yearsExperience`) inside the editor.
- `readOnly` passes straight to the picker. `disabled` is derived from `useCatalogStatus()` for
  every kind, fixing today's languages-only behaviour.
- Tags: `levelOptions` is absent. The existing rule that deactivated tags stay assigned but are not
  offered follows automatically from `activeNames`.

### 6. Copy

New `catalogPicker.*` keys (add placeholder with `{{label}}`, no results, remove/edit with
`{{value}}`, level, pending, retry, level required hint) and `search.criteria.mode.label`. Keys left
unused by the removed markup are deleted: `search.criteria.*.add`, `search.criteria.option.select`,
`candidate.profile.*.add`, `candidate.profile.action.remove`, `candidate.profile.option.select`,
`candidate.profile.tags.label` and similar. `i18n.spec.ts` guards for orphans. No file joins
`LEGACY_HARDCODED_COPY`.

### 7. Stack, data, authorization and storage impact

- **Stack:** one runtime npm dependency (decision 1). No backend change.
- **Data model:** none. `SearchFilters`, stored presets (`jsonb`, `FilterSchemaVersion` 1) and
  position requirements are unchanged. Relation writes use the existing collection `PUT`.
- **Authorization:** unchanged. The edit route guard (`candidates.update`) and the API's per-endpoint
  checks remain the control. The picker's read-only mode is presentation only.
- **Storage:** none.

### 8. Test strategy

- **Unit:**
  - `catalog-value-picker.logic.spec.ts` (filtering, accent and case folding, exclusion, order)
  - `catalog-value-picker.spec.tsx` (every spec scenario, with `user-event` keyboard)
  - `candidate-relation-section.spec.tsx` (the four kinds, required-level discard, in-place change,
    pending and error with retry, read-only, outage)
  - `candidate-relations.service.spec.ts` (update methods)
  - updates to `search-criteria-form.spec.tsx`, `candidate-profile-sections.spec.tsx`,
    `candidate-tags.spec.tsx` (folded into the section spec) and `catalog-loading-states.spec.tsx`
  - react-aria popovers portal to `document.body`, so specs query `screen`, not `container`
- **E2E:** the helper from decision 3, plus rewrites of `advanced-search`, `candidate-profile`,
  `candidate-api-cutover`, `candidate-tags-notes` and `catalogs-crud`. A narrow-width check for an
  open editor goes in `navigation-responsive.spec.ts`.
- **Security:** `tests/security/ktl-21-tags-notes.spec.ts` changes selectors only. Existing API
  authorization suites run unchanged as evidence that nothing moved.

## Risks / Trade-offs

- [react-aria upgrade churn or bundle growth] → Pin via `package-lock.json`; import components
  individually; compare `vite build` output size before and after and record it in the release note.
- [Portal content invisible to container-scoped unit queries] → Convention in the picker spec; review
  the updated specs for `container.querySelector` on editor content.
- [E2E selector churn across six specs] → One helper; every native-select selector for these
  families is removed in the same change (a grep for `Draft"`, `add-skill` and
  `candidate-tag-select` must return nothing).
- [Pending chip and absorbed aggregate disagree briefly] → Items are keyed by relation id; pending
  state is cleared only after the service resolves, and the aggregate is the source afterwards.
- [Users used to the Add button] → The placeholder «Añadir idioma…» names the action; the release
  note shows the new flow.
- [Trade-off: fixed `PickerItem` shape] → Each host maps its model, a few lines per host, in
  exchange for a simpler picker and tests.

## Migration Plan

Frontend-only and deployable as one release. No data migration. Rollback means redeploying the
previous frontend bundle; stored data is untouched in both directions.
