## Why

KTL-24 gave every page one catalog value picker, but the pages around it still look different.
Search, presets and positions show the four families as compact bordered rows with an inline
label, one row per family in a single column. The candidate edit and detail pages scatter the same
families over separate panels in a two-column grid, headed by `<h3>` titles and interleaved with
Educación and Experiencia. The row layout lives in search-only markup and CSS, so candidate pages
cannot reuse it and the two looks keep drifting apart. Adding a candidate relation also still takes
a second step, because the level editor opens on every add even when the lowest level is right.

## What Changes

- A new shared, presentational **catalog family rows** component owns the family order
  (Habilidades, Idiomas, Programas, Etiquetas), the row look (bordered row, inline label in a fixed
  column, chips and add control on the chips' line) and the spacing between rows. Each host
  supplies its own picker per row. Selection state, persistence and the search ANY/ALL toggle stay
  in the hosts.
- **Search, preset and position** editors render their rows through it. They look the same as
  today, except that the skill row is labelled `Habilidades` instead of `Habilidad`.
- **Candidate edit and detail pages** show one full-width `Competencias` panel with the four family
  rows, tags last. The catalog status notice appears once for the panel.
- **Candidate pages drop the two-column grid.** Every panel is full width and stacked:
  - Edit: Datos principales, Competencias, Educación, Experiencia, Notas, Documentos.
  - Detail: Datos principales, Auditoría, Competencias, Educación, Experiencia, Notas,
    Documentos, then the CV preview.
- **Required level with a default.** Adding a language, skill or program to a candidate saves it
  immediately with the family's lowest active level, by catalog order. The chip editor opens only
  when the user activates the chip. A family whose level catalog has no active value cannot be
  added to, so nothing is ever saved without a level.
- **BREAKING (UI and tests only):** the per-family `<h3>` headings on candidate pages disappear.
  The row labels replace them. Tests that choose a level right after adding a candidate value must
  change. Section `data-testid`s, picker test ids and `name=` attributes are kept.

**Actors.**

- Recruiters (`rrhh_user`, `rrhh_admin`) editing and viewing candidates and searching.
- Users holding `presets.manage` or `positions.manage` editing criteria.
- Any reader of the candidate detail page.

**Key entities.** None new. The change concerns only how candidate relation entries (language,
skill, program, tag) and search criteria are presented, and the level given to a newly added
candidate relation.

**Assumptions.**

- Level catalogs are ordered lowest first by `SortOrder`, which is the rank KTL-25 already uses for
  «al menos X». The frontend already receives active values in that order.
- The existing relation endpoints accept any active level on add, so the default level needs no API
  change.

**Edge cases.**

- A level family with no active values: its add control is disabled and the catalog notice
  explains why. Other families stay usable.
- The lowest level is deactivated between loading the page and adding a value: the API refuses the
  write, and the chip shows the error with a retry, as for any refused write.
- A held value or level that has since been deactivated is still shown, as today.
- At 390 px, rows and chips wrap and no page scrolls horizontally.

**Success criteria.**

1. Search, preset, position and candidate pages render the four families through one component,
   in the same order and with the same row look.
2. Adding a candidate language, skill or program takes one interaction after typing (Enter). It
   takes two today.
3. Candidate edit and detail pages show every section full width, stacked in the stated order.
4. Search, preset and position pages look unchanged apart from the `Habilidades` label.
5. Build, unit, e2e, security, lint and format checks pass.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `catalog-value-picker`:
  - «Optional and required levels»: a required level now starts at the lowest active level, and
    the editor does not open on add.
  - «Read-only and disabled pickers»: an add control is disabled when the required level family
    has no active value.
  - New requirement: every host renders its families through the shared family rows, with the same
    order and look.
- `candidate-profile-pages`:
  - «Edit page owns every candidate change»: default-level adding replaces the save-once-chosen
    and abandon scenarios.
  - «Candidate pages are localized and accessible»: row labels inside the Competencias panel
    replace the per-family headings.
  - New requirement: the Competencias panel and the stacked full-width layout on both pages.

## Impact

- **Frontend only:**
  - New shared component in `features/catalogs/components/`, with `.css` and `.logic.ts` siblings
  - `catalog-value-picker.tsx`: required-level default commit
  - `search/components/criteria-group.tsx`, `search-criteria-form.tsx`, `criteria-group.model.ts`
    and `search-filters.css`: the row CSS moves out
  - A new Competencias panel component; `candidate-relation-section.tsx` becomes a row
  - `candidate-edit-page.tsx` and `candidate-detail-page.tsx`
  - `es.json`: new `candidate.profile.competencies.title`; `search.criteria.skill.label` becomes
    `Habilidades`
- **No** dependency, endpoint, contract, migration, grant, role, RLS or storage change.
- **Personal data, RLS, storage, roles.**
  - The pages render only relation values they already load. No new request, log line, URL or
    storage path carries them (principle 1).
  - The detail page stays read-only. The route guard and the API remain the authorization
    boundary and fail closed as today (principle 3).
- **Tests:**
  - Unit specs for the picker, criteria group, search criteria form, relation section, both
    candidate pages and catalog loading states
  - A new unit spec for the shared rows
  - The e2e specs `candidate-profile`, `candidate-tags-notes`, `candidate-api-cutover`,
    `catalogs-crud`, `navigation-responsive` and `advanced-search`
- **Docs:**
  - The `catalog-value-picker` and `candidate-profile-pages` specs, through deltas
  - `docs/ktl-27/release-notes.md`
  - The KTL-24 section of `README.md`, which says a candidate entry is saved only once its level is
    chosen
