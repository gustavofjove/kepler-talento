## [original]

I'd like to provide admins the ability of choosing different pastel colours for the different catalog items (excluding level catalogs) so that these colours are applied when their chips are displayed in the different sections they appear.

By default the colour would be the current pastel orange but a new "colour" property for catalog items will be available so when admins are adding or editing an item they should be able to click a circle which will open a dialog with a few options to select, clearly different to each other (6, 9 or 12 options). The read-only view will be a circle with the selected colour and its name will appear as the tooltip.

## [enhanced]

### User story

**As** a catalog administrator (`catalogs.manage`),
**I want** to give each skill, language, program and tag its own pastel colour,
**so that** recruiters can tell values apart at a glance wherever their chips appear (candidate
Competencias panel, search criteria, position requirements, saved presets).

### Decisions taken while refining

| Topic               | Decision                                                                                                                                                                                                                                                  |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Colourable families | Only the four chip families: `skill`, `language`, `program`, `tag` (the `CatalogFamilyKind` set of `catalog-family-rows.logic.ts`). Level families, `education_type`, `education_status` and `sector` get no colour. Nothing renders them as chips today. |
| Palette             | A closed set of **9** pastel colours. Each one is a stable token stored in the database, never a hex value.                                                                                                                                               |
| Default             | `orange`: the current chip look (`--fj-orange-pale` / `--fj-orange-soft` / `--fj-orange-dark`). Existing rows are migrated to it, so nothing changes visually until an admin picks another colour.                                                        |
| Uniqueness          | Colours are not unique. Any number of values can share one.                                                                                                                                                                                               |
| Inactive values     | Keep their colour. Records that still reference them show it.                                                                                                                                                                                             |
| When a pick saves   | Choosing a colour only changes the draft. It is written with the row's «Guardar» (edit) or the form's «Añadir» (create), like the name and code.                                                                                                          |
| Status chips        | KTL-38 status chips (`StatusChip`, `.badge--success/danger/neutral`) are unaffected. Catalog colours carry no status meaning.                                                                                                                             |

#### Palette

| Token    | UI name (`es.json`) |
| -------- | ------------------- |
| `orange` | Naranja (default)   |
| `yellow` | Amarillo            |
| `green`  | Verde               |
| `teal`   | Turquesa            |
| `blue`   | Azul                |
| `indigo` | Índigo              |
| `violet` | Violeta             |
| `pink`   | Rosa                |
| `grey`   | Gris                |

Each token defines a background, a border and a text colour of the same hue. Text on background
must reach **WCAG AA ≥ 4.5:1** at 12px/600 (chip size). Record the measured ratios in
`docs/CORPORATE_IDENTITY_Kepler.md`, as KTL-38 did for status chips. `orange` reuses the existing
`--fj-orange-*` tokens unchanged.

### Functional description

#### Catalog administration (`/admin/catalogs`, `CatalogManagementPage`)

1. When the selected family is colourable, the table gets a **«Color»** column between «Nombre»
   and «Estado». For level, education and sector families the column, the create-form field and
   the dialog do not appear.
2. **Read-only cell:** a filled circle in the item's colour (`role="img"`,
   `aria-label="Color: Azul"`). Its tooltip (`title`) is the colour name, e.g. «Azul».
