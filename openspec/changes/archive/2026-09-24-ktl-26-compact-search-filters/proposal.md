## Why

The shared search criteria editor uses a CV dropdown and an always expanded candidate-status group.
The status group occupies substantial space even when it has its unrestricted default, and narrowing
or restoring a selection takes more effort than necessary. Recruiters need a compact, readable top
row that works equally well in searches, saved presets and position requirements.

## What Changes

- Replace the CV dropdown with `Con CV` and `Sin CV` checkboxes, both checked by default. Preserve
  the existing unrestricted, `yes` and `no` filter values.
- Present candidate status as a compact disclosure. Show all statuses selected by default, summarize
  the current selection while closed, and open a vertical popover over the content.
- Keep status checkboxes for multi-selection, add a radio-shaped button to select only one status,
  and offer `Seleccionar todos` for partial selections. Keep at
  least one option selected in both the CV and status groups so the visible state always agrees
  with the query.
- Place text, CV and the closed status control on one row when space permits and let them wrap on
  narrower screens. Apply the same controls to every host of the shared criteria editor.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `saved-search-presets`: Extend the shared criteria editor requirement to cover the CV and status
  controls, their selection rules and responsive layout across search, preset and position editors.

## Impact

- **Actors and value:** Recruiters and HR users can narrow or restore CV and status filters quickly
  while preserving the meaning of existing searches, presets and position requirements.
- **Entities and contracts:** `SearchFilters.hasCv` and `statusValues` retain their current wire and
  stored shapes. Empty or complete status selection remains unrestricted at the API boundary; the
  UI avoids an empty-looking selection. Primary-CV presence keeps its current meaning.
- **Code:** Shared React criteria form, its styles, translation strings, selection helpers and
  affected frontend tests. Existing Playwright selectors for the CV dropdown need updating.
- **Boundaries:** No new endpoint, dependency, table, migration, permission or database grant.
  Personal data remains behind the existing permission-checked API. RLS policies, role definitions,
  document storage and storage access are unchanged. Search terms and filters must still stay out of
  URLs and logs.
- **Scope:** Candidate status names and any candidate–position status model are outside this change.
  Success means the default is visibly unrestricted, one-choice and restore-all actions work, the
  controls fit desktop and narrow viewports without overflow, and existing stored filters retain
  their meaning.
