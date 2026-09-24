# KTL-26 — Compact search form filters for CV and candidate status

## [original]

The CV dropdown in the top part of the search parameters form is poor UX. Replace it with two
checkboxes (yes/no), both selected by default. Keep candidate status as a search parameter, but let
the option list fit whether it has two, three, seven or more values. Select every status by default
and make the status filter take much less space when the user is not changing it. Put text, CV and
status on one row when the viewport permits, wrapping when it does not. Provide quick ways to select
one status and to restore all statuses. The meaning of some candidate statuses may belong to a
candidate–position relationship; review that domain concern separately.

## [enhanced]

**Status:** Proposed

### Summary

The shared `SearchCriteriaForm` currently puts text and a three-option CV `<select>` in one column
and an always expanded, bordered set of five candidate-status checkboxes in another. The status
group consumes substantial space even when all statuses are selected and the filter is inactive.
Recruiters should be able to scan and change these three basic filters quickly without changing
what a search means.

### User story

As a recruiter building a search, saved preset or position requirement, I can see the text, CV and
candidate-status filters together, narrow either categorical filter in one or two actions, and
restore its unrestricted default without losing track of what is selected.

### Decisions from discovery (2026-09-24)

1. **Single row when space permits.** Text is flexible in width; the CV choices and closed status
   control are compact. The controls wrap cleanly as space narrows, without horizontal scrolling or
   a second mobile DOM tree.
2. **CV is a pair of checkboxes.** Use the existing Spanish labels `Con CV` and `Sin CV`, both
   checked by default. Both checked maps to the existing empty `hasCv` value (no restriction); only
   `Con CV` maps to `yes`; only `Sin CV` maps to `no`. The last checked choice cannot be unchecked,
   because no choice would misleadingly appear to mean zero results while the existing API has no
   such state. Keep both boxes operable with keyboard and pointer.
3. **Status is a compact disclosure.** Its closed label communicates the current selection:
   `Todos los estados` when unrestricted, the status name when one is selected, and a concise
   selected-count summary when several but not all are selected. It starts closed when all statuses
   are selected. Opening it reveals a vertical popover over the page content, anchored to the status
   control without moving the filters below it. A long list scrolls inside the popover.
4. **Quick single or multiple selection.** Statuses use checkboxes for multiple selection. Each row
   also has a radio-shaped button that selects only that status, with a distinct Spanish accessible
   name. The repeated textual `Solo` actions were removed after review. When the selection is partial,
   `Seleccionar todos` restores all statuses. The last selected status cannot be unchecked. There
   is no `Deseleccionar todos` action.
5. **Candidate status vocabulary stays as is.** This ticket does not rename or remodel `new`,
   `available`, `in_process`, `hired` and `rejected`. Whether some statuses belong to a
   candidate–position relationship is a separate domain decision.

The layout and interaction sketch is in [docs/ktl-26/search-form-ux.md](../docs/ktl-26/search-form-ux.md).

### Existing behavior and contract

- `SearchCriteriaForm` is shared by advanced search, preset create/edit and position create/edit;
  changing it affects all three journeys.
- `SearchFilters.hasCv` remains `'' | 'yes' | 'no'`; `statusValues` remains an array of candidate
  statuses. The server treats an empty or complete status selection as unrestricted. Existing saved
  presets and position requirements retain their stored shape and meaning.
- `Con CV` means a non-removed **primary** CV exists, regardless of its scan state. It does not
  promise that the document can be opened. `Sin CV` means no primary CV, even if other documents
  exist.
- The current backend recognizes exactly five candidate statuses. The option layout should adapt
  to different list lengths, but this ticket does not add or remove status values.

### Acceptance criteria

```gherkin
Scenario: Default basic filters
  Given a new criteria editor
  Then Con CV and Sin CV are checked
  And the status control is closed and says Todos los estados
  And the search remains unrestricted by CV or status

Scenario: Restrict and restore CV presence
  When the user unchecks Sin CV
  Then the filter value is hasCv=yes
  When the user checks Sin CV again
  Then the filter value is hasCv=""
  And both choices are checked

Scenario: CV selection cannot become empty
  Given only Con CV is checked
  Then the user cannot leave both CV choices unchecked
  And the visible checked choice agrees with the active filter

Scenario: Choose one status
  Given all statuses are selected
  When the user opens Estados and activates the select-only button for Disponible
  Then only Disponible is selected
  And the closed control summarizes Disponible

Scenario: Restore every status
  Given only Disponible is selected
  When the user activates Seleccionar todos
  Then every status is checked and the filter is unrestricted

Scenario: Status selection cannot become empty
  Given only one status is selected
  Then the user cannot leave every status unchecked
  And the visible selection agrees with the active filter

Scenario: Responsive layout
  Given two, three or seven status options
  Then the vertical popover stays inside the viewport and scrolls if needed
  And opening it does not move the lower filters
  And text, CV and closed status share one row when space permits
  And the controls wrap on a narrow viewport

Scenario: Shared criteria editor
  Given the advanced search, preset editor and position editor
  Then each uses the same CV and status interaction and accessible names

Scenario: Saved criteria
  Given an existing preset or position requirement with hasCv=yes and selected statuses
  When its criteria editor opens
  Then the checkboxes and closed status summary represent those stored values
  And saving without changes preserves their meaning
```

### Implementation scope

- Update `frontend/src/app/features/search/components/search-criteria-form.tsx` and
  `search-filters.css`; keep pure selection and summary helpers in a sibling `.ts` file. Preserve
  the current controlled `SearchFilters` flow and the outer form's submit/collapse behavior.
- Add flat Spanish i18n keys in `frontend/src/assets/i18n/es.json` for new labels and actions.
  Do not hardcode new JSX copy. Existing CV labels may be reused.
- Keep the summary used by the collapsed whole form and read-only preset views consistent with the
  same filter semantics. Opening the status disclosure must not run a search or save a preset.
- No API, persistence, permission, database, migration, grant, storage or new dependency change is
  intended. Candidate-list filters are a separate UI and are outside this ticket.

### Verification and documentation

- Update focused component tests for default CV/status state, each CV mapping, status checkboxes,
  select only, select all,
  non-empty selection, disclosure summary, keyboard access and shared hosts.
- Update affected Playwright selectors that currently target `select[name="hasCv"]`; cover desktop
  and narrow viewport layout with no horizontal overflow.
- Run the affected frontend tests and a targeted Playwright journey, then `npm run lint` and
  `npm run format:check` from `frontend/` before marking implementation done.
- Update the relevant candidate-search and shared-criteria-editor specs and user-facing docs when
  implementing. Preserve search authorization and personal-data protections.

### Out of scope

- Revising candidate status values or introducing candidate–position statuses.
- Changing primary-CV presence semantics or document download permissions.
- Changing the search request shape or the separately implemented candidate-list filters.
