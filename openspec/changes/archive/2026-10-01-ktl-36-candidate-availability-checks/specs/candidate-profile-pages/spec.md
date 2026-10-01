## ADDED Requirements

### Requirement: Availability block on the candidate page

The candidate page SHALL show the candidate's availability under the contact line in the page
header:

- **Line:** «Sin comprobar», «Disponible», «No disponible» or «No disponible hasta {date}».
- **When known, a second line:** «Comprobado el {date} por {name} · {time elapsed}». The time
  elapsed is in the active language («hoy», «hace 7 meses»). Dates are calendar days shown in the
  stored day in every time zone.

For an actor holding `candidates.update` on an active candidate, the block SHALL offer:

- «Sigue igual», only when the value is known and any until date is today or later. It records the
  stored value and until date again with today's date.
- «Cambiar…» («Registrar comprobación» when the value is `unknown`). It opens an inline form with:
  - a labelled radio group for the value;
  - a «Hasta (opcional)» date, shown only for «No disponible»;
  - a «Comprobado el» date defaulting to today, hidden for «Sin comprobar»;
  - «Guardar» and «Cancelar».
- Right after each successful check, an inline «Deshacer». It records the previous value, until date
  and check date again. It stays until another check is made or the page is left, and is never
  removed on a timer.

When the until date has passed, the line SHALL be marked «(vencido)», a hint SHALL ask for a new
check, and «Sigue igual» SHALL NOT be offered.

Without `candidates.update`, or on a logically removed candidate, the block SHALL be read-only.

The block SHALL act immediately. It SHALL NOT take part in per-panel edit mode, SHALL NOT count
towards unsaved-change protection, and SHALL stay usable while a panel is being edited. After a
check, the page SHALL adopt the returned candidate and version, so a later save of another panel is
not refused because of that check. The result of each check and undo SHALL be announced through a
polite live region, and focus SHALL return to «Cambiar…» after saving or cancelling the form.

#### Scenario: Unchecked candidate

- **WHEN** an editor opens a candidate whose availability is `unknown`
- **THEN** the block reads «Sin comprobar» and offers «Registrar comprobación» but not «Sigue igual»

#### Scenario: Reconfirming

- **WHEN** an editor activates «Sigue igual» on a candidate checked `available` on 2026-03-12
- **THEN** the block shows today's date, the editor's name and «hoy», the value is unchanged, and
  «Deshacer» is offered

#### Scenario: Undoing a reconfirm

- **WHEN** the editor then activates «Deshacer»
- **THEN** the block again shows 2026-03-12 and the previous value, and the confirmation is announced

#### Scenario: Lapsed until date

- **WHEN** a reader opens a candidate checked «No disponible» until a date that has passed
- **THEN** the line is marked «(vencido)», the hint asks for a new check, and «Sigue igual» is not
  offered

#### Scenario: Changing the value

- **WHEN** an editor opens «Cambiar…», chooses «No disponible», enters an until date and saves
- **THEN** the block shows «No disponible hasta» that date with today's check, and focus returns to
  «Cambiar…»

#### Scenario: Check while another panel is edited

- **WHEN** Datos principales is in edit mode with unsaved changes and the editor activates
  «Sigue igual», then saves Datos principales
- **THEN** both writes succeed, and no unsaved-change prompt or concurrency conflict appears

#### Scenario: Reader sees no availability actions

- **WHEN** an actor without `candidates.update` opens a candidate
- **THEN** the availability lines are shown and no «Sigue igual», «Cambiar…» or «Deshacer» is
  offered

### Requirement: Pipeline badges derived from positions

For an actor holding `positions.read`, the candidate page SHALL show badges beside the candidate's
name, derived from the candidate's position links and never stored:

- «En proceso» when at least one link on an open position is at `new`, `shortlisted` or
  `interview`;
- «Contratado» when at least one link, on any position, is at `hired`.

Both MAY show at once. The badges SHALL be text, not colour alone. They SHALL be derived from the
same link list the «Posiciones» panel loads, without a second request, and SHALL follow stage
changes made in that panel without a reload. Without `positions.read`, no badge SHALL be shown and
no position request SHALL be sent.

#### Scenario: Candidate in process and hired before

- **WHEN** a reader with `positions.read` opens a candidate linked at `interview` on an open position
  and at `hired` on a closed one
- **THEN** both «En proceso» and «Contratado» are shown

#### Scenario: Stage change updates the badges

- **WHEN** a manager changes that open position's link to `rejected` in «Posiciones»
- **THEN** «En proceso» disappears without a reload and «Contratado» remains

#### Scenario: Candidate on a closed position only

- **WHEN** a candidate's only link is at `shortlisted` on a closed position
- **THEN** no badge is shown

#### Scenario: Reader without position permission

- **WHEN** an actor without `positions.read` opens a candidate
- **THEN** no badge is shown and no position request is sent

## MODIFIED Requirements

### Requirement: Candidate values are shown in readable form

The candidate page SHALL show stored values in the active language's readable form, not in their
storage format:

- Auditoría's creation and update times SHALL show a date and a time to the minute, in the viewer's
  time zone (for example «30 sept 2026, 11:49»), never an ISO timestamp.
- Datos principales' reception and review dates are calendar days. They SHALL show a date without a
  time (for example «1 sept 2026»), and SHALL show the stored day in every time zone. A stored value
  that is not a valid calendar day SHALL be shown as stored, and an empty one as «Pendiente».
- The location SHALL show as «Location (Province)». When only one of the two is stored, it SHALL
  show that one alone, with no empty parentheses.
- Datos principales SHALL NOT show a candidate status or a free-text availability. Availability is
  shown only in the availability block, and the core record form SHALL NOT edit it.

These are display rules only: the stored values are unchanged.

#### Scenario: Audit times are readable

- **WHEN** a reader opens a candidate created at 2026-09-30T09:49:00Z from Spain in summer time
- **THEN** Auditoría shows the creation time as «30 sept 2026, 11:49»

#### Scenario: Calendar day in a western time zone

- **WHEN** a candidate received on 2026-09-01 is viewed from a time zone behind UTC
- **THEN** the reception date shows «1 sept 2026», not the day before

#### Scenario: Location with and without province

- **WHEN** a candidate has location Alcobendas and province Madrid, and another has only the
  province Madrid
- **THEN** the first shows «Alcobendas (Madrid)» and the second shows «Madrid»

#### Scenario: Core record has no status or availability

- **WHEN** an editor views Datos principales and then opens its form
- **THEN** neither shows a status or an availability field, and the create page does not ask for
  one either
