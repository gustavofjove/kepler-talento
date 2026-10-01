## ADDED Requirements

### Requirement: Legacy status and availability are kept as note text

The export contract SHALL keep the candidate `Status` and `Availability` columns, and validation
SHALL keep rejecting a status outside the documented codes. The load SHALL NOT store either as a
candidate field. Every loaded candidate SHALL have an `unknown` availability check. Instead, the
load SHALL compose the candidate's notes from the row's notes followed by:

- «Estado en Access: {Spanish label}.» when the legacy status is not `new`;
- «Disponibilidad en Access: {text}» when the legacy availability is not blank.

The notes SHALL be composed from the row on every run, so a re-run produces the same notes and does
not repeat the lines. The reconciliation report and migration output SHALL NOT contain these
values, as for any other notes.

#### Scenario: Legacy candidate with status and availability

- **WHEN** a legacy row with notes «Perfil senior», status `hired` and availability «Incorporación
  en enero» is loaded
- **THEN** the candidate is `unknown`, and its notes are «Perfil senior» followed by «Estado en
  Access: Contratado.» and «Disponibilidad en Access: Incorporación en enero»

#### Scenario: Legacy candidate with nothing to keep

- **WHEN** a legacy row has status `new` and a blank availability
- **THEN** its notes are exactly the row's notes

#### Scenario: Load is re-run

- **WHEN** the same export is loaded twice
- **THEN** each candidate's notes are identical after both runs

#### Scenario: Unknown legacy status

- **WHEN** a legacy row carries a status outside the documented codes
- **THEN** the row is rejected with the existing status reason code and no business data is
  written for it
