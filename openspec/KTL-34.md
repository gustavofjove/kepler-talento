# KTL-34 — UI tweaks

**Status:** In progress
**Depends on:** KTL-18 (candidate list), KTL-30 (position candidates), KTL-31 (record tables)

## Summary

A set of small, independent UI adjustments collected after the KTL-30 to KTL-33 releases. Each
tweak is listed below with its scope and acceptance criteria. None of them changes the API
contract, permissions or stored data.

## Specs updated

- `openspec/specs/candidate-profile-pages/spec.md`: main data and audit side by side (tweak 3);
  readable audit dates, calendar days and location (tweaks 4–6); form errors in the error style at
  the top of the form (tweak 7); empty competency families on the label line (tweak 8); titled,
  separated add forms (tweak 9).
- `openspec/specs/data-tables/spec.md`: CV presence shown as a tick (tweak 1); phones without the
  Spanish prefix (tweak 10).
- `openspec/specs/catalog-value-picker/spec.md`: read-only empty text on the label line (tweak 8).

Tweak 2 (selection count alignment) changes no specified behaviour. `frontend-localization`,
`position-candidates` and `identity-and-access-control` state nothing these tweaks contradict.

## Tweaks

### 1. CV column shows a tick instead of text

**Today.** Every table with a «CV» column prints «Disponible» when the candidate has a primary CV
and «Pendiente» when they do not. The words take room, read like a candidate status and are easy
to confuse with the «Disponible» candidate status in the neighbouring column.

**Change.** The CV cell shows a tick when the candidate has a primary CV and stays empty when they
do not. One shared component (`shared/components/cv-indicator.tsx`) renders the cell everywhere:

| Table                                               | Component                                                     |
| --------------------------------------------------- | ------------------------------------------------------------- |
| Candidate list                                      | `features/candidates/components/candidate-table.tsx`          |
| Search results (and the matches on a position page) | `features/search/components/search-results.tsx`               |
| Candidates of a position                            | `features/positions/components/position-candidates-panel.tsx` |

**Accessibility.** The tick is an image with the accessible name «Con CV» and the same text as a
tooltip. The empty cell carries visually hidden «Sin CV» text, so a screen reader never meets a
silent cell. Both strings live in `es.json` under `cvIndicator.*`; the per-table keys
(`candidates.list.cv.*`, `search.results.cvAvailable`/`cvPending`,
`positions.candidates.cvAvailable`/`cvPending`) are removed.

**Out of scope.** The document availability badge on the candidate profile
(`candidate.profile.documents.state.*`) describes a single document's scan state, not whether the
candidate has a CV, and keeps its text. The CSV export keeps its `cv_disponible` = `si`/`no` column.

**Acceptance criteria.**

- In the three tables, a candidate with a primary CV shows a tick; one without shows nothing
  visible; neither shows «Disponible» or «Pendiente» in the CV column.
- The tick is exposed as `role="img"` named «Con CV»; the empty cell exposes «Sin CV» to assistive
  technology only.
- Unit specs for the three tables assert on the accessible names, not on the old text.

### 2. Selection count aligned with the bulk actions

**Today.** On the candidate list, the «N seleccionado(s) en esta página» text sits at the top of
the bulk-actions row instead of level with the buttons beside it. The row is a `.form-actions`
flex container, whose default `align-items: stretch` lets the buttons fill the row height while the
plain text stays at the top.

**Change.** The bulk-actions row (`candidate-list-page.tsx`) gets a `bulk-actions` class that
centres its items vertically (`candidate-list-page.css`). The global `.form-actions` rule is not
changed, so other button rows keep their layout.

**Acceptance criteria.**

- With one or more candidates selected, the count text is vertically centred on the same line as
  the «Baja lógica masiva» and «Alta lógica masiva» buttons, at desktop and mobile widths.

### 3. Main data and audit side by side on the candidate page

