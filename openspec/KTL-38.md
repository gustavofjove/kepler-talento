# KTL-38 — Status chips with semantic tones

**Status:** Draft
**Depends on:** KTL-36 (availability value and table cells), KTL-37 (Disponibilidad panel)

## Summary

Show status values as chips (`.badge`) with a small set of semantic tones, and stop using the
brand orange for them.

Today every `.badge` in the application is pale orange with dark orange text
(`frontend/src/styles.css`). Orange is also the primary button colour, so a status chip next to
an action button reads as something clickable. The badge already avoids most button cues: pill
radius, no border, 11px text, no hover. Its colour is the one cue it shares with buttons.

Candidate availability («Sin comprobar», «Disponible», «No disponible») is shown as plain text,
both in the tables and in the Disponibilidad panel, so a reader has to read each cell to tell
the values apart. Position status («Abierta», «Cerrada») is a chip, but both values look the
same.

We tried colouring the availability text green and red. Dark tones were hard to tell apart from
black body text, and brighter tones on white read as errors or links. Chips with a pale tinted
background can carry a clearly green or red tone and stay readable.

This is a presentation change only. API, permissions and stored data do not change.

## Specs updated

- `openspec/specs/data-tables/spec.md`:
  - New requirement _Status values are shown as toned chips_ (rules in change 1).
  - The availability cell requirement and its scenario («No disponible · hace 6 meses»): the
    value becomes a chip, followed by the elapsed time as muted text.
- `openspec/specs/candidate-profile-pages/spec.md`:
  - _Availability block on the candidate page_, the **Line** item: the value is a chip, followed by
    «hasta {date}» and the lapsed marker as text.

## Changes

### 1. Tones for `.badge`

Add tone modifiers next to `.badge` in `styles.css`, using the Kepler tokens
(`docs/CORPORATE_IDENTITY_Kepler.md`):

| Modifier          | Background                     | Text                           | Meaning                     |
| ----------------- | ------------------------------ | ------------------------------ | --------------------------- |
| `.badge--success` | `--fj-green-bg`                | `--fj-green-dark`              | Good / active / available   |
| `.badge--danger`  | pale red (new token if needed) | dark red (new token if needed) | Blocking / unavailable      |
| `.badge--neutral` | `--bg-2` or similar            | `--fg-2`                       | Unknown / closed / inactive |

- Text must reach WCAG AA contrast (4.5:1) on its own background. Record the measured ratios in
  the design.
- A chip never has a solid fill, border, hover state or pointer cursor. Those belong to buttons.
- The text label is always present, so colour is never the only signal.
- Plain `.badge` stays orange for now. Other chips (roles, stages, catalog flags, the shell's role
  badge) are out of scope; see Open questions.
- `advanced-search-page.tsx` uses `status status--success` for the export history «Completada»,
  but neither class is defined in any stylesheet. Make it `badge badge--success`.

### 2. Candidate availability as a chip

| Value         | Tone    |
| ------------- | ------- |
| Sin comprobar | neutral |
| Disponible    | success |
| No disponible | danger  |

- **Tables** (`candidate-table.tsx`, `search-results.tsx`): the cell shows the chip followed by the
  elapsed time as muted text («hace 6 meses»). «Sin comprobar» shows the chip alone. The cell
  keeps its test ids (`candidate-availability-cell`, `search-availability-cell`). The chip and
  the elapsed text each get their own test id, so tests can assert them separately.
- **Panel** (`candidate-availability.tsx`): the line shows the chip, then «hasta {date}» when an
  until date is set, then the existing «(vencido)» marker. The `candidate-availability-line` test
  id and the live region behaviour stay. The «Comprobado el … por …» meta line does not change.
- New i18n keys replace `candidate.availability.cell` and `candidate.availability.unavailableUntil`
  as needed (for example `candidate.availability.elapsed`, `candidate.availability.until`). Use
  whole sentences with interpolation, not concatenated fragments.
- The tone comes from the state through one helper in `candidate-availability.logic.ts`
  (`availabilityTone(state)`), not from inline conditionals in each component.
- The «Disponibilidad» filter options, the filter chip and the CSV export keep their text unchanged.

### 3. Position status as a chip with tones

| Value   | Tone    |
| ------- | ------- |
| Abierta | success |
| Cerrada | neutral |

Closing a position is a normal end state, not a problem, so «Cerrada» is neutral, not red.

Applies wherever position status is shown: `position-list-page.tsx`, `position-detail-page.tsx`
and `candidate-positions-panel.tsx`, through one shared helper.

## Out of scope

- Re-toning the other chips: pipeline stages, inactive markers, import states, document states,
  roles and catalog flags.
- Document availability («Disponible» / «No disponible» in `candidate-documents.tsx`). It uses the
  same words but means something else (the file can or cannot be downloaded).
- Any change to filters, export columns or API values.

## Acceptance criteria

- In the candidates table and the search results, «Disponible» is a green chip, «No disponible» a
  red chip and «Sin comprobar» a neutral chip. Known checks show the elapsed time beside the chip
  as muted text.
- The Disponibilidad panel shows the same chip, followed by «hasta {date}» and «(vencido)» when
  they apply.
- «Abierta» is a green chip and «Cerrada» a neutral chip in the positions list, the position page
  and the candidate's positions panel.
- No chip uses a solid fill, a border, a hover change or a pointer cursor. Each tone's text meets
  4.5:1 contrast on its background.
- The export history «Completada» is drawn as a success chip.
- Tests updated: unit specs for the availability cell and panel (chip tone, elapsed text,
  until date) and the position status chips. E2e specs that read the combined availability
  string assert the chip and elapsed test ids instead. New e2e selectors hardcode no Spanish text.
- `npm run lint`, `npm run format:check` and `npm test` pass.

## Open questions

- Should the other chips also move off orange, e.g. to neutral? Proposed: a follow-up ticket, once
  these three tones are in use.
- Danger tone: reuse `--danger` (#c8342b) for the text with a pale tint, or add darker
  `--danger-dark` / `--danger-bg` tokens? Proposed: new tokens, chosen for contrast.
- Should «Sin comprobar» be neutral, or warning (amber) to prompt a check? Proposed: neutral, so
  unchecked candidates (most imported ones) don't fill the list with warnings.
