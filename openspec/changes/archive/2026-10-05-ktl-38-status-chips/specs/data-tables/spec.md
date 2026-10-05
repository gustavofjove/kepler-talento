## ADDED Requirements

### Requirement: Status values are shown as toned chips

Status values SHALL be shown as compact chips with a pale tinted background, dark text and the
value's label, using one of three semantic tones:

- **success** (green) for a good, active or available state;
- **danger** (red) for a blocking or unavailable state;
- **neutral** (grey) for an unknown, closed or inactive state.

Each tone's text SHALL reach a contrast ratio of at least 4.5:1 on its own background, and the
neutral background SHALL remain distinguishable from a hovered table row. A chip SHALL NOT have a
solid fill, a border, a hover change or a pointer cursor, and SHALL always carry its text label, so
colour is never the only signal.

The tones SHALL be applied as follows:

| Value                                  | Tone    |
| -------------------------------------- | ------- |
| Candidate availability «Sin comprobar» | neutral |
| Candidate availability «Disponible»    | success |
| Candidate availability «No disponible» | danger  |
| Position status «Abierta»              | success |
| Position status «Cerrada»              | neutral |
| Export history «Completada»            | success |

Position status SHALL use these tones in the positions list, on the position page and in the
candidate's «Posiciones» panel. Chips for other values (pipeline stages, inactive markers, import
and document states, roles, catalog flags) keep their current appearance.

#### Scenario: Availability values are told apart by tone

- **WHEN** a reader views a candidate table containing one unchecked, one available and one
  unavailable candidate
- **THEN** «Sin comprobar» is a neutral chip, «Disponible» a green chip and «No disponible» a red
  chip, each with its text label

#### Scenario: Position status tones

- **WHEN** a reader views the positions list with one open and one closed position, then opens each
  position and a linked candidate's «Posiciones» panel
- **THEN** «Abierta» is a green chip and «Cerrada» a neutral chip in all three places

#### Scenario: Export history status

- **WHEN** a reader opens the export history on the advanced search page after an export
- **THEN** the batch's «Completada» is drawn as a success chip

#### Scenario: Chips carry no button cues

- **WHEN** a pointer hovers any toned chip
- **THEN** its background, text and cursor do not change, and it has no border or solid fill

#### Scenario: Tones meet contrast

- **WHEN** the text and background colours of each tone are measured
- **THEN** every pair reaches at least 4.5:1

## MODIFIED Requirements

### Requirement: Candidate tables show availability

The candidate list, the advanced search results and «Candidatos que encajan» SHALL show a
«Disponibilidad» column where a candidate status used to be:

- for a known check, the value as a toned chip followed by the time elapsed since the check as
  muted text («No disponible» chip, then «hace 6 meses»);
- otherwise the «Sin comprobar» chip alone.

The chip and the elapsed time SHALL be separately identifiable, so each can be checked on its own.

No candidate table SHALL show a candidate status. «Candidatos de la posición» SHALL keep showing the
stage instead. In the candidate list, the column SHALL be sortable by check date, and the filter bar
SHALL offer a single-value «Disponibilidad» filter, kept in the page URL like the other list
filters, with a removable chip when set. The filter options, the filter chip and the CSV export
SHALL keep their plain-text values. The CSV export of search results SHALL carry
`disponibilidad` (the Spanish value label) and `comprobado_el` (`yyyy-MM-dd`, blank when unchecked)
instead of `estado`.

#### Scenario: Availability column

- **WHEN** a reader views the candidate list with one candidate checked `unavailable` six months ago
  and one never checked
- **THEN** the first row shows a red «No disponible» chip followed by muted «hace 6 meses», the
  second shows a neutral «Sin comprobar» chip with no elapsed time, and no column shows a candidate
  status

#### Scenario: List is filtered by availability

- **WHEN** a reader selects «Disponible» in the list's «Disponibilidad» filter
- **THEN** only available candidates are listed, the URL keeps the filter, and a chip removes it

#### Scenario: List is sorted by check date

- **WHEN** a reader sorts the list by «Disponibilidad»
- **THEN** rows are ordered by check date with unchecked candidates last, and the column header
  reports the sort direction

#### Scenario: Export carries availability

- **WHEN** a reader exports search results
- **THEN** the file has `disponibilidad` and `comprobado_el` columns and no `estado` column