**Today.** Every section of the candidate page is a full-width panel on its own row (KTL-27). The
«Auditoría» panel holds three short lines and leaves most of its row empty below «Datos
principales».

**Change.** «Datos principales» and «Auditoría» share one row, in two equal columns
(`candidate-detail-page.tsx`, existing `.grid.two` utility). Every other panel stays full width
and in the same order.

- The row falls back to one column when the page's main column is narrower than 620px, for example
  on a phone or while the CV preview sits beside the panels. This is the existing
  `@container page-split-main` rule for `.grid.two`.
- While «Datos principales» is in edit mode, its form takes the whole row and «Auditoría» moves
  below it, so the form is never squeezed into half the width. The row splits again when the
  panel closes.

**Acceptance criteria.**

- On a wide screen, «Datos principales» and «Auditoría» sit next to each other, first and second.
- The other panels keep the KTL-27 order and full width.
- Opening «Datos principales» for editing makes it full width; closing it restores the two columns.
- At phone width, the two panels stack.

### 4. Readable audit dates

**Today.** «Auditoría» prints `createdAt` and `updatedAt` as a cut ISO string
(`2026-09-30T11:49:16`), in UTC.

**Change.** Both values use `formatDate` with `{ dateStyle: 'medium', timeStyle: 'short' }`, which
gives Spanish output in the viewer's time zone, for example «30 sept 2026, 11:49».

**Out of scope.** The export history table on the advanced search page still prints
`exportedAt.slice(0, 19)`. It can move to the same format in a later tweak.

**Acceptance criteria.**

- The audit dates show a Spanish day, abbreviated month, year and hour:minute, with no ISO `T`
  separator and no seconds.

### 5. Location shown as «Location (Province)»

**Today.** «Datos principales» shows the location and province separated by a space
(«Alcobendas Madrid»), which reads as one place name.

**Change.** A `candidateLocation` helper (`features/candidates/candidate-location.ts`) renders
«Alcobendas (Madrid)». With only one part it shows that part alone, and with neither it shows
nothing: never empty parentheses or a stray space.

**Acceptance criteria.**

- Location and province → «Location (Province)».
- Only location → «Location». Only province → «Province». Neither → empty.

### 6. Readable reception and review dates

**Today.** «Datos principales» prints «Fecha de recepción» and «Revisión prevista» exactly as
stored (`2026-09-01`).

**Change.** Both use a new `formatDay` helper (`core/i18n/format.ts`), which gives Spanish output
matching the audit dates (tweak 4) without the time, for example «1 sept 2026». These values are
calendar days with no time or zone, so `formatDay` reads and formats them in UTC. A viewer west of
UTC therefore never sees the previous day. A value that is not a valid `YYYY-MM-DD` day, such as a
legacy import, is shown as stored instead of breaking the panel. Empty values still show
«Pendiente».

**Acceptance criteria.**

- `2026-09-01` shows as «1 sept 2026» in every time zone.
- A malformed stored value is shown unchanged and the page still renders.
- An impossible day in the right shape (`2026-02-30`, `2025-02-29`) is shown as stored, not rolled
  over to the next month by the browser's date parser.

### 7. Validation errors in red at the top of the form

**Today.** Form errors such as «Nombre y apellidos son obligatorios.» appear after the last field,
styled as a grey `.empty-state` placeholder or `.muted` text. They look like an empty-list notice
rather than an error, and in long forms they are below the fold.

**Change.** A shared `FormError` component (`shared/components/form-error.tsx` + `.css`) renders
the message in the danger colour on a light red background with a red left border, as
`role="alert"` so assistive technology announces it. It renders nothing while there is no message.
It replaces the grey messages in:

