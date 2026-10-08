## MODIFIED Requirements

### Requirement: Catalog families and item shape

The system SHALL organize catalog values into the ten business families used by candidate
records and search: languages, programs, skills, language levels, program levels, skill
levels, education types, education statuses, sectors, and tags. Each catalog item SHALL carry a
stable identifier, a code, a Spanish name, an optional English name, a position within its
family, an active flag, and a colour from the closed catalog palette.

The tags family SHALL have the same item shape as every other family. A tag SHALL NOT carry a
level, a status, or any attribute the other families do not have.

#### Scenario: Family is listed

- **WHEN** an authorized user requests the values of a family
- **THEN** the API returns that family's items in their configured order, each with its
  identifier, code, Spanish name, optional English name, position, active flag, and colour

#### Scenario: Unknown family is requested

- **WHEN** a caller requests a family that is not one of the ten business families
- **THEN** the API rejects the request with a stable validation code and returns no items

#### Scenario: Tags family is administered like any other

- **WHEN** an authorized administrator creates, renames, recolours, reorders, deactivates or
  reactivates a value of the tags family
- **THEN** the operation behaves exactly as it does for the skills, languages and programs
  families, through the same operations, with no additional field to supply

## ADDED Requirements

### Requirement: Catalog value colour

Every catalog item SHALL hold exactly one colour from a closed palette of nine pastel colours,
identified by the stable tokens `orange`, `yellow`, `green`, `teal`, `blue`, `indigo`, `violet`,
`pink` and `grey`. `orange` SHALL be the default and SHALL reproduce the chip appearance that
existed before colours were introduced. Items that existed before colours were introduced SHALL
hold `orange`.

Only the skills, languages, programs and tags families SHALL be colourable. Items of the level
families, education types, education statuses and sectors SHALL always hold `orange`.

Creating a value SHALL accept an optional colour. When the colour is omitted, the value is created
with `orange`. Updating a value SHALL accept an optional colour. When it is omitted, the stored
colour is kept. A colour change SHALL be subject to the same version check, authorization and audit
as any other update. Several values MAY share a colour, and deactivating a value SHALL NOT change
its colour.

The API SHALL reject a colour that is not one of the nine tokens with the stable code
`catalog.color.invalid`, and a colour other than `orange` for a non-colourable family with the
stable code `catalog.color.not_supported`. A rejected request SHALL store nothing.

Each palette colour SHALL pair a background, border and text colour of the same hue whose text
reaches a contrast ratio of at least 4.5:1 against its background at chip text size.

#### Scenario: Existing values default to orange

- **WHEN** values that existed before colours were introduced are listed
- **THEN** each one has the colour `orange`

#### Scenario: Value is created without a colour

- **WHEN** an authorized administrator creates a skill without supplying a colour
- **THEN** the value is created with the colour `orange`

#### Scenario: Value is created with a colour

- **WHEN** an authorized administrator creates a tag with the colour `pink`
- **THEN** the value is created with the colour `pink`

#### Scenario: Colour is changed

- **WHEN** an authorized administrator updates a language with the colour `blue` and the current
  version
- **THEN** the language's colour becomes `blue` and a `catalog.updated` audit event is recorded

#### Scenario: Update without a colour keeps it

- **WHEN** an authorized administrator renames a `green` program without supplying a colour
- **THEN** the program keeps the colour `green`

#### Scenario: Unknown colour is rejected

- **WHEN** a create or update request supplies the colour `magenta`
- **THEN** the API rejects it with the code `catalog.color.invalid` and stores nothing

#### Scenario: Colour on a non-colourable family is rejected

- **WHEN** a create or update request for a language level supplies the colour `blue`
- **THEN** the API rejects it with the code `catalog.color.not_supported` and stores nothing

#### Scenario: Colour change on a stale version

- **WHEN** an administrator changes the colour of a value that has changed since it was read
- **THEN** the API rejects the change as a concurrency conflict and the stored colour is unchanged

#### Scenario: Unauthorized colour change

- **WHEN** a caller without the catalog management capability submits a colour, valid or not
- **THEN** the request is denied before validation and no change is stored or audited

#### Scenario: Deactivated value keeps its colour

- **WHEN** a `green` tag is deactivated
- **THEN** it still has the colour `green`

### Requirement: Catalog colour is chosen in administration

For a colourable family, the catalog administration screen SHALL show each value's colour as a
filled circle whose tooltip is the colour's Spanish name and whose accessible name states the
colour. For a non-colourable family the screen SHALL show no colour column, no colour field and no
colour dialog.

While a value is edited inline, and in the form that adds a value, the circle SHALL be a button
that opens a dialog titled «Elegir color». The add form's circle starts at Naranja. The dialog
SHALL offer the nine palette colours, each with its visible Spanish name (Naranja, Amarillo, Verde,
Turquesa, Azul, Índigo, Violeta, Rosa, Gris), and SHALL mark the current colour with a
non-colour cue. The colours SHALL be a single-choice group in which the arrow keys move between
colours without choosing one, and Enter, Space or a click chooses. Choosing a different colour
SHALL close the dialog, return focus to the circle and change only the pending edit; choosing the
current colour SHALL close the dialog unchanged. The
colour SHALL be stored only when the row is saved or the value is added. Escape or «Cancelar» in the
dialog SHALL close it without changing the pending colour, and cancelling the row edit SHALL discard
a chosen colour. After a value is added, or when the family changes, the add form's colour SHALL
return to Naranja.

#### Scenario: Colour column for a colourable family

- **WHEN** an administrator views the skills family
- **THEN** each row shows a circle in the value's colour, with the colour's Spanish name as its
  tooltip and accessible name

#### Scenario: No colour for a non-colourable family

- **WHEN** an administrator views a level family, education types, education statuses or sectors
- **THEN** no colour column, colour field or colour dialog is offered

#### Scenario: Colour is chosen while editing

- **WHEN** an administrator edits a skill, chooses «Azul» in the colour dialog and saves the row
- **THEN** the skill is stored with the colour `blue` and its row shows a blue circle titled «Azul»

#### Scenario: Cancelled edit discards the colour

- **WHEN** an administrator chooses a colour for an edited row and then cancels the edit
- **THEN** the row shows its previous colour and no change is submitted

#### Scenario: Colour is chosen when adding

- **WHEN** an administrator adds a tag after choosing «Rosa»
- **THEN** the tag is created with the colour `pink` and the add form's colour returns to Naranja

#### Scenario: Dialog is operated by keyboard

- **WHEN** an administrator opens the colour dialog, moves with the arrow keys and presses Escape
- **THEN** the dialog closes without changing the pending colour and focus returns to the circle
