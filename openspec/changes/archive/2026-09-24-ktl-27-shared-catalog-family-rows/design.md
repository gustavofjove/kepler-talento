## Context

See `proposal.md` (Why) and the delta specs for the required behaviour. This section covers only
the current code that shapes the approach.

- **Picker (KTL-24).** `features/catalogs/components/catalog-value-picker.tsx` is presentational.
  It renders `.catalog-picker-row`: label, `headerAction`, chips, then (+) or the input.
  `labelHidden` hides the label visually. With `levelMode="required"`, `choose()` stores a
  `DRAFT_KEY` item and opens its editor. `commit()` turns the draft into `onAdd`, and
  `closeEditor()` discards it.
- **Search rows.** `search-criteria-form.tsx` maps `CRITERIA_GROUPS` (`criteria-group.model.ts`)
  to `CriteriaGroup`, which wraps the picker in `div.criteria-group[data-criteria]`. The row look
  exists only in `search-filters.css`: `.criteria-group` (border, radius, `8px 12px` padding)
  and `.criteria-group .catalog-picker-label { flex: 0 0 6.5rem }`. The form shows one
  `CatalogStatusNotice` above the four rows. `advanced-search.spec.ts` locates
  `.criteria-group`. `search-criteria.logic.ts` also uses `labelKey` for the collapsed summary,
  so the label change reaches the summary too.
- **Candidate rows.** `candidate-relation-section.tsx` renders `section.section-block[data-testid]`
  with an `<h3>` and its own `CatalogStatusNotice`, plus the picker with `labelHidden` and
  `levelMode="required"`. It passes all active levels as `levelOptions`. Both candidate pages put
  the four sections into separate `article.panel`s inside `div.grid.two`, together with
  education, experience, notes and documents. The detail page also has a top `grid two` holding
  Datos principales and Auditoría.
- **Catalog status.** `useCatalogStatus()` gives one message for loading or failure.
  `CatalogStatusNotice` renders it as `p.empty-state[data-testid="catalog-status"]`. Nothing
  reports an empty level family today.
- **Frontend rules** (AGENTS.md):
  - plain co-located CSS, no CSS Modules
  - pure helpers in `*.logic.ts`
  - copy from `es.json`
  - `name=` and `data-testid` are load-bearing
  - one DOM tree for every width
  - the `.span-all` utility is used instead of inline grid styles

## Goals / Non-Goals

**Goals:**

- The row look and family order exist in exactly one place, which search and candidates both use.
- Search DOM, class names and test ids stay as they are, so the search journeys need no selector
  changes beyond the label text.
- The picker's required-level behaviour changes once, for every required-level host.

**Non-Goals:**

- Merging `CriteriaGroup` and `CandidateRelationSection` into one stateful component. Their
  state and persistence differ on purpose (KTL-24 D-goals).
- Changing `SearchCriteriaSummary`, the preset list or the collapsed editor, apart from the label
  text.
- Restyling Educación, Experiencia, Notas or Documentos beyond their width and position.

## Decisions

### D1. `CatalogFamilyRows` is a layout component with a render prop

A new `features/catalogs/components/catalog-family-rows.tsx` renders
`div.catalog-family-rows`, and one `div.catalog-family-row[data-family=<kind>]` per family, in
the order from `CATALOG_FAMILY_ORDER`. That order lives in `catalog-family-rows.logic.ts`:
`['skill', 'language', 'program', 'tag']`. The component takes `renderRow(kind) => ReactNode`
and an optional `notice` rendered above the rows. Hosts keep their pickers, state and handlers.

`CRITERIA_GROUPS` and `RELATION_DEFINITIONS` stay keyed by kind, and the rows component iterates
the shared order. Search therefore no longer depends on the array order of `CRITERIA_GROUPS`, and
both hosts get the same order.

_Alternatives:_

- **A config-array prop** (`families: { kind, label, picker props }[]`). Rejected: it would expose
  the picker's whole prop surface a second time, and it cannot carry the per-host `headerAction`
  and details editors cleanly.
- **Hosting `CriteriaGroup` on candidates.** Rejected: it is typed to `CriteriaFilter` and ANY/ALL.

### D2. Row CSS moves to the shared component; search keeps its class

`catalog-family-rows.css` owns the rows:

- `.catalog-family-rows`: a single-column grid with the gap `search-criteria-form` uses today
  between rows.
- `.catalog-family-row`: the border, radius and `8px 12px` padding moved from
  `.criteria-group`.
- `.catalog-family-row .catalog-picker-label`: `flex: 0 0 6.5rem`.

`CriteriaGroup` keeps its `div.criteria-group[data-criteria]` wrapper, which
`advanced-search.spec.ts` locates. That wrapper sits inside the row, and the moved declarations
are deleted from `search-filters.css` so the border is not drawn twice. The ANY/ALL toggle CSS
stays in `search-filters.css`.

_Alternative:_ keep `.criteria-group` as the row class for every host. Rejected: a search-named
class on candidate pages would repeat the coupling this change removes.

### D3. Candidate rows show their label and drop their heading

