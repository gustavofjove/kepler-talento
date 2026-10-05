## Context

See proposal.md — Why. Current state relevant to the approach:

- `.badge` in `frontend/src/styles.css` is the only chip style: pill radius, no border, 11px
  Montserrat 600, `--fj-orange-pale` background and `--fj-orange-dark` text. Every chip in the app
  uses it bare, plus `.inactive-badge` and `.suggestion-badge` local variants.
- Availability text comes from `availabilityCell()` in `candidate-availability.logic.ts`, which
  joins label and elapsed time through the `candidate.availability.cell` key
  («{{value}} · {{elapsed}}»). `candidate-table.tsx` and `search-results.tsx` render it inside
  `candidate-availability-cell` / `search-availability-cell` spans. «Candidatos que encajan» on the
  position page reuses `SearchResults`, so it follows automatically.
- The panel line in `candidate-availability.tsx` is one bold `<p>` holding either the label or
  `candidate.availability.unavailableUntil` («No disponible hasta el {{date}}»), then «(vencido)».
- `formatElapsed()` already returns a full localized phrase («hace 6 meses», «hoy»).
- Position status renders `<span className="badge">` with `positions.status.<status>` in three
  places; the export history uses `status status--success`, which no stylesheet defines.

No API, data model, authorization, storage or database change: this slice is SPA presentation
only, so there are no migrations, grants or security boundaries to move.

## Goals / Non-Goals

**Goals:**

- One way to draw a toned chip, so the tone rule lives in CSS tokens and two pure helpers, not in
  per-component conditionals.
- Keep every existing test id and the live-region behaviour; add test ids for chip and elapsed text.

**Non-Goals:**

- Re-toning other chips or changing plain `.badge` (follow-up ticket per the brief).
- A generic design-system component library; the chip stays a CSS class plus a thin component.

## Decisions

### D1. Tone modifiers on `.badge`, colours as tokens

Add `.badge--success`, `.badge--danger`, `.badge--neutral` next to `.badge`. They only override
`background` and `color`; the base rule already has no border, no hover and the default cursor.
New tokens in `:root`:

| Tone    | Background                     | Text                            | Measured ratio |
| ------- | ------------------------------ | ------------------------------- | -------------- |
| success | `--fj-green-bg` `#e8efe2`      | `--fj-green-dark` `#3a5727`     | 6.95:1         |
| danger  | `--danger-bg` `#fbe9e7` (new)  | `--danger-dark` `#9b2c24` (new) | 6.44:1         |
| neutral | `--neutral-bg` `#eceff3` (new) | `--fg-2` `#3a4a60`              | 7.82:1         |

Ratios computed with the WCAG 2.x relative-luminance formula. For reference the existing orange
badge measures 5.74:1.

