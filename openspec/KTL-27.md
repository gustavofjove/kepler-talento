# KTL-27 — Candidate "Competencias" panel sharing the search criteria layout

## [original]

I'd like to reuse the same controls in the Candidate edit page as in the Search and Preset for
Languages, Skills, etc. They use the same individual component for adding the items, but the
component doesn't look the same, e.g. one row per filter vs multiple rows, one column vs two
columns, etc. One panel with "Competencias" sounds good; it should be a component shared with
Preset and Search. Sections should be vertically separated rather than in two columns. Every host
should render the families with the same layout. The level stays required on the candidate, but
starts at the lowest level by default and can be changed by clicking the chip.

## [enhanced]

**Status:** Proposed

### Summary

Since KTL-24 every page edits skills, languages, programs and tags with the same
`CatalogValuePicker`. The pages around it still differ:

|                     | Search / preset / position                                             | Candidate edit and detail                                                          |
| ------------------- | ---------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| Arrangement         | One bordered row per family, stacked in one column (`.criteria-group`) | One `panel` per family in a `grid two`, interleaved with Educación and Experiencia |
| Label               | Inline, in a fixed 6.5rem column, on the chips' line                   | Hidden (`labelHidden`); an `<h3>` section title above the picker                   |
| Family order        | Habilidades, Idiomas, Programas, Etiquetas                             | Idiomas, Programas, …, Habilidades, Etiquetas                                      |
| Adding with a level | Optional level, committed at once                                      | Required level: the chip editor opens and nothing saves until a level is chosen    |

The row layout lives in search-only CSS and markup, so the candidate pages cannot use it and the
two looks drift apart. This ticket extracts the layout into one shared component, puts the four
candidate families in a single **Competencias** panel that uses it, stacks every candidate page
section vertically, and makes adding a candidate relation a single step.

### User story

As a recruiter, I see and edit a candidate's skills, languages, programs and tags in the same
compact rows I use to search for them, so I recognise them at once. I add a value in one step
and refine its level only if the default is wrong.

### Decisions from discovery (2026-09-24)

1. **One shared family-rows component.** A new presentational component under
   `frontend/src/app/features/catalogs/components/` (working name `CatalogFamilyRows`) owns the
   family order (Habilidades, Idiomas, Programas, Etiquetas), the row look (bordered row, inline
   label in a fixed column, chips and add control on the same line, wrapping when they do not
   fit) and the spacing between rows. Each host supplies its own picker configuration per row:
   level mode, header action (the ANY/ALL toggle on search), commit handlers, detail fields.
   Selection state and persistence stay in the hosts.
2. **Search, preset and position look unchanged** apart from decision 7. `SearchCriteriaForm`
   renders its four `CriteriaGroup`s through the new component, and the rows must look the same
   as they do today.
3. **Competencias panel on both candidate pages.** The edit page and the read-only detail page
   each show one full-width panel titled `Competencias` holding the four family rows, tags last.
   Within the panel each row's visible label replaces the per-family `<h3>`. The catalog status
   notice appears once for the panel, not once per family.
4. **Candidate pages are stacked, not gridded.** Both pages drop `grid two`. Every panel is full
   width and stacked vertically, in this order:
   - Edit: Datos principales, Competencias, Educación, Experiencia, Notas, Documentos.
   - Detail: Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas,
     Documentos, followed by the existing CV preview.
5. **Required level with a default.** Adding a language, skill or program to a candidate commits
   it immediately with the family's **lowest active level**: the first active value of the level
   family by catalog `SortOrder`, the same rank KTL-25 uses for `al menos X`. The chip editor does
   not open on add. Activating the chip opens it to change the level and the details
   (certification for languages, years of experience for programs), as today. If the level family
   has no active values, the family's add control is disabled and the catalog status notice is
   shown. An entry is never persisted without a level. Tags have no level and are unchanged.
6. **Same layout everywhere.** The `catalog-value-picker` capability gains a requirement that
   every host renders its families through the shared rows, with the same order and look.
7. **`Habilidades` everywhere.** The search row label changes from `Habilidad` to `Habilidades`
   (`search.criteria.skill.label`), matching the plural of the other three. This is the only
   visible change on search, preset and position pages.

### Existing behavior and contract

- `CatalogValuePicker` ([catalog-value-picker.tsx](../frontend/src/app/features/catalogs/components/catalog-value-picker.tsx))
  already supports `levelMode`, `labelHidden`, `headerAction`, `detailText` and `renderDetails`.
  Today `levelMode="required"` opens the editor on a `DRAFT_KEY` item on add. Decision 5 needs a
  way to commit with a default level instead, e.g. a `defaultLevel` prop, or a `required` mode
  that commits with the first `levelOptions` entry.
- `CriteriaGroup` ([criteria-group.tsx](../frontend/src/app/features/search/components/criteria-group.tsx))
  and `CRITERIA_GROUPS` ([criteria-group.model.ts](../frontend/src/app/features/search/components/criteria-group.model.ts))
  define the search rows. The row look is `.criteria-group` and
  `.criteria-group .catalog-picker-label` in
  [search-filters.css](../frontend/src/app/features/search/components/search-filters.css). It
  moves to the shared component's own CSS file (plain CSS, no CSS Modules).
- `CandidateRelationSection` ([candidate-relation-section.tsx](../frontend/src/app/features/candidates/components/candidate-relation-section.tsx))
  and `RELATION_DEFINITIONS` drive the candidate pickers and their per-chip pending, error and
  retry states. Those write semantics stay as they are.
- `useCatalogs().activeNames(levelFamily)` already returns active values in catalog order, so its
  first entry is the lowest level.
