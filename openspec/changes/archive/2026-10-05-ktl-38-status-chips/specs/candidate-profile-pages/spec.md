## MODIFIED Requirements

### Requirement: Availability block on the candidate page

The candidate page SHALL show the candidate's availability in a panel headed «Disponibilidad» in the first summary row beside Datos principales, never in the page header:

- **Line:** the value as a toned chip — neutral «Sin comprobar», green «Disponible» or red «No disponible» — followed, for «No disponible» with an until date, by «hasta {date}» as text, and by the «(vencido)» marker when that date has passed.
- **When known, check metadata:** «Comprobado el {date} por {name} · {time elapsed}». The time elapsed is in the active language («hoy», «hace 7 meses»). Dates are calendar days shown in the stored day in every time zone.

The value SHALL NOT repeat «Disponibilidad» beneath the panel heading. When there is room, the check metadata SHALL sit to the right of the value on the same row; it SHALL wrap without horizontal scrolling when there is not. The check metadata SHALL be regular weight. The summary row, hints and actions SHALL have readable vertical separation.

For an actor holding `candidates.update` on an active candidate, the block SHALL offer:

- «Sigue igual», only when the value is known and any until date is today or later. It records the stored value and until date again with today's date.
- «Cambiar…» («Registrar comprobación» when the value is `unknown`). It opens an inline form with:
  - a labelled radio group for the value;
  - a «Hasta (opcional)» date, shown only for «No disponible»;
  - a «Comprobado el» date defaulting to today, hidden for «Sin comprobar»;
  - «Registrar» and «Cancelar». The date field and these actions SHALL share one row when the panel has enough width and SHALL wrap without horizontal scrolling when it does not.
- Right after each successful check, an inline «Deshacer». It records the previous value, until date and check date again. It stays until another check is made or the page is left, and is never removed on a timer.

When the until date has passed, the line SHALL be marked «(vencido)», a hint SHALL ask for a new check, and «Sigue igual» SHALL NOT be offered.

Without `candidates.update`, or on a logically removed candidate, the block SHALL be read-only.

The block SHALL act immediately. It SHALL NOT take part in per-panel edit mode, SHALL NOT count towards unsaved-change protection, and SHALL stay usable while a panel is being edited. After a check, the page SHALL adopt the returned candidate and version, so a later save of another panel is not refused because of that check. The result of each check and undo SHALL be announced through a polite live region, and focus SHALL return to «Cambiar…» after saving or cancelling the form.

#### Scenario: Unchecked candidate

- **WHEN** an editor opens a candidate whose availability is `unknown`
- **THEN** the block shows a neutral «Sin comprobar» chip without a repeated «Disponibilidad» label and offers «Registrar comprobación» but not «Sigue igual»

#### Scenario: Reconfirming

- **WHEN** an editor activates «Sigue igual» on a candidate checked `available` on 2026-03-12
- **THEN** the block shows today's date, the editor's name and «hoy», the value is unchanged, and «Deshacer» is offered

#### Scenario: Check metadata beside the value

- **WHEN** a reader opens a checked candidate on a wide sections column
- **THEN** the availability chip is on the left and regular-weight «Comprobado el …» metadata is to its right; on a narrow column they remain readable without horizontal scrolling

#### Scenario: Undoing a reconfirm

- **WHEN** the editor then activates «Deshacer»
- **THEN** the block again shows 2026-03-12 and the previous value, and the confirmation is announced

#### Scenario: Lapsed until date

- **WHEN** a reader opens a candidate checked «No disponible» until a date that has passed
- **THEN** the line shows the red «No disponible» chip, «hasta» that date and the «(vencido)» marker, the hint asks for a new check, and «Sigue igual» is not offered

#### Scenario: Changing the value

- **WHEN** an editor opens «Cambiar…», chooses «No disponible», enters an until date and saves
- **THEN** the block shows a red «No disponible» chip followed by «hasta» that date with today's check, and focus returns to «Cambiar…»

#### Scenario: Registering in the inline form

- **WHEN** an editor selects «Disponible» in the inline form on a wide summary panel
- **THEN** «Comprobado el», «Registrar» and «Cancelar» share one row; on a narrow panel they wrap and remain reachable without horizontal scrolling

#### Scenario: Check while another panel is edited

- **WHEN** Datos principales is in edit mode with unsaved changes and the editor activates «Sigue igual», then saves Datos principales
- **THEN** both writes succeed, and no unsaved-change prompt or concurrency conflict appears

#### Scenario: Reader sees no availability actions

- **WHEN** an actor without `candidates.update` opens a candidate
- **THEN** the availability lines are shown and no «Sigue igual», «Cambiar…» or «Deshacer» is offered
