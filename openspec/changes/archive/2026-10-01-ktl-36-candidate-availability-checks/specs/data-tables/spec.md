## ADDED Requirements

### Requirement: Candidate tables show availability

The candidate list, the advanced search results and «Candidatos que encajan» SHALL show a
«Disponibilidad» column where a candidate status used to be:

- for a known check, its value and the time elapsed since the check («No disponible · hace 6
  meses»);
- otherwise «Sin comprobar».

No candidate table SHALL show a candidate status. «Candidatos de la posición» SHALL keep showing the
stage instead. In the candidate list, the column SHALL be sortable by check date, and the filter bar
SHALL offer a single-value «Disponibilidad» filter, kept in the page URL like the other list
filters, with a removable chip when set. The CSV export of search results SHALL carry
`disponibilidad` (the Spanish value label) and `comprobado_el` (`yyyy-MM-dd`, blank when unchecked)
instead of `estado`.

#### Scenario: Availability column

- **WHEN** a reader views the candidate list with one candidate checked `unavailable` six months ago
  and one never checked
- **THEN** the first row reads «No disponible · hace 6 meses», the second reads «Sin comprobar», and
  no column shows a candidate status

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
