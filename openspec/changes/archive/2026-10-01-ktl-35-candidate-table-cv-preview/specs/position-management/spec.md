## MODIFIED Requirements

### Requirement: Live candidate matching preserves privacy boundaries

The position detail experience SHALL evaluate its stored requirements through the existing
candidate-search behavior when, and only when, the actor also holds `candidates.read`. Results
SHALL:

- remain live for open and closed positions;
- be paged;
- use the existing minimal candidate projection, deterministic order, filter semantics and
  no-duplicate guarantee.

The system SHALL NOT persist match snapshots or match counts on positions, and evaluating matches
SHALL NOT create, change or remove any position candidate link. Links are recorded only through the
explicit position candidate operations.

For an actor holding `positions.manage` on an open position, each match row SHALL offer «Añadir».
For a candidate already linked to the position, the row SHALL show a disabled «Añadido» instead,
determined from the position's complete link list. After a successful add, the row SHALL switch to
«Añadido» and the linked candidates panel SHALL show the candidate without a page reload. The match
action SHALL NOT appear on the advanced search page.

Match rows on the position page and result rows on the advanced search page SHALL offer the «CV»
column's «Ver», as the data-tables row CV preview specifies, and SHALL NOT offer «Abrir CV». On
both pages:

- a click on a result row outside its links and controls SHALL open the candidate;
- the candidate's name SHALL remain a keyboard-reachable link;
- phones SHALL be plain text;
- there SHALL be no separate «Detalle» action.

An actor holding `positions.read` without `candidates.read` SHALL receive the position but SHALL
receive no candidate item, count or match-derived fact, and the client SHALL NOT initiate a
candidate search. Candidate values, requirements and match counts SHALL NOT appear in URLs, logs or
audit payloads.

#### Scenario: Candidate newly satisfies requirements

- **WHEN** an eligible candidate changes to satisfy a position and an authorized reader next opens or refreshes its detail
- **THEN** the candidate appears in the live search without a position-candidate link being created

#### Scenario: Match is added to the position

- **WHEN** a manager activates «Añadir» on a match of an open position
- **THEN** a link is created, the row shows a disabled «Añadido» and the candidate appears in the linked candidates panel

#### Scenario: Linked match is shown

- **WHEN** a match is already linked to the position when the page loads
- **THEN** its row shows the disabled «Añadido» state regardless of which match page is shown

#### Scenario: Match action is not offered

- **WHEN** the actor lacks `positions.manage` or the position is closed
- **THEN** match rows offer no add action

#### Scenario: Closed position is viewed

- **WHEN** an authorized reader opens a closed position and can read candidates
- **THEN** its current live matches are evaluated using its unchanged requirements

#### Scenario: Position reader lacks candidate permission

- **WHEN** an actor with `positions.read` but without `candidates.read` opens a position
- **THEN** the position is shown with a localized explanation instead of candidate results and no search request is made

#### Scenario: Candidate result offers a CV action

- **WHEN** a matching candidate's primary CV can be previewed and the actor holds
  `documents.download`
- **THEN** its row offers «Ver» in the «CV» column, on the position page and on the advanced search
  page, and neither page offers «Abrir CV»

#### Scenario: Advanced search results are unchanged

- **WHEN** results are shown on the advanced search page
- **THEN** no add-to-position action appears, and the copy is unchanged apart from the removed
  «Detalle» and «Abrir CV» and the «CV» column's «Ver»

#### Scenario: Result row opens the candidate

- **WHEN** a user clicks a result row outside its e-mail link and controls
- **THEN** the candidate page opens, a modified or middle click opens it in a new tab, and clicking a control in the row does not navigate
