## Why

KTL-30 introduced a table pattern for the position candidate tables and the advanced search
results: the row opens the record, the name is the keyboard link, phones are plain text, and
cells are aligned with compact controls. The other lists still mix «Abrir», «Editar» and
eye-icon buttons, top-aligned cells and tall controls. Users meet a different way to open a
record on every screen. Source brief: `openspec/KTL-31.md`.

## What Changes

- **Shared table style.** A `.data-table` style centres cells vertically and gives dropdowns
  and row buttons the compact 28px height. It is applied to Candidatos, Posiciones, Presets,
  Catálogos, Usuarios and the three KTL-30 tables. It replaces the KTL-30 rules scoped to
  `positions.css`.
- **Clickable rows:**
  - **Candidatos** rows open the candidate page. «Abrir» is removed, and the selection
    checkbox cell never navigates.
  - **Posiciones** rows open the position page.
  - **Presets** rows open the preset's edit page.
  - In all three, links and controls inside a row keep their own behaviour, Ctrl/⌘-click and
    middle-click open a new tab, and the name or title remains a real link for keyboard users.
- **Presets list:**
  - **BREAKING (spec):** the eye button, the read-only view dialog and «Editar» are removed.
  - Each preset shows its criteria summary on a full-width line below its values.
  - «Eliminar» stays and keeps its confirmation.
- **Catálogos** rows open their inline editor on click; «Editar» is removed and «Subir»/«Bajar»
  become arrow icon buttons (review follow-up).
- **Usuarios** gets styling only. Its rows are edited in place and have no page to open. A user
  detail page is a future ticket.
- **Phones** are plain text in the candidates table and the candidate page header, so no `tel:`
  link remains. The e-mail stays a `mailto` link.

## Capabilities

### New Capabilities

- `data-tables`: the shared behaviour and styling of record tables:
  - which tables have clickable rows and where each row leads;
  - the controls exclusion, the selection-cell opt-out and modifier-key behaviour;
  - the keyboard link and its focus ring;
  - plain-text phones;
  - vertically centred cells and compact controls;
  - which tables deliberately have no row navigation.

### Modified Capabilities

- `saved-search-presets`: «Preset administration section». The view control and dialog are
  replaced by rows that open the edit page and an inline criteria line per preset, reversing
  the rule that rows render no criteria.

## Impact

- **Frontend only.**
  - `shared/components/row-link.ts` (opt-out attribute) and a new
    `shared/components/data-table.css`.
  - The candidate table and candidate page header, and `contact-links.ts` (`telHref` removed).
  - The positions list and `positions.css`.
  - The preset list page, the catalog management page and the users page.
  - `SearchResults` and both KTL-30 panels, which switch to the shared class.
  - `es.json`: unused keys removed.
- **No backend, API, database, permission, grant or storage change.** The preset list endpoint
  already returns each preset's filters.
- **Personal data (principle 1).** No new data is exposed: every value shown was already on the
  same screen or in the same response. Phones become less actionable, not more visible.
- **Authorization (principle 3).** Unchanged. Row destinations are routes the actor could already
  open, behind the same route guards: candidate page (`candidates.read`), position page
  (`positions.read`), preset edit page (`presets.manage`, the same permission the list needs).
- **Tests.**
  - Unit: the candidate list, preset list, positions list, users, catalogs, contact links and
    candidate page specs.
  - E2E: `advanced-search-presets.spec.ts` uses `preset-view` and `preset-edit` today.
- **Success criteria:** the 10 acceptance criteria in `openspec/KTL-31.md` pass, and the
  390px-wide and keyboard checks hold.
- **Dependencies:** none.