| Form                                                 | Position                                                         |
| ---------------------------------------------------- | ---------------------------------------------------------------- |
| Candidate core form (create and «Datos principales») | First in the form, above the first field                         |
| Candidate experience and education add forms         | First in the form                                                |
| Candidate notes                                      | Top of the notes section (errors come from add, edit and remove) |
| Candidate document upload                            | First in the upload form (`document-upload-error` test id kept)  |
| Admin users and roles create forms                   | First in the form, full width                                    |
| Login                                                | Unchanged position, already above the button                     |
| Candidate competencies                               | Unchanged: beside the family row it refers to                    |

**Out of scope.** The preset form's name error is already red and sits under the field it refers
to. The import page's refused and failed notices report a batch state, not form input.

**Acceptance criteria.**

- Submitting the candidate form without a name shows «Nombre y apellidos son obligatorios.» in
  red, as the first element of the form.
- Every listed form's error is red, announced as an alert, and never styled as `.empty-state` or
  `.muted`.
- Existing test ids on error messages are kept.

### 8. Empty competency families on one line

**Today.** In read mode, a «Competencias» family without values shows «Sin xxx asociados.» as a
grey dashed `.empty-state` box on its own line below the family label. An empty family is
therefore taller than a filled one, whose chips sit on the label's line.

**Change.** The shared catalog picker (`catalog-value-picker.tsx`) renders the empty text inside
the label row, where the chips would be, as one line of plain muted text (`.catalog-picker-empty`).
It is as tall as a chip, so empty and filled rows have the same height. It keeps the rule that the
text shows only on read-only pickers; editable pickers show just the (+). It gets a
`<prefix>-empty` test id through `pickerTestIds`.

**Acceptance criteria.**

- A candidate with no values in a family shows «Sin xxx asociados.» beside the family label, on
  one line, without a box or dashed border.
- Empty and filled family rows are the same height in read mode.

### 9. Titled, separated add forms in «Formación» and «Experiencia»

**Today.** In edit mode the form for a new entry follows the list of existing entries with nothing
between them, so its fields read like part of the last entry.

**Change.** The add form stays **below** the entries. A new entry is appended to the end of the
list, so the form sits where its result will appear, and people read what is already there before
adding. Each form gets:

- a light rule above it (`border-top: 1px solid var(--border)`, `.candidate-add-form` in
  `candidate-panel.css`);
- an `h3` title, «Nueva formación» / «Nueva experiencia» (`candidate.profile.*.newTitle`), which
  also names the form for assistive technology (`aria-labelledby`).

The submit buttons keep «Añadir formación» / «Añadir experiencia».

**Acceptance criteria.**

- In edit mode, each panel shows its entries, then a light separator, then the titled add form.
- The form is exposed as a form named «Nueva formación» / «Nueva experiencia».

### 10. Phones shown without the Spanish prefix

**Today.** Phones are free text and are shown exactly as stored. Imports, generated data and manual
entry often store `+34 600 000 001`. Almost every candidate is Spanish, so the prefix is noise.

**Change.** A `displayPhone` helper (`features/candidates/contact-links.ts`) drops a leading Spanish
country code (`+34`, `0034`, `(+34)`, `(34)`) when what follows is a 9-digit Spanish number, and
keeps the rest's spacing as typed. Any other number, foreign (`+33 …`) or unusual (too short,
with an extension), is shown unchanged, so no information is hidden. It is applied wherever a
phone is shown:

- candidate list (`candidate-table.tsx`);
- search results and position matches (`search-results.tsx`);
- candidates of a position (`position-candidates-panel.tsx`);
- candidate page header (`candidate-detail-page.tsx`).

This matches CV extraction (KTL-32), which already fills the phone field without the prefix.

**Out of scope.** This is a display rule only. The stored value, the edit form, the free-text
search and the CSV export keep the number as entered. Removing the prefix from stored data would
mean a normalisation rule on write plus a backfill over the encrypted `Phone` column (KTL-33), so
it would be a separate decision.

**Acceptance criteria.**

- A candidate stored as `+34 600 111 222` shows `600 111 222` in the four places above.
- `+33 6 12 34 56 78` and `+34 600` are shown unchanged.
