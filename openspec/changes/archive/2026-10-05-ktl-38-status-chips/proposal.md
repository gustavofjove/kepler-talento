## Why

Every `.badge` in the SPA is pale orange with dark orange text, the same hue as the primary
button, so a status chip beside an action reads as something clickable, and the two position
statuses («Abierta», «Cerrada») look identical. Candidate availability («Sin comprobar»,
«Disponible», «No disponible») is plain text in the tables and in the Disponibilidad panel, so a
reader has to read every cell to tell the values apart. Coloured text was tried and rejected:
dark tones blend with body text and bright ones read as errors or links. Pale tinted chips can
carry a clearly green or red tone and stay readable (brief: `openspec/KTL-38.md`).

## What Changes

- Three tone modifiers for `.badge`: success (green), danger (red) and neutral (grey), each with
  a pale background and dark text that reaches WCAG AA (4.5:1). New danger and neutral colour
  tokens are added because no existing pair is both tinted and accessible. Plain `.badge` stays
  orange.
- Candidate availability becomes a chip in the candidate list, the advanced search results and
  «Candidatos que encajan»: neutral «Sin comprobar», green «Disponible», red «No disponible». A
  known check is followed by the elapsed time as muted text («hace 6 meses») instead of the
  combined «No disponible · hace 6 meses» string.
- The Disponibilidad panel line shows the same chip, then «hasta {date}» when an until date is
  set, then the existing «(vencido)» marker. The «Comprobado el … por …» metadata is unchanged.
- Position status is a toned chip wherever it is shown (positions list, position page, the
  candidate's «Posiciones» panel): green «Abierta», neutral «Cerrada».
- The export history «Completada», which today uses undefined `status status--success` classes,
  is drawn as a success chip.
- Unchanged: the «Disponibilidad» filter options and filter chip, the CSV export values, API
  contracts, permissions and stored data.

Actors: every reader of the candidate list, search results and position pages (`candidates.read`,
`positions.read`); editors of availability (`candidates.update`) see the same chip in the panel.

Edge cases: an unchecked candidate shows the neutral chip with no elapsed text; an «No disponible»
check without an until date shows the chip alone; a lapsed until date keeps «(vencido)» after
«hasta {date}»; rows on hover keep the chip legible (the neutral background is darker than the
hover background).

Success criteria: the acceptance criteria in the brief — each value renders with its tone in
every listed place, no chip has a solid fill, border, hover change or pointer cursor, each tone
measures ≥ 4.5:1, and `npm run lint`, `npm run format:check` and `npm test` pass.

Personal data and security: this is presentation only. The chip shows the same availability value
and elapsed time the tables already project; no new field, endpoint, permission, RLS policy,
storage path or role is touched, so principles 1 and 3 are unaffected.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `data-tables`: new requirement that status values are toned chips (tones, contrast, no button
  cues, label always present), covering availability, position status and export history; the
  candidate availability column shows the value as a chip followed by the elapsed time.
- `candidate-profile-pages`: the availability block's line shows the value as a chip, followed by
  «hasta {date}» and the lapsed marker as text.

## Impact

- `frontend/src/styles.css`: tone modifiers and new `--danger-*` / `--neutral-bg` tokens.
- `frontend/src/app/features/candidates/components/candidate-availability.logic.ts` (tone helper,
  cell helper), `candidate-availability.tsx/.css`, `candidate-table.tsx`,
  `candidate-positions-panel.tsx`.
- `frontend/src/app/features/search/components/search-results.tsx`,
  `search/pages/advanced-search-page.tsx`.
- `frontend/src/app/features/positions/position-list-page.tsx`, `position-detail-page.tsx`, plus a
  shared position-status tone helper.
- `frontend/src/assets/i18n/es.json` (and `en.json`): elapsed / until keys replace
  `candidate.availability.cell` and `candidate.availability.unavailableUntil`.
- Unit specs for the availability helpers, panel, tables and position pages; e2e
  `candidate-availability.spec.ts` assertions on the panel line.
- `docs/CORPORATE_IDENTITY_Kepler.md`: record the chip tones and tokens.
- No backend, API, database or dependency changes.
