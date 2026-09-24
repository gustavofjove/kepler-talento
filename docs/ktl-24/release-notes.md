# KTL-24 release notes

Frontend-only. No endpoint, contract, migration, grant or role change. Stored presets and position
requirements keep their `SearchFilters` shape.

## One picker for every catalog value

Skills, languages, programs and tags are chosen with a single shared control everywhere they are
edited: the advanced search, the preset create and edit pages, the position form and the
candidate edit page.

- **One line at rest.** Each picker is a single line: its label, its chips and a (+) button
  («Añadir idioma», «Añadir habilidad», …). It wraps only when the chips do not fit. Where values
  can be added, an empty picker shows just (+), with no «Sin …» text.
- **Type to add.** (+) turns into an input, focused, with every value not yet held already listed.
  Typing narrows the list to the values containing the text, ignoring case and accents, in catalog
  order. Enter or a click adds the highlighted value; values already present are not offered. When
  nothing matches it says «No hay coincidencias.». The input stays open for the next value (a click
  on it lists the rest again). Escape with the list closed, or clicking elsewhere, turns it back
  into (+).
- **Chips.** Each value is a chip showing its level (or «Cualquier nivel» for a search criterion
  without one) and, on candidates, the certification or years of experience. A chip is removed
  with its «×» button, or with Delete/Backspace when focused. Values since deactivated in the
  catalog stay visible and removable but are never offered.
- **Level on the chip.** Activating a chip (click or Enter) opens an editor anchored to it with
  the family's levels as a toggle group (a select for families of more than six levels), plus
  the certification field for languages and the years field for programs. Choosing a level saves
  and closes; a detail field saves on Enter or when focus leaves it. A level is **changed in
  place**, without removing and re-adding the entry. Tag chips open no editor.
- **Keyboard.** Every step works without a mouse. Enter on (+) opens the input; from the input,
  Shift+Tab reaches the last chip's remove button and then the chip. Arrow keys move between chips and between levels. Escape closes
  the editor and returns focus to the chip.

## Search, presets and positions

- A new criterion starts at «Cualquier nivel»; its level is set from its chip. Re-adding a value
  to change its level is no longer possible (nor needed).
- The ANY/ALL control («Cualquiera» / «Todos») is now a toggle next to the family label, shown
  only once the family holds two criteria, when it starts to matter. The four family labels share
  one width, so the rows line up.
- The criteria summary appears inside the editor only while it is collapsed; the chips already
  show every criterion when it is open. Preset and position pages, which do not collapse, no
  longer repeat the summary above the editor. The preset list and criteria dialog are unchanged.
- A catalog outage now disables every family's (+), with the existing notice, instead of leaving
  empty dropdowns.

## Candidate edit and detail pages

- **A language, skill or program is saved only once its level is chosen.** Adding one opens its
  level editor at once; closing it without a level (Escape or a click elsewhere) discards the
  value and sends nothing. Tags have no level and save immediately.
- Each add, change and removal still saves at once. While a write is in flight its chip shows
  «Guardando…». A refused write (for example a concurrent change by someone else) is reported
  under the chips as «Idioma: mensaje» with a «Reintentar» button, and the other entries and the
  core form are untouched. Removing the chip of a failed add just drops it.
- The four relation sections are one table-driven component (`candidate-relation-section.tsx`);
  `candidate-languages.tsx`, `candidate-skills.tsx`, `candidate-programs.tsx`,
  `candidate-tags.tsx` and `candidate-tags.css` are gone. `candidateRelationsService` gains
  `updateLanguage`, `updateSkill` and `updateProgram`, which replace an entry by id through the
  existing collection `PUT` and refuse an entry that no longer exists.
- Every relation section, not only languages, now disables adding during a catalog outage.
- The detail page shows the same chips read-only: no (+), no remove button, no editor, and the
  «Sin …» text when a section is empty.

## New runtime dependency: `react-aria-components`

Added for the picker's behaviour only (combobox filtering and keyboard handling, chip
navigation and removal, the anchored and viewport-clamped editor popover, focus management and
touch). All styling is ours, in `catalog-value-picker.css`, with the Kepler tokens. Its few
internal strings are localized by an `I18nProvider locale="es-ES"` in `main.tsx`; every visible
string comes from `es.json`. The reasoning and the alternatives considered are in the change's
design (decision 1).

Production bundle (`npm run build`), before and after:

| Asset | Before                       | After                        | Delta                    |
| ----- | ---------------------------- | ---------------------------- | ------------------------ |
| JS    | 1,044.61 kB (gzip 303.23 kB) | 1,296.93 kB (gzip 382.78 kB) | +252.32 kB (gzip +79.55) |
| CSS   | 30.31 kB (gzip 6.06 kB)      | 34.35 kB (gzip 6.64 kB)      | +4.04 kB (gzip +0.58)    |

Much of the JS growth is react-aria's bundled translations for every locale it supports. Stripping
the unused ones at build time (react-aria's locale optimization plugin) is a possible follow-up.

## Test identifiers

The native selects and Add buttons are gone, and with them their `name`s and `data-testid`s. One
scheme, derived from a prefix, replaces them on every host:

| Element     | Identifier                                                                 |
| ----------- | -------------------------------------------------------------------------- |
| Picker      | `data-testid="<prefix>-picker"`                                            |
| Add (+)     | `data-testid="<prefix>-add"`                                               |
| Input       | `data-testid="<prefix>-input"`, `name="<prefix>"` (only while adding)      |
| Chip        | `data-testid="<prefix>-chip"`, `data-value="<value>"`                      |
| Chip remove | `data-testid="<prefix>-remove"`                                            |
| Chip editor | `data-testid="<prefix>-editor"`; levels are `radio`s                       |
| Retry       | `data-testid="<prefix>-retry"`                                             |
| Search mode | `data-testid="search-<family>-mode"`, options `data-value="ANY"` / `"ALL"` |

Prefixes: `search-skill`, `search-language`, `search-program`, `search-tag` (identical on the
search, preset and position pages) and `candidate-language`, `candidate-skill`,
`candidate-program`, `candidate-tag`. The section wrappers keep `candidate-languages`,
`candidate-skills`, `candidate-programs` and `candidate-tags`.

Removed: `select[name="skillDraft|languageDraft|programDraft|tagDraft"]`, `*LevelDraft`,
`select[name="*Mode"]`, `add-skill`, `add-language`, `add-program`, `add-tag`,
`candidate-tag-select`, and the candidate `select[name="language"|"skill"|"program"|"level"]`.
Playwright specs drive every picker through `tests/e2e/support/catalog-picker.ts`
(`addValue`, `setLevel`, `removeValue`, `chips`, `openInput`, `offered`, `addFirstOffered`).

## Copy

New keys: `catalogPicker.*` and `search.criteria.mode.label`, plus
`candidate.profile.validation.entryNotFound`. Keys left unused by the removed markup were deleted
(`search.criteria.*.add`, `search.criteria.*.level`, `search.criteria.option.select`,
`search.criteria.match`, `search.criteria.remove`, `search.criteria.group.empty`,
`candidate.profile.*.add`,
`candidate.profile.*.label`, `candidate.profile.*.level`, `candidate.profile.tags.remove`).
`criteria-group.tsx` left `LEGACY_HARDCODED_COPY`.