3. **Editing a row** (the row's pencil, see below): the circle becomes a button. Its accessible
   name is e.g. «Cambiar el color de Inglés (actual: Azul)» and its tooltip is still the colour
   name. Activating it opens the colour dialog. The row's Enter/Escape and «Guardar»/«Cancelar»
   behave as today. «Cancelar» also discards a colour picked in the dialog.
4. **Create form:** a «Color» field holds the same circle button, starting at Naranja. The form's
   line is split in two halves: the name in one, and the colour circle followed by the code in the
   other. When the family changes, or the form is closed, the colour resets to Naranja.
5. **Colour dialog** («Elegir color»): a modal with the 9 swatches in a 3×3 grid. Each swatch
   shows its colour and its visible Spanish name. The current colour is marked with a check
   icon, so the selection does not rely on colour alone. The swatches form a single-choice grid:
   the arrow keys move between them without choosing, and Enter, Space or a click chooses.
   Choosing a swatch updates the draft,
   closes the dialog and returns focus to the circle button. Escape or «Cancelar» closes it
   unchanged. On a narrow screen each colour's name wraps under its circle.

#### Catalog administration actions (added during implementation)

Requested while KTL-41 was being built; specified in the `data-tables` capability.

6. **«Nuevo».** The add form is hidden by default. A «Nuevo» button on the family selector's line,
   to its right at every width, shows it and focuses the name. The form gets «Cancelar», which
   hides and empties it. A successful «Añadir» adds the value, then hides and resets the form.
7. **Row actions.** A click on a row no longer starts the inline edit (this reverses KTL-31 for
   Catálogos), and the name is plain text. «Acciones» holds icon buttons: a pencil «Editar», the
   reorder arrows, and «Desactivar» (an «Activar» icon for an inactive value). Each has a tooltip
   with the action and an accessible name with the value, e.g. «Desactivar Inglés». Deactivation
   keeps its confirmation.
8. **Locks.** While a row is edited, «Nuevo» and the other rows' actions are disabled. While the add
   form is open, «Nuevo» and the row edit buttons are disabled.

#### Where the colour is applied

| Surface                                                                                                                    | Component                                                       | How                                                                                                                                   |
| -------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Candidate «Competencias» panel (read and edit)                                                                             | `CatalogValuePicker` via `candidate-relation-section.tsx`       | Whole chip (value, level, detail and remove button) uses the value's colour                                                           |
| Search criteria form, position requirements form                                                                           | `CatalogValuePicker` via `search/components/criteria-group.tsx` | Same                                                                                                                                  |
| Read-only criteria summary: search page (collapsed), criteria dialog, preset list (inline), position detail (requirements) | `search/components/search-criteria-summary.tsx`                 | Chips of the skill/language/program/tag groups use the value's colour. Text, availability, «Revisado desde» and CV chips stay orange. |

The colour is resolved by family + Spanish name (the identity that picker items and saved criteria
already carry), and inactive values are included. A name that no longer resolves, such as an old
preset criterion, falls back to `orange`. The values the picker offers for adding show a small
circle in their colour to the left of their name; it is decorative, so each option is still named
by its text alone. Level options carry no circle.

### API and data contract

Existing endpoints are extended. No new routes.

| Endpoint (`backend/Web/Features/Catalogs/CatalogEndpoints.cs`)               | Change                                                                                                                                              |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/catalogs/{family}` (`ListCatalogFamily`)                           | Each `CatalogItemResponse` gains `color` (token string). Returned for every family.                                                                 |
| `POST /api/catalogs/{family}` (`CreateCatalogItem`)                          | `CreateCatalogItemRequest` gains optional `string? Color`. When omitted it defaults to `orange`.                                                    |
| `PUT /api/catalogs/{family}/{id}` (`UpdateCatalogItem`)                      | `UpdateCatalogItemRequest` gains optional `string? Color`. `null` keeps the current colour. The existing `Version` check applies (409 on mismatch). |
| `PUT /api/catalogs/{family}/order`, `PUT /api/catalogs/{family}/{id}/active` | Unchanged, but the responses include `color`.                                                                                                       |

Older clients that send no `color` keep working on both writes.

**Validation** (FluentValidation, after the existing permission guard):

| Code                          | When                                                                                           | Message                                     |
| ----------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `catalog.color.invalid`       | `color` is present and not one of the 9 tokens                                                 | «El color no es válido.»                    |
| `catalog.color.not_supported` | `color` is present for a family outside `skill`/`language`/`program`/`tag` and is not `orange` | «Esta familia de catálogo no admite color.» |

**Authorization:** unchanged. Reads need `catalogs.read`. Create and update need `catalogs.manage`,
checked in the endpoint before dispatch and again in the handler (`CatalogGuards.RequireManage`).
A colour change is audited as `catalog.updated`, like a rename. Catalog values are reference
data, not personal data.

**Persistence:**

- `CAT_CatalogItems` gains `"Color"` `varchar(20) NOT NULL DEFAULT 'orange'`, plus
  `CK_CAT_CatalogItems_Color` limiting the column to the 9 tokens. The constraint text is built
  from the domain list, like `FamilyCheckConstraint`.
- EF Core migration `AddCatalogItemColor` in `backend/Infrastructure/Persistence/Migrations/`.
  Existing rows take `orange`.
- No grant change. `ktl_runtime` already has table-level `SELECT, INSERT, UPDATE` on
  `CAT_CatalogItems` (migration `AddCatalogItems`), and still has no `DELETE`.

### Files to change

**Backend**

| Layer          | File                                                     | Change                                                                                                                                 |
| -------------- | -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Domain         | `Domain/Catalogs/CatalogColors.cs` (new)                 | Closed token list, `Default = "orange"`, `IsKnown(color)`, `Supports(family)` for the four colourable families                         |
| Domain         | `Domain/Catalogs/CatalogItem.cs`                         | `Color` property (default `orange`), optional constructor argument, `Recolor(color, updatedAtUtc)` (no-op when unchanged)              |
| Application    | `Application/Features/Catalogs/CatalogContract.cs`       | `Color` in `CatalogItemResponse`. New error codes and messages. `MustBeAKnownColor` / `MustBeSupportedBy(family)` validator extensions |
| Application    | `CreateCatalogItem.cs`, `UpdateCatalogItem.cs`           | `string? Color` on the command, validator rules, apply in the handler                                                                  |
| Infrastructure | `Persistence/Configurations/CatalogItemConfiguration.cs` | Column, max length, default, check constraint                                                                                          |
| Infrastructure | `Persistence/Migrations/<ts>_AddCatalogItemColor.cs`     | Generated with `dotnet ef migrations add AddCatalogItemColor …`                                                                        |
| Infrastructure | `Persistence/DatabaseInitializer.cs`                     | Seeds keep the default. Only touched if the constructor signature requires it                                                          |
| Web            | `Web/Features/Catalogs/CatalogEndpoints.cs`              | `Color` on both request records, passed to the commands                                                                                |

**Frontend** (`frontend/`)

| File                                                                                         | Change                                                                                                                                                                                                                                   |
| -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/app/features/catalogs/models/catalog.models.ts`                                         | `CatalogColor` union and `color: CatalogColor` on `CatalogItem`                                                                                                                                                                          |
| `src/app/features/catalogs/catalog-color.logic.ts` (new)                                     | `CATALOG_COLORS` (order of the grid), `DEFAULT_CATALOG_COLOR`, `isColorableFamily(family)`, `colorNameKey(color)`                                                                                                                        |
| `src/app/features/catalogs/services/catalog.api.ts`                                          | `color?` on `CatalogItemPayload`                                                                                                                                                                                                         |
| `src/app/features/catalogs/services/catalog.service.ts`                                      | `create`/`update` accept `color`. New `colorOf(family, name): CatalogColor` over active and inactive items, falling back to the default                                                                                                  |
| `src/app/features/catalogs/components/catalog-color-swatch.tsx` + `.css` (new)               | Read-only circle and trigger button                                                                                                                                                                                                      |
| `src/app/features/catalogs/components/catalog-color-dialog.tsx` + `.css` (new)               | Modal on `react-aria-components` (`ModalOverlay`/`Modal`/`Dialog` and a grid `ListBox`, already a dependency) with `shared/components/modal.css`. No new runtime dependency                                                              |
| `src/app/features/catalogs/pages/catalog-management-page.tsx`                                | «Color» column, create-form field, edit draft `editColor`/`newColor`                                                                                                                                                                     |
| `src/app/features/catalogs/components/catalog-value-picker.tsx` / `.logic.ts` / `.css`       | Optional prop `colorOf?: (item: PickerItem) => CatalogColor \| undefined`. The chip gets `data-catalog-color`                                                                                                                            |
| `src/app/features/candidates/components/candidate-relation-section.tsx`                      | Pass `colorOf` from `useCatalogs()` for `definition.valueFamily`                                                                                                                                                                         |
| `src/app/features/search/components/criteria-group.tsx`                                      | Pass `colorOf` for the group's value family                                                                                                                                                                                              |
| `src/app/features/search/components/search-criteria.logic.ts`, `search-criteria-summary.tsx` | `SummaryGroup.values` become `{ text, color? }`. Catalog groups carry the colour, and the chip gets `data-catalog-color`                                                                                                                 |
| `src/styles.css`                                                                             | One `[data-catalog-color='<token>']` block per token setting `--catalog-chip-bg/-border/-fg`. `.catalog-picker-chip`, `.filters-summary .chip` and the swatch read those variables, with the orange tokens as fallback. No inline styles |
| `src/assets/i18n/es.json` (+ `en.json` optional)                                             | Keys below                                                                                                                                                                                                                               |

Read the catalogs through `useCatalogs()` during render, never through `useServices()`. Keep
`name=` and the existing `data-testid`s. New test ids: `catalog-color` (read-only cell),
`catalog-color-trigger` (row in edit), `new-catalog-color-trigger` (create form),
`catalog-color-dialog`, `catalog-color-option` (with `data-value=<token>`). The radio group uses
`name="color"`.

**i18n keys (Spanish)**

```json
"catalogs.management.column.color": "Color",
"catalogs.management.form.color": "Color",
"catalogs.color.currentLabel": "Color: {{color}}",
"catalogs.color.changeLabel": "Cambiar el color de {{name}} (actual: {{color}})",
"catalogs.color.changeNewLabel": "Elegir el color del nuevo valor (actual: {{color}})",
"catalogs.color.dialog.title": "Elegir color",
"catalogs.color.dialog.description": "El color se aplica a los chips de este valor en toda la aplicación.",
"catalogs.color.dialog.cancel": "Cancelar",
"catalogs.color.name.orange": "Naranja",
"catalogs.color.name.yellow": "Amarillo",
"catalogs.color.name.green": "Verde",
"catalogs.color.name.teal": "Turquesa",
"catalogs.color.name.blue": "Azul",
"catalogs.color.name.indigo": "Índigo",
"catalogs.color.name.violet": "Violeta",
"catalogs.color.name.pink": "Rosa",
"catalogs.color.name.grey": "Gris"
```

### Acceptance criteria

1. **Default for existing data.** Given the migration has run, every existing catalog item has
   colour `orange`, and every chip looks exactly as before.
2. **Read-only circle.** Given the family «Habilidades» is selected, each row shows a circle in
   the item's colour. Hovering it shows the colour name, and its accessible name is «Color: <name>».
3. **Non-colourable families.** Given a level family, «Tipos de formación», «Estados de
   formación» or «Sectores» is selected, there is no «Color» column, no colour field in the
   create form and no colour dialog.
4. **Pick while editing.** Given a skill row is being edited, when the admin opens the circle,
   chooses «Azul» and presses «Guardar», then the API stores `blue` and the row shows a blue circle
   titled «Azul».
5. **Cancel discards.** Given a colour was chosen in an edited row, when the admin presses
   «Cancelar» or Escape, the row shows its previous colour and no request is sent.
6. **Create with colour.** When the admin adds a tag with colour «Rosa», then the new tag is
   created with `pink`, and the form's circle returns to «Naranja».
7. **Dialog accessibility.** The dialog traps focus, offers 9 named swatches with the current one
   checked, moves with the arrow keys, closes on Escape, and returns focus to the circle that
   opened it.
8. **Chips everywhere.** Given the skill «Análisis» is `blue`, its chip is blue in the candidate
   «Competencias» panel (read and edit), the search criteria form, the position requirements
   form, the collapsed search summary, the criteria dialog, the preset list and the position
   detail. Non-catalog summary chips stay orange.
9. **Inactive value.** Given a `green` tag is deactivated, candidates that still carry it show it
   green.
10. **Concurrency.** Given another admin changed the item since it was loaded, saving a new colour
    returns 409 `catalog.concurrency.conflict`, and the error toast is shown.
11. **Validation.** `PUT`/`POST` with `color: "magenta"` returns 400 `catalog.color.invalid`.
    `color: "blue"` on `language_level` returns 400 `catalog.color.not_supported`. Omitting `color`
    on update keeps the stored colour.
12. **Fail closed.** Unauthenticated callers, and callers with `catalogs.read` but not
    `catalogs.manage`, get 403 when sending a `color` (the catalog endpoints answer an
    unauthenticated caller with 403). The check runs before validation, so an
    invalid colour from an unauthorized caller still returns 403, not 400. Nothing is written or
    audited.
13. **Audit.** A successful colour change records one `catalog.updated` event with the acting actor.
14. **Contrast.** Each of the 9 tokens reaches ≥ 4.5:1 text/background contrast, recorded in
    `docs/CORPORATE_IDENTITY_Kepler.md`.
15. **Dropdown circles.** In the value picker's add list, each value shows a circle in its colour
    to the left of its name, and the option is still named by the value alone.
16. **«Nuevo».** On opening Catálogos the add form is hidden. «Nuevo», on the family selector's
    line and to its right, shows it with focus on the name. «Cancelar» hides and empties it. A
    successful «Añadir» hides it.
17. **Inert rows.** Clicking a catalog row outside its controls starts no edit and navigates
    nowhere. The pencil «Editar <value>» starts the edit, by pointer or keyboard.
18. **Icon actions.** «Desactivar <value>» and «Activar <value>» are icon buttons with tooltips.
    Deactivation asks for confirmation and activation does not. While a row is edited, «Nuevo» and
    the other rows' actions are disabled.

### Test coverage

| Suite                                                                                            | Coverage                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `backend/Tests/UnitTests/Domain/` (new `CatalogItemColorTests`)                                  | Default colour, `Recolor` changes `UpdatedAtUtc` only on change, `CatalogColors.Supports`                                                                                                                                                                         |
| `backend/Tests/UnitTests/Features/CatalogHandlerTests.cs`                                        | Create with/without colour, update keeps colour on `null`, invalid and unsupported colour codes, manage guard                                                                                                                                                     |
| `backend/Tests/IntegrationTests/CatalogApiTests.cs`                                              | Round trip of `color` on POST/PUT/GET, 400 codes, 409 on stale `Version`, 403 before validation, audit row written                                                                                                                                                |
| `backend/Tests/IntegrationTests/` (schema)                                                       | Migration sets `orange` on existing rows. `CK_CAT_CatalogItems_Color` rejects an unknown value. `ktl_runtime` can update `Color` and still cannot `DELETE`                                                                                                        |
| `frontend/tests/unit/catalog.service.spec.ts`                                                    | `create`/`update` send `color`. `colorOf` resolves inactive items and falls back to `orange`                                                                                                                                                                      |
| `frontend/tests/unit/catalog-management-colors.spec.tsx` (+ new `catalog-color-dialog.spec.tsx`) | Column only for colourable families, tooltip and accessible name, dialog keyboard flow, cancel discards, create form resets                                                                                                                                       |
| `frontend/tests/unit/catalog-management-rows.spec.tsx`                                           | Inert row, pencil by pointer and keyboard, icon toggle with confirmation, locks, «Nuevo» (hidden by default, focus, cancel, close after add)                                                                                                                      |
| `frontend/tests/unit/catalog-value-picker.spec.tsx`                                              | `data-catalog-color` set from `colorOf`, absent without it; option circles in the add list                                                                                                                                                                        |
| `frontend/tests/unit/` search summary spec                                                       | Catalog groups carry colour, non-catalog groups do not                                                                                                                                                                                                            |
| `frontend/tests/e2e/catalogs-crud.spec.ts`                                                       | Admin sets a colour on a `Date.now()`-marked tag, and the candidate «Competencias» chip shows it (assert on `data-catalog-color`, not on Spanish text). Values are created through «Nuevo» and edited or toggled through `catalog-edit` / `catalog-toggle-active` |

### Documentation

- `openspec/specs/business-catalogs/spec.md`: item shape gains a colour for the four chip
  families. New requirement «Catalog value colour» (closed palette, default, validation codes,
  non-colourable families). The «Tags family is administered like any other» scenario still holds,
  since tags get exactly the same field as skills, languages and programs.
- `openspec/specs/catalog-value-picker/spec.md`: chips take their value's colour, and the add list
  shows a colour circle beside each value.
- `openspec/specs/data-tables/spec.md`: «Catalog rows open their inline editor» (KTL-31) is replaced
  by «Catalog values are edited from their row actions».
- `openspec/specs/saved-search-presets/spec.md`: catalog chips in the read-only criteria summary
  are coloured.
- `docs/CORPORATE_IDENTITY_Kepler.md`: palette tokens and contrast ratios.
- `docs/ktl-41/`: release note with the API contract change and the migration.
- `README.md`: one line in the «Catálogos» section, and the «Tablas coherentes KTL-31» paragraph
  for the new catalog actions (in Spanish).

### Non-functional requirements

- **Security:** authorization fails closed before validation. No new permission and no new grant.
  The colour is reference data, never personal data.
- **Performance:** `colorOf` is an in-memory lookup over the already loaded catalog state. No extra
  requests. Memoise per family map where chips render in lists.
- **Accessibility:** colour is never the only cue. Chips keep their text, the dialog shows names
  and a check mark, swatch controls have accessible names, and the dialog is keyboard-operable with
  focus return. Focus outlines stay `--fj-orange-bright`.
- **Responsive:** the dialog grid fits at 360px width (3 columns of swatch + name, wrapping the
  name if needed). The «Color» column stays narrow. No new breakpoint (the shell's single 768px
  breakpoint stays in `primary-nav.css`).

### Out of scope

- Colours for level, education and sector families, and turning their text into chips.
- Custom colours or hex input.
- Demo seed (`npm run seed:demo`) assigning colours.
