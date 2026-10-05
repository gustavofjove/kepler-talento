## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-38` from an up-to-date `main`

## 1. Chip tones and shared chip (data-tables: Status values are shown as toned chips)

- [x] 1.1 Add `--danger-bg`, `--danger-dark` and `--neutral-bg` tokens and the `.badge--success`,
      `.badge--danger`, `.badge--neutral` modifiers to `frontend/src/styles.css` (design D1); no
      border, solid fill or hover rule; set the default cursor to override clickable-row inheritance
- [x] 1.2 Add `src/app/shared/components/status-chip.tsx` with `ChipTone` and `StatusChip`
      (`badge badge--<tone>`, `data-tone`, optional `data-testid`) (design D2)
- [x] 1.3 Add a unit spec for `StatusChip`: class, `data-tone`, test id, no interactive role

## 2. Candidate availability as a chip (data-tables: Candidate tables show availability)

- [x] 2.1 In `candidate-availability.logic.ts`, add `availabilityTone(state)` and
      `availabilityElapsed(state, checkedOn, now?)`; remove `availabilityCell` (design D3, D4)
- [x] 2.2 Add `candidates/components/availability-cell.tsx` (+ `.css`) rendering the chip and the
      muted elapsed span with `<prefix>-cell`, `<prefix>-chip` and `<prefix>-elapsed` test ids
- [x] 2.3 Use `AvailabilityCell` in `candidate-table.tsx` (`candidate-availability`) and
      `search-results.tsx` (`search-availability`), keeping the existing cell test ids
- [x] 2.4 Remove `candidate.availability.cell` from `es.json` (and `en.json` if present)

## 3. Availability panel line (candidate-profile-pages: Availability block on the candidate page)

- [x] 3.1 In `candidate-availability.tsx`, render the line as chip + «hasta el {date}» +
      «(vencido)» with `candidate-availability-chip` and `candidate-availability-until` test ids;
      keep `candidate-availability-line` and the live region unchanged (design D5)
- [x] 3.2 Add `candidate.availability.untilDate` («hasta el {{date}}») and remove
      `candidate.availability.unavailableUntil` in `es.json` (and `en.json`)
- [x] 3.3 Update `candidate-availability.css`: line becomes wrapping `inline-flex`, drops its bold
      weight; metadata still sits beside it on a wide panel

## 4. Position status and export history chips (data-tables: Status values are shown as toned chips)

- [x] 4.1 Add `features/positions/position-status.logic.ts` with `positionStatusTone(status)`
- [x] 4.2 Render position status with `StatusChip` in `position-list-page.tsx`,
      `position-detail-page.tsx` and `candidate-positions-panel.tsx`
- [x] 4.3 Replace `status status--success` in `advanced-search-page.tsx` with a success
      `StatusChip` (design D6)

## 5. Unit tests (mandatory: review and update affected tests)

- [x] 5.1 Update `tests/unit/candidate-availability.logic.spec.ts`: replace the `availabilityCell`
      case with `availabilityTone` and `availabilityElapsed` cases; add `positionStatusTone`
- [x] 5.2 Update `tests/unit/candidate-availability.spec.tsx`: chip `data-tone` per state, «hasta
      el …» for an until date, «(vencido)» after it when lapsed, live region unchanged
- [x] 5.3 Update `candidate-list-page.spec.tsx`, `search-results.spec.tsx` and
      `candidate-detail-page.spec.tsx` where they read the availability cell or line: assert chip
      tone and elapsed text separately; unchecked shows no elapsed element
- [x] 5.4 Update `position-pages.spec.tsx` and `candidate-positions-panel.spec.tsx`: «Abierta» is
      `data-tone="success"`, «Cerrada» is `data-tone="neutral"`
- [x] 5.5 Update `advanced-search-page.spec.tsx`: export history status is a success chip
- [x] 5.6 Check `i18n.spec.ts` still passes with the removed and added keys

## 6. Run suites

- [x] 6.1 Run `npm test` from `frontend/` (unit + integration + security projects) and inspect the
      output; fix any failure
- [x] 6.2 Run `npm run build:all` and confirm tsc, Vite and dotnet build succeed (no backend change
      expected; no PostgreSQL or storage state changes to verify for this presentation-only slice)

## 7. End-to-end

- [x] 7.1 Update `tests/e2e/candidate-availability.spec.ts`: assert the panel chip by
      `candidate-availability-chip` and its `data-tone`, and the until text by
      `candidate-availability-until`; no new Spanish selectors
- [x] 7.2 Update `tests/e2e/candidate-list-paging.spec.ts`: after the seeded check, assert the list
      cell's chip `data-tone` and that the elapsed element is present
- [x] 7.3 With `docker compose up` running, run
      `npx playwright test tests/e2e/candidate-availability.spec.ts tests/e2e/candidate-list-paging.spec.ts tests/e2e/candidate-crud.spec.ts tests/e2e/advanced-search.spec.ts`
      and inspect the report
- [x] 7.4 Run the full `npm run e2e` and confirm the global teardown purged the marked test data

## 8. Security regression check

- [x] 8.1 Confirm the change adds no endpoint, permission, data field or storage path (diff review);
      run `npm run security:rls && npm run security:storage` and confirm they still pass

## 9. Documentation, lint and format

- [x] 9.1 Update `docs/CORPORATE_IDENTITY_Kepler.md` «Badges y estados» and its token block with the
      three tones, new tokens and measured contrast ratios
- [x] 9.2 Run `npm run lint` and `npm run format:check` from `frontend/` and fix any finding
- [x] 9.3 Run `openspec validate ktl-38-status-chips --strict`