`CandidateRelationSection` becomes a row. It keeps its `data-testid` wrapper (`candidate-languages`
and so on) and drops the `<h3>` and `labelHidden`, so the visible label is the picker's accessible
name. It also drops its own `CatalogStatusNotice` in favour of the panel's.

The row label keeps its `candidate.profile.<family>.title` key. With search's skill label now
`Habilidades`, both namespaces read Habilidades, Idiomas, Programas and Etiquetas. Pointing candidate
rows at `search.criteria.<family>.label` (the plan before implementation), or moving both to a
shared `catalogPicker.family.<family>` namespace, was considered. Both were rejected: they would
couple the candidate feature to search keys, or rename keys used by the summary and i18n tests, for
no user-visible gain. The `.empty` keys stay for the read-only empty text.

### D4. `CandidateCompetencies` panel shared by both pages

A new `features/candidates/components/candidate-competencies.tsx` renders
`article.panel[data-testid="candidate-competencies"]`:

- an `<h2>` titled `Competencias` (`candidate.profile.competencies.title`)
- the panel's notice (D6)
- `CatalogFamilyRows` rendering `CandidateRelationSection` per kind

It takes `candidate` and `readOnly`. The heading is `<h2>`, like the other panel headings on the
page.

Both pages replace `div.grid.two` with a plain vertical stack in the order given in the spec. The
detail page's top `grid two` goes too. The stack uses the existing `.grid` utility, which is one
column with the standard gap, so no page-specific CSS is needed. `.span-all` on Notas becomes
redundant and is removed.

### D5. Required level commits with the first level option

In `CatalogValuePicker`, `choose()` for a required level calls
`onAdd({ key: value, value, level: levelOptions[0], details: {} })` directly, then refocuses the
input the way the optional path does. The `DRAFT_KEY` draft, its special cases in `closeEditor`,
`remove`, `collapseWhenIdle` and `shown`, and the `catalogPicker.levelRequired` hint are then
dead, and are deleted.

`levelOptions` is already the level family's active names in catalog order
(`useCatalogs().activeNames`). Its first entry is therefore the lowest level by `SortOrder`, the
rank KTL-25 uses.

_Alternatives:_

- **A `defaultLevel` prop chosen by the host.** Rejected: every required-level host would pass the
  same value, and the spec defines the default in terms of the picker.
- **Keeping the draft flow behind an option.** Rejected: no host needs it after this change, and
  the draft is the most intricate focus code in the picker.

### D6. A required-level picker with no level is disabled, and the panel says why

The picker treats `required && levelOptions.length === 0` like `disabled`: the add control is
disabled, and existing chips stay visible and removable. Chips can still be removed because
removal needs no level. The editor still opens on chips, but it offers no level.

The panel builds one notice from two sources:

- the `useCatalogStatus()` message while loading or failed
- otherwise a line from a new pure helper in `candidate-competencies.logic.ts`, naming each
  required family whose level list is empty, for example `No hay niveles activos de Programas;
actívalos en Catálogos para poder añadir.` (key `candidate.profile.competencies.noLevels`,
  interpolated)

The line reuses `CatalogStatusNotice` (`p.empty-state[data-testid="catalog-status"]`) by passing
it a status with that message, so the notice keeps one look and one test id.

Search is unaffected: its level is optional, so an empty level family only removes the level
choice.

### D7. No API, dependency or permission change

The relation services already send a level on add (`addLanguage` and the others). The detail page
stays read-only by rendering `readOnly` rows. The edit route guard and the API permission checks
are unchanged, so the existing fail-closed tests remain the security evidence.

## Risks / Trade-offs

- **[Risk] The lowest level is wrong for most entries, and editors forget to change it.** →
  Mitigation: the chip shows the level at once, and changing it is one click. The ticket accepted
  this trade for one-step adding. Search's «al menos X» means the lowest level never hides a
  candidate from a search with no level.
- **[Risk] Moving the row CSS changes search pixels.** → Mitigation: the declarations move
  verbatim. An e2e check asserts row order, the label column width and no horizontal overflow at
  desktop and 390 px, without comparing screenshots. The advanced-search journey runs unchanged.
- **[Risk] Tests that choose a level after adding now find no open editor.** → Mitigation: in
  `tests/e2e/support/catalog-picker.ts`, `addValue(page, prefix, name, level)` waits for the saved
  chip and then calls `setLevel` when `level` is given and differs from the level the chip already
  shows. Callers keep their signature. Candidate
  unit specs that expect the draft editor are rewritten to expect an immediate add at the first
  level.
- **[Trade-off] Removing `<h3>` per family** changes heading navigation on the candidate pages. The
  panel heading plus labelled pickers still meet the spec. Screen-reader users land on the panel,
  not on each family.
- **[Risk] A level is deactivated after the page loaded, and the add sends it.** → Mitigation: the
  API refuses the write, and the chip shows the error with a retry, as today.

## Migration Plan

This is a frontend-only deploy, and rollback is reverting the commit. There is no data to migrate:
existing relation entries already carry a level, and stored presets and positions are unchanged.
