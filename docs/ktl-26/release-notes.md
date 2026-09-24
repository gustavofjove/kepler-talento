# KTL-26: Compact CV and status search filters

The basic search filters now share a row when space permits and wrap on narrow screens. The CV
filter uses `Con CV` and `Sin CV` checkboxes, both selected by default. Candidate status starts as
a compact `Todos los estados` control. Opening it shows a vertical popover over the content, with
status checkboxes and a radio-shaped button on each row to select only that status. A partial
selection offers `Seleccionar todos` at the end.
Clicking outside the status options or pressing Escape closes them without clearing the selection.

At least one CV choice and one status remain selected. Both CV choices or all statuses mean no
restriction in that family. The same editor appears in advanced search, saved preset editing and
position requirements. Existing presets and positions retain their filter values and meaning.

This release changes only the frontend interaction. The search API, candidate status values,
primary-CV semantics, permissions, PostgreSQL schema and private-document access remain unchanged.
