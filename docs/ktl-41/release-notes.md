# KTL-41 release notes: catalog value colours

Skills, languages, programs and tags can now be given one of nine pastel colours, so their chips
can be told apart at a glance. Nothing changes visually until an administrator picks a colour:
every existing value is orange, the chip colour used until now.

## What users see

- **«Catálogos»** (`catalogs.manage`): for the four colourable families, a «Color» column shows
  each value's colour as a circle; its tooltip is the colour's name. While a row is edited, and in
  the add form, the circle opens «Elegir color» (on a narrow screen each name wraps under its
  circle): Naranja, Amarillo, Verde, Turquesa, Azul, Índigo,
  Violeta, Rosa and Gris, with the current one checked. Arrow keys move between colours, Enter,
  Space or a click chooses, Escape or «Cancelar» closes unchanged. The choice is stored with the
  row's «Guardar» or the form's «Añadir»; «Cancelar» on the row discards it. Level families,
  «Tipos de formación», «Estados de formación» and «Sectores» show no colour.
- **Chips** take their value's colour in the candidate «Competencias» panel, the search criteria,
  the preset and position criteria editors, and the read-only criteria summary (search page,
  criteria dialog, preset list, position page). Text, availability, «Comprobado desde» and CV
  chips in the summary stay orange.
- **The add list** of the value picker shows a small circle in each value's colour to the left of
  its name.
- Deactivated values keep their colour. A saved criterion whose value no longer exists (for
  example, an old preset after a rename) is drawn orange.
- Level and detail text inside a catalog chip now uses `--fg-2` instead of `--fg-3`, so it reaches
  4.5:1 contrast on every colour, orange included.

## Catalog administration actions

Requested alongside the colours, these change how «Catálogos» is operated:

- **«Nuevo».** The add form is hidden until «Nuevo», to the right of the family selector, opens
  it. The form puts the name in one half of its line and, in the other, the colour circle followed
  by the code. «Cancelar» closes it; a successful «Añadir» adds the value and closes it.
- **Row actions.** A click on a row no longer opens its inline edit (KTL-31's behaviour), and the
  value's name is plain text. «Acciones» now holds, as icons: the pencil «Editar», the reorder
  arrows, and «Desactivar» (or «Activar» for an inactive value). Each has a tooltip and an
  accessible name with the value, such as «Desactivar Inglés». Deactivation still asks for
  confirmation.
- While a row is edited, «Nuevo» and the other rows' actions are disabled; while the add form is
  open, the row edit buttons are.

Test ids for automation: `catalog-new`, `catalog-create-form`, `catalog-create-cancel`,
`catalog-edit` (now the pencil), `catalog-toggle-active`.

## API

Additive; clients that send no colour keep working.

| Endpoint                           | Change                                                                                           |
| ---------------------------------- | ------------------------------------------------------------------------------------------------ |
| `GET /api/catalogs/{family}`       | Every item carries `color`, one of the nine tokens below.                                        |
| `POST /api/catalogs/{family}`      | Optional `color`. Omitted: `orange`.                                                             |
| `PUT /api/catalogs/{family}/{id}`  | Optional `color`. Omitted: the stored colour is kept. `version` still applies (409 on mismatch). |
| `PUT …/order`, `PUT …/{id}/active` | Unchanged requests; responses include `color`.                                                   |

Tokens: `orange` (default), `yellow`, `green`, `teal`, `blue`, `indigo`, `violet`, `pink`, `grey`.

| Code                          | Status | When                                                                                |
| ----------------------------- | ------ | ----------------------------------------------------------------------------------- |
| `catalog.color.invalid`       | 400    | `color` is not one of the tokens (case-sensitive).                                  |
| `catalog.color.not_supported` | 400    | `color` other than `orange` on a family other than skill, language, program or tag. |

Authorization is unchanged and checked before validation: a caller without `catalogs.manage`
(including an unauthenticated one) gets 403 for any colour, valid or not, and nothing is written
or audited. A colour change is audited as `catalog.updated`.

## Database

Migration `AddCatalogItemColor` adds `"Color" varchar(20) NOT NULL DEFAULT 'orange'` to
`CAT_CatalogItems`, with `CK_CAT_CatalogItems_Color` limiting it to the nine tokens. Existing rows
take `orange` from the default. Which families may hold another colour is enforced by the API,
not the database.

No grant changes: `ktl_runtime` already holds table-level `SELECT, INSERT, UPDATE` on
`CAT_CatalogItems` and still has no `DELETE`.

**Rollout.** Run the migrator before the new API, as always. An API still on the previous build
writes without the column and gets the default, so both builds work during the switch.

**Rollback.** The migration's `Down` drops the constraint and the column. Only the chosen colours
are lost; they are reference data, not personal data.

## Palette

Tokens, hex values and measured contrast are recorded in
[`docs/CORPORATE_IDENTITY_Kepler.md`](../CORPORATE_IDENTITY_Kepler.md) under «Catalog value
colours». Every colour reaches at least 5.74:1 text-on-background contrast. The SPA's palette is
kept identical to the API's by `tests/unit/catalog-color-palette.spec.ts`.