- **API:** unchanged. The existing `CandidateRelationsService` calls (`addLanguage`, `addSkill`,
  `addProgram`, `update*`, `remove*`) already send a level. No endpoint, MediatR handler, table,
  migration, permission or `ktl_runtime` grant changes.

### Acceptance criteria

```gherkin
Scenario: Candidate edit page layout
  Given an editor opens an existing candidate's edit page
  Then the sections Datos principales, Competencias, Educación, Experiencia, Notas and Documentos
    are shown full width, one below another
  And Competencias shows the rows Habilidades, Idiomas, Programas and Etiquetas in that order

Scenario: Candidate detail page layout
  Given any reader opens a candidate's detail page
  Then Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas and Documentos
    are shown full width, one below another
  And Competencias shows the four rows read-only, each with its empty text when it has no items

Scenario: Same rows on every host
  Given the advanced search, a preset editor, a position editor and a candidate edit page
  Then each renders the four families in the same order, with inline labels and the same row look

Scenario: Search look is preserved
  Given the advanced search page before and after this change
  Then the family rows look the same, except that the skill row is labelled Habilidades

Scenario: Adding a language defaults to the lowest level
  Given the language_level catalog's first active value is A1
  When an editor adds Inglés on the candidate edit page
  Then Inglés persists immediately with level A1 and no level editor opens

Scenario: Changing the default level
  Given a candidate has Inglés at A1
  When the editor activates the Inglés chip and chooses C1
  Then the entry persists with C1 and keeps its certification

Scenario: No active levels
  Given the program_level catalog has no active values
  Then the Programas add control is disabled and the catalog notice is shown
  And no program entry can be written without a level

Scenario: Refused write
  When the API refuses a relation write
  Then that chip shows the Spanish error with a retry and the other chips are unchanged

Scenario: Narrow viewport
  Given the candidate edit or detail page at 390 pixels wide
  Then every row, chip and editor is reachable and the page does not scroll horizontally
```

### Implementation scope

- **New shared component** in `frontend/src/app/features/catalogs/components/` with a sibling
  `.css` file. Any pure helpers or constants (family order) go in a `.logic.ts` sibling. Reuse one
  family order for search and candidates rather than two tables.
- **Search:** `SearchCriteriaForm` and `CriteriaGroup` render through the shared component, and
  the row CSS moves out of `search-filters.css`. The collapsed-summary, ANY/ALL toggle and
  catalog-notice behaviour stay as they are.
- **Candidates:** a `Competencias` panel component, used by
  [candidate-edit-page.tsx](../frontend/src/app/features/candidates/pages/candidate-edit-page.tsx)
  and [candidate-detail-page.tsx](../frontend/src/app/features/candidates/pages/candidate-detail-page.tsx),
  renders the four `CandidateRelationSection` rows through the shared component. Both pages switch
  from `grid two` to stacked full-width panels.
- **Picker:** support committing a required-level item with a default level (decision 5), and
  keep the current draft flow for any host that still needs it.
- **i18n:** a new `candidate.profile.competencies.title` = `Competencias` key, and
  `search.criteria.skill.label` = `Habilidades`. No hardcoded JSX or attribute copy.
- **Test ids:** keep `candidate-languages`, `candidate-skills`, `candidate-programs`,
  `candidate-tags`, the `candidate-<family>` and `search-<family>` picker test ids, and every
  `name=` attribute.

### Verification

- **Unit (Vitest + Testing Library):** update `candidate-relation-section.spec.tsx`,
  `candidate-edit-page.spec.tsx`, the candidate detail page spec, `catalog-loading-states.spec.tsx`
  and the catalog value picker and criteria group specs. Add specs for the shared rows component
  (order, labels, header action) and for the default-level commit, including the no-active-levels
  case.
- **E2E (Playwright), run for real:** `candidate-profile.spec.ts`, `candidate-tags-notes.spec.ts`,
  `candidate-api-cutover.spec.ts`, `catalogs-crud.spec.ts`, `navigation-responsive.spec.ts`, plus
  the advanced search and preset journeys. Update any step that chooses a level right after adding
  a value. Cover 390px with no horizontal overflow.
- **Security:** no permission, grant or storage change. The existing tests showing that relation
  writes fail closed for unauthenticated and unauthorized callers must still pass unchanged.
- Run `npm run lint` and `npm run format:check` from `frontend/`.

### Documentation

- Specs:
  - `catalog-value-picker`: the "Optional and required levels" requirement (required now means a
    default lowest level instead of opening the editor on add), plus a new shared-rows layout
    requirement.
  - `candidate-profile-pages`: the Competencias panel, stacked layout on both pages, the
    replacement for "a heading per section" (row labels inside Competencias), and the level
    scenarios ("Entry is saved once its level is chosen", "Level choice is abandoned").
  - `saved-search-presets`: only if its shared-editor requirement names the skill label or the
    row look.
- Add `docs/ktl-27/release-notes.md`, and update `README.md` if it describes the candidate pages.

### Non-functional requirements

- **Personal data:** no new data is exposed. The detail page shows exactly the fields it shows
  today.
- **Accessibility:** one top-level heading per page, one heading per panel, the row labels as
  each picker's accessible name, full keyboard operation, and focus returns to the chip when the
  editor closes.
- **Responsive:** the single 768px shell breakpoint stays unchanged. Rows wrap within their own
  width, with no `matchMedia` branching.

### Out of scope

- Making the candidate level optional, or any change to relation endpoints or storage.
- Changing Educación, Experiencia, Notas or Documentos beyond their position and width.
- Changing search semantics, ANY/ALL, or the `al menos X` level meaning.
