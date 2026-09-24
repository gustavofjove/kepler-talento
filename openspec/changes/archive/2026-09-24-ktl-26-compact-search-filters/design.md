## Context

See [proposal.md](proposal.md) for the user problem. `SearchCriteriaForm` is a controlled React
component shared by advanced search, preset editing and position editing. It currently stores CV
presence as `'' | 'yes' | 'no'` and status as `CandidateStatus[]`; the API and persisted presets and
position requirements use the same values. An empty or complete status array is unrestricted. The
frontend default already contains every status, while the CV default is `''`.

The current form gives the CV control a native select and all status options an always visible
fieldset. Search execution remains owned by the host form; editing filter values does not by itself
refresh results. The outer search filter panel also has its own collapse control, independent of the
proposed status disclosure.

## Goals / Non-Goals

**Goals:**

- Keep one controlled editor and one filter shape for search, presets and position requirements.
- Make the basic filters compact, responsive and keyboard accessible without changing the API.
- Keep the closed status label, expanded checkboxes and filter values in agreement, including for
  existing stored filters.

**Non-Goals:**

- Define new candidate statuses or a candidate–position status model.
- Change what qualifies as a primary CV or when a document may be downloaded.
- Change the candidate-list filter bar, backend filtering, permissions or the whole-form collapse.

## Decisions

### Derive CV checkbox state from the existing value

Render `Con CV` checked for `''` and `yes`, and `Sin CV` checked for `''` and `no`. A checkbox change
computes the next pair and maps it back to exactly one of the existing three wire values. Reject a
transition that would leave both unchecked, so the visible state never implies an impossible
zero-result filter. Keep this mapping in a pure sibling `.ts` helper; the component sends the next
immutable `SearchFilters` value through `onFiltersChange`.

A new two-boolean persisted shape would require backend, preset and position migrations without
changing search capability. A radio group would preserve the old three-option interaction but
would not show both included categories as directly as the requested checkboxes.

### Keep status disclosure state local and separate from filter state

Use an actual button with `aria-expanded` and `aria-controls` to open an anchored popover. Its
open state belongs to the editor instance, while selected statuses remain in the controlled
`SearchFilters`. Initial state is closed; opening and closing never calls `onFiltersChange` or
`onSubmit`. While open, a document pointer listener closes it only for targets outside the button
and panel. Escape closes it when focus is in either control and returns focus to the button. The
listeners are removed while closed and on unmount. When the host collapses the entire form, the
status option panel unmounts with it.

The status button derives its label from the selected set and the known option set: all selected
means `Todos los estados`, one selected uses its localized name, and a partial multi-selection uses
a localized count. For a legacy empty status array, render all options selected because the API
already treats that array as unrestricted; retain the stored value until the user actually changes
the selection. Users narrow the set with the checkboxes; `Seleccionar todos` restores the full
known set. Ordinary checkbox toggles reject removal of the last selected status. Use the status
option list rather than hardcoded count assumptions, and keep order deterministic. Each status also
gets an icon-only button whose circle resembles a radio control; it is a real button with a localized
`Seleccionar solo {status}` accessible name and selects that status alone. The circle is filled when
that status is the sole selection. Checkboxes remain available for multiple selection.

An always visible fieldset costs space in the common unrestricted case. The popover is positioned
against the disclosure so opening it does not push down the remaining criteria. It has a bounded
width and height, scrolls for longer lists, and aligns to the viewport edge at narrow widths.

### Layout follows available width

Use one basic-filter row with wrapping CSS and sensible minimum widths: text grows, while the CV
group and closed status control take only the width they need. Anchor the vertical popover to the
status control and bound its height with internal scrolling. On opening and window resize, measure
the button and panel and clamp the panel's horizontal offset within the viewport. The form wraps
near 790 px, so fixed left or right CSS alignment can overflow at adjacent widths. Use the existing
Kepler tokens and plain co-located CSS; render one DOM tree at all widths.

A rigid two-column layout reproduces the current wasted space. A fixed-height, unscrollable popover
would hide options in longer lists. The overlay must stay within the viewport at narrow widths.

### Keep labels and access paths consistent

Reuse the existing `Con CV` and `Sin CV` translation keys. Add flat Spanish keys for the status
summary, select-all and select-only actions. Native checkboxes keep their visible labels. Repeated
textual `Solo` actions were removed after review; compact radio-shaped buttons bring back the quick
single-status action without repeating the word in every row. They use button semantics because
the filter also permits multiple selected statuses. Preserve meaningful `name` and
`data-testid` attributes where the current
form and tests depend on them, replacing the old select's test selectors deliberately. The shared
read-only criteria summary continues to omit unrestricted CV and status families and show partial
selections.

Duplicate editor implementations in the three hosts would drift. Host-specific authorization stays
in the current pages and API; this component only edits values and never makes a permission
decision.

### Boundary, data and test strategy

This is a React, CSS and i18n change. The search API remains the only data boundary, with existing
`candidates.read` and position/preset permissions. No database model, EF migration, runtime grant,
RLS policy, role definition, document storage path or new runtime dependency changes. Candidate
identity and filter values must not be logged or put in URLs. Existing API authorization tests
remain the security evidence for the unchanged boundary.

Use focused Vitest/Testing Library tests for checkbox mapping, no-empty invariants, status labels,
checkbox selection, select only, select all, host parity, keyboard operation and no search on disclosure toggling. Update
existing Playwright journeys that select `select[name="hasCv"]`; use role/accessibility or stable
test-id selectors and check one wide and one narrow viewport for wrapping and horizontal overflow.
Keep the existing search contract tests as regression coverage for primary-CV and status semantics.

## Risks / Trade-offs

- **A final checked box cannot be cleared** → Guard the transition and retain its checked state;
  test both mouse and keyboard interaction so no empty-looking filter is ever submitted.
- **Legacy empty status arrays are unrestricted** → Render them as all selected before the user
  edits, and verify that applying an existing preset or position requirement still communicates
  its actual meaning.
- **The popover can overlap lower controls or a viewport edge** → Position it above the document
  flow with bounded dimensions and scrolling; verify its placement at narrow widths. Return focus
  to the disclosure button when Escape closes it.
- **Old Playwright selectors point to a removed select** → Update affected search and preset
  journeys to use accessible checkbox names or stable test identifiers in the same change.

## Migration Plan

No data migration is needed: the request and stored filter shapes remain unchanged. Deploy the
frontend bundle through the existing release flow. Existing saved presets and position requirements
render through the new controls. A frontend rollback restores the former controls without any data
conversion or backend rollback.