- **Danger:** reusing `--danger` `#c8342b` on the same pale tint gives exactly 4.50:1 — on the
  limit, and 11px text gives no margin. A darker `--danger-dark` is chosen instead (resolves the
  brief's open question).
- **Neutral:** `--bg-2` `#fafafa` was suggested, but it is also the table-row hover background, so
  the chip would vanish on a hovered row. `#eceff3` is a cool grey that stays visible on white and
  on `--bg-2` while keeping `--fg-2` text at 7.82:1.
- The toned modifiers explicitly set `cursor: default`: table rows set `cursor: pointer`, which
  a nested `span` otherwise inherits. No `:hover` rule is added. A unit test checks the chip has
  no interactive role, and a browser test checks computed styling before and after hover.

Alternative considered: separate `.chip` class. Rejected: the base `.badge` geometry is already
right and e2e specs bind to `span.badge`.

### D2. A thin `StatusChip` component

`src/app/shared/components/status-chip.tsx` exports `ChipTone = 'success' | 'danger' | 'neutral'`
and `StatusChip({ tone, children, testId })`, rendering
`<span className={`badge badge--${tone}`} data-tone={tone} data-testid={testId}>`. `data-tone`
gives unit and e2e tests a stable attribute that does not depend on class names or Spanish text.
Six call sites would otherwise repeat the class template.

### D3. Tone helpers in `.logic.ts` files

- `availabilityTone(state): ChipTone` in `candidate-availability.logic.ts`
  (`unknown → neutral`, `available → success`, `unavailable → danger`).
- `positionStatusTone(status): ChipTone` in a new `features/positions/position-status.logic.ts`
  (`open → success`, `closed → neutral`), used by the positions list, position page and
  `candidate-positions-panel.tsx`.

Both are exhaustive `switch`/record lookups typed on the union, so a new state fails `tsc`.

### D4. Availability cell as a shared component

`availabilityCell()` (string) is replaced by `availabilityElapsed(state, checkedOn, now?)`, which
returns `formatElapsed(checkedOn)` for a known check and `null` otherwise. A new
`AvailabilityCell` component (`candidates/components/availability-cell.tsx`) takes `state`,
`checkedOn` and a `testIdPrefix` (`candidate-availability` / `search-availability`) and renders:

```
<span data-testid="{prefix}-cell" class="availability-cell">
  <StatusChip tone=… testId="{prefix}-chip">{label}</StatusChip>
  <span class="availability-cell__elapsed" data-testid="{prefix}-elapsed">hace 6 meses</span>
</span>
```

The elapsed span is omitted for «Sin comprobar». `.availability-cell` is `inline-flex`, wraps,
6px gap. The elapsed text is muted by size and weight (12px, regular) in `--fg-2` (9.02:1 on
white, 8.64:1 on the hovered row), not by `--fg-3`, which measures 3.69:1 on white and fails AA.

`candidate.availability.cell` is deleted; no new elapsed key is needed because `formatElapsed`
already returns the localized phrase.

### D5. Panel line composition

The line `<p data-testid="candidate-availability-line">` keeps its test id and stays outside the
live region. Inside: `StatusChip` (`testId="candidate-availability-chip"`), then, for an until
date, a text span with `t('candidate.availability.untilDate', { date })` → «hasta el {{date}}»
(`data-testid="candidate-availability-until"`), then the existing «(vencido)» span. The key name
`untilDate` avoids the existing `candidate.availability.until` («Hasta (opcional)») form label.
`candidate.availability.unavailableUntil` is deleted. The line drops `font-weight: 600` (the chip
carries its own weight), and becomes `inline-flex` with `align-items: center; flex-wrap: wrap;
gap: 6px` so «hasta el …» wraps on a narrow panel. The summary's `align-items: baseline` stays so
the metadata still lines up beside the line.

### D6. Export history

`advanced-search-page.tsx` replaces `status status--success` with
`<StatusChip tone="success">`. This file is not in `LEGACY_HARDCODED_COPY` (the copy already comes
from `t()`), so no lint list change.

### D7. No new dependency

Pure CSS and React; no npm package added.

## Risks / Trade-offs

- [E2e specs assert the combined line text, e.g. `toHaveText('Sin comprobar')` on
  `candidate-availability-line`] → the line still contains only the chip text for an unknown
  check, so `toHaveText` keeps passing; assertions that matter for tone switch to
  `candidate-availability-chip` and its `data-tone`. New selectors use test ids, never Spanish.
- [A chip in a centred table cell (KTL-31 `data-table.css` centres every cell) next to elapsed
  text may wrap awkwardly on narrow viewports] → `inline-flex` + `flex-wrap` with the chip first;
  checked in the existing responsive e2e run.
- [Green/red alone is not readable by colour-blind users] → the label is always present (spec);
  tone is reinforcement only.
- [New tokens drift from the corporate identity doc] → `docs/CORPORATE_IDENTITY_Kepler.md`
  «Badges y estados» and its token block are updated in the same change.

## Test strategy

- Unit: `availabilityTone`, `availabilityElapsed`, `positionStatusTone` in the logic spec;
  `AvailabilityCell` (chip tone, elapsed present/absent, test ids); panel spec (chip tone, «hasta
  el …», «(vencido)» order, live region unchanged); position pages and candidate positions panel
  (status chip `data-tone`); advanced search page (export history chip); `i18n.spec.ts` for the
  removed/added keys.
- E2e: `candidate-availability.spec.ts` asserts the panel chip `data-tone` and until text by test
  id; `candidate-list-paging.spec.ts` asserts the list cell chip and elapsed after its seeded check.
- No backend or security suite changes are required; the security suites are still run to show no
  regression.
