## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-31`, named after the ticket file `openspec/KTL-31.md`. Branch from `main` once the KTL-30 work is committed and merged, or from `feat/KTL-30` if the user asks to stack it. (Proposal: traceable KTL-31 delivery.)

## 1. Shared pieces

- [x] 1.1 Add `shared/components/data-table.css` with `.data-table`: vertically centred cells, and compact `select`, text `input` and `.button` inside cells at the `.button.small` height. Remove `.position-links-table` and the compact select rules from `positions.css`, keeping `.position-stage-select`'s `min-width`. (Design D1. Spec: consistent table styling.)
- [x] 1.2 Extend `useRowLink` so that clicks inside `[data-row-link-ignore]` never navigate. Add `.row-link-group:hover td` to `row-link.css` for multi-row records. (Design D2, D3. Spec: rows open their record.)
- [x] 1.3 Switch `SearchResults`, `position-candidates-panel.tsx` and `candidate-positions-panel.tsx` to `.data-table`. (Design D5.)

## 2. Tables with clickable rows

- [x] 2.1 Candidatos (`candidate-table.tsx`):
  - apply `.data-table` and `useRowLink`;
  - make the name the `.row-link`;
  - mark the selection `<td>` with `data-row-link-ignore`;
  - remove «Abrir»;
  - render the phone as text.

  (Spec: rows open their record; phones are plain text.)

- [x] 2.2 Posiciones (`position-list-page.tsx`): apply `.data-table` and `useRowLink`, with the title as the `.row-link`. (Spec: rows open their record.)
- [x] 2.3 Presets (`preset-list-page.tsx` and `.css`):
  - render one `<tbody className="row-link-group">` per preset;
  - the values row has the name `.row-link` to the edit page and «Eliminar»;
  - the full-width filters row shows `SearchCriteriaSummary`;
  - remove the eye button, `EyeIcon`, the `viewing` state, the dialog and «Editar».

  (Design D3. Spec: preset administration section.)

## 3. Styling-only tables and phones

- [x] 3.1 Apply `.data-table` to the Catálogos table and the Usuarios table, with no row hook. Check that their inline edit, move, activate and role controls render compact and centred. (Design D5. Spec: tables without a record page have no row navigation.)
- [x] 3.2 Render the candidate page header phone as text. Delete `telHref` from `contact-links.ts`. (Design D4. Spec: phones are plain text.)
- [x] 3.3 Remove the now-unused `es.json` keys (`candidates.list.open`, `presets.action.view`, `presets.action.edit` and the preset view dialog keys), after checking each has no remaining use. `npm run lint` must pass.

## 4. Unit tests

- [x] 4.1 Update the affected specs:
  - `row-link` opt-out;
  - `candidate-list-page.spec.tsx`: row navigation, the checkbox cell not navigating, no «Abrir», plain phone;
  - position pages: the list row click;
  - `preset-list-page.spec.tsx`: dialog tests replaced by a filters line (with and without criteria), row and name opening edit, «Eliminar» confirming without navigating;
  - `contact-links.spec.ts`: the `telHref` case removed.
- [x] 4.2 Add non-navigation tests for the Catálogos and Usuarios rows, and a plain-phone assertion on the candidate page header.
- [x] 4.3 Run `npm test` and inspect the output.

## 5. End to end

- [x] 5.1 Search `frontend/tests/e2e` for `preset-view`, `preset-edit`, `tel:` and «Abrir» usage. Update `advanced-search-presets.spec.ts` to open a preset through its row or name link and assert the inline criteria line.
- [x] 5.2 Add a row-click step to the candidate list and positions e2e specs, with `Date.now()`-marked data only.
- [x] 5.3 Rebuild the stack if the image is stale (`docker compose up --build -d`), then run the targeted Playwright specs: presets, candidates list, positions, navigation-responsive, position-candidates and advanced-search. Inspect the results and confirm the teardown purged the marked data.

## 6. Documentation

- [x] 6.1 Write `docs/ktl-31/release-notes.md`. Add a short Spanish section to `README.md` on consistent tables. Note that the user detail page and the view/edit merges are future work.

## 6b. Review follow-up

- [x] 6b.1 Catálogos: row click starts the inline edit through a shared `isRowClick`; the name is the keyboard button; «Editar» removed; «Subir»/«Bajar» become labelled arrow icon buttons. Unit and e2e specs updated. (Design D7. Spec: catalog rows open their inline editor.)
- [x] 6b.2 Presets: table inside a `.panel`; criteria rendered with `SearchCriteriaSummary layout="inline"` as one wrapping line of chips; `.filters-summary .chip` styled for every host. (Design D8. Spec: preset administration section.)

- [x] 6b.3 Verify follow-up: `tests/e2e/data-tables.spec.ts` checks Candidatos, Catálogos, Usuarios and Presets at 390px. It found a 2px page overflow on Catálogos from `.field--wide`'s fixed `min-width: 320px`, now capped at `min(100%, 320px)`. Brief amended with the review decisions. (Spec: consistent table styling, narrow viewport.)

## 7. Quality gates

- [x] 7.1 Run `npm run security:rls` and `npm run security:storage`. The boundary is unchanged, but the gates are run regardless.
- [x] 7.2 From `frontend/`, run `npm run lint` and `npm run format:check`.
- [x] 7.3 Run `npm run build:all`.
