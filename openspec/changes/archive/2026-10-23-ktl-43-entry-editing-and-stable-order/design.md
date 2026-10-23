## Context

See `proposal.md` for motivation and `specs/` for the required behaviour.

- The position page keeps a position's links in `usePositionCandidates` (KTL-30 design D5): it
  loads once and then applies each write's response locally, re-sorting with
  `sortPositionCandidates`, the client copy of the API order. That re-sort moves a restaged row.
- Educación and Experiencia (KTL-29) stage a draft list through `usePanelDraft`, whose dirty flag
  compares `JSON.stringify` of draft and saved values. Each panel has one form that only adds.
- `SetCandidateEducationHandler` and `SetCandidateExperienceHandler` already update an entry whose
  `id` matches a stored row (`existing.SetValues(...)`), and `CandidateCatalogLookup.ResolveAsync`
  resolves inactive catalog values. Entry editing therefore needs no backend change.

## Goals / Non-Goals

**Goals:**

- Keep a restaged row in place without changing the API contract or the reload order.
- Reuse each panel's existing form for editing, so add and edit share validation and markup.
- Leave the panel clean after a no-op edit, under the existing content-comparing dirty check.

**Non-Goals:**

- A start year for education (schema change).
- An inline row editor or a dialog.
- Any change to the API, its order, schema or grants.

## Decisions

### D1. Replace the restaged link in place on the client

`changeStage` swaps in the server's response with `replacePositionCandidate`, which maps the list
by candidate id; a link no longer in the list falls back to the sorted insert. Adding a link keeps
`upsertPositionCandidate`, so new rows appear where the API would put them. Reloads (including the
stale-write reload) use the API order.

_Alternative:_ drop stage from the API order (newest added first) on both server and client. It
gives one order everywhere, but loses the by-stage grouping on load and changes the API contract,
its spec and its query. The user chose the client-only option.

### D2. Years derived in a pure helper

`candidate-period.logic.ts` derives `educationPeriod` and `experiencePeriod` from the stored values
(years read from the `YYYY-MM-DD` date prefix). Range and «actualidad» text come from `es.json`
keys with interpolation, and single years are rendered as plain integers, not through
`formatNumber`, which would insert a thousands separator. Each panel shows the column only when
some row has a period, so panels without years keep their current look.

### D3. Edit through the existing form

Each panel holds `editingId` beside its form draft. «Editar» copies the entry into the draft
(`toDraft`), focuses the first field through a ref, and switches the title, submit label and the
«Cancelar edición» button. Submitting with an entry in edit replaces it with `replaceEntry`;
otherwise it appends as before. The entry in edit is derived (`editing = canEdit ? find(editingId)
: undefined`), so a removed entry or a closed panel cannot leave a dangling edit; `clearForm` runs
on cancel, remove-of-the-edited-entry and successful save so the next «Editar» of the panel starts
empty.

_Alternative:_ an inline editor per row. It duplicates the six- and seven-field forms per row and
does not fit at 390 px; a dialog would add a modal flow for a short form.

### D4. Merge only changed fields

`applyEntryDraft(original, draft)` starts from the stored entry and applies a draft field only when
its value differs, treating blank and missing as equal, and stores a cleared field as `undefined`.
This keeps fields the form does not show (`notes`, `functions`), and keeps the entry's JSON
identical after a no-op edit, so `usePanelDraft` reports the panel clean.

### D5. Keep retired catalog values selectable

`optionsWithCurrent(active, current)` prepends the draft's value to the active options when it is
not among them. Without it the controlled `<select>` would show blank and the next update would
drop the value. The API accepts the inactive value on write.

### D6. Experience form order

Fields are reordered within the existing `grid two`; «Puesto actual» takes a full row (`span-all`)
right above the dates because it disables «Fecha fin». Every `name=` and `id` is unchanged, so the
Playwright suite is unaffected.

## Risks / Trade-offs

- [The position list is out of API order until the next load] → Accepted by design; reloading or
  any stale write restores it.
- [Typed changes in the form are lost if the panel is saved without «Actualizar»] → Same as the
  existing add form; the form title and the highlighted row show that an edit is pending.
- [A retired value appears first in its select] → Only while that entry is in edit; it is the value
  the entry already holds.

## Security, data and storage

No new endpoint, permission, data path, field, grant or storage access. The panels still write
through `CandidateRelationsService`, whose API calls are permission-checked and fail closed; no
security suite changes are needed.

## Test strategy

Unit specs for the pure helpers (`candidate-period.logic`, `candidate-entry-form.logic`,
`position-candidates.logic`) and component specs for the panels and the position page. The
existing `candidate-crud` and `position-candidates` Playwright journeys exercise the forms by
`name=` and still apply; a targeted run confirms them.

## Migration Plan

Front-end deploy only; rollback is a redeploy of the previous bundle.
