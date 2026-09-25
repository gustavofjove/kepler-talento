## MODIFIED Requirements

### Requirement: Bounded position listing

`GET /api/positions` SHALL return a page envelope containing items, effective page information and total matching count. It SHALL default to open positions, page 1, page size 25 and update time descending; page sizes SHALL be limited to 1 through 100. Text filtering SHALL ignore blank input and otherwise match title or location without regard to case or accents. Status filtering SHALL accept only `open`, `closed` or `all`.

The closed sortable fields SHALL be `title`, `location`, `status` and `updatedAt`, each in `asc` or `desc` direction. The position identifier ascending SHALL be the final tie-breaker. Each list item SHALL contain only identifier, title, location, status, update time, version and the number of candidates added to the position; it SHALL NOT contain description, requirements, candidate-match data or any candidate identity. The candidate count SHALL require only `positions.read`, because it names no one.

#### Scenario: Default list is requested

- **WHEN** an authorized reader lists positions without filters or paging values
- **THEN** the system returns at most 25 open positions ordered by update time descending with identifier tie-breaking

#### Scenario: Text and status filters are applied

- **WHEN** an authorized reader supplies non-blank text and `closed`
- **THEN** only closed positions whose title or location matches the text ignoring case and accents are counted and returned

#### Scenario: All statuses are requested

- **WHEN** an authorized reader supplies `all`
- **THEN** eligible open and closed positions can appear in the result

#### Scenario: List input is unsupported

- **WHEN** a caller supplies an unknown status, sort field or direction, a page below 1 or a page size outside 1 through 100
- **THEN** the request is rejected with a stable validation problem before an unbounded or dynamically composed query executes

#### Scenario: Page is past the end

- **WHEN** an authorized reader requests a page beyond the matching set
- **THEN** the system returns an empty item list with the correct total count

#### Scenario: List shows how many candidates were added

- **WHEN** an authorized reader lists positions and one position has three linked candidates
- **THEN** that item reports a candidate count of 3 and no candidate name or identifier

### Requirement: Live candidate matching preserves privacy boundaries

The position detail experience SHALL evaluate its stored requirements through the existing candidate-search behavior when, and only when, the actor also holds `candidates.read`. Results SHALL remain live for open and closed positions, SHALL be paged and SHALL use the existing minimal candidate projection, deterministic order, filter semantics and no-duplicate guarantee. The system SHALL NOT persist match snapshots or match counts on positions, and evaluating matches SHALL NOT create, change or remove any position candidate link. Links are recorded only through the explicit position candidate operations.

For an actor holding `positions.manage` on an open position, each match row SHALL offer «Añadir». For a candidate already linked to the position, the row SHALL show a disabled «Añadido» instead, determined from the position's complete link list. After a successful add, the row SHALL switch to «Añadido» and the linked candidates panel SHALL show the candidate without a page reload. The match action SHALL NOT appear on the advanced search page. Match rows on the position page SHALL NOT offer «Abrir CV»; the advanced search page keeps it. On both pages, a click on a result row outside its links and controls SHALL open the candidate. The candidate's name SHALL remain a keyboard-reachable link, phones SHALL be plain text, and there SHALL be no separate «Detalle» action.

An actor holding `positions.read` without `candidates.read` SHALL receive the position but SHALL receive no candidate item, count or match-derived fact and the client SHALL NOT initiate a candidate search. Candidate values, requirements and match counts SHALL NOT appear in URLs, logs or audit payloads.

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

- **WHEN** a matching candidate has a primary CV and the actor holds `documents.download`
- **THEN** the position page offers no «Abrir CV» action for that row, while the advanced search page keeps its permission-checked document action

#### Scenario: Advanced search results are unchanged

- **WHEN** results are shown on the advanced search page
- **THEN** no add-to-position action appears, «Abrir CV» remains, and the rest of the copy is unchanged apart from the removed «Detalle»

#### Scenario: Result row opens the candidate

- **WHEN** a user clicks a result row outside its e-mail link and controls
- **THEN** the candidate page opens, a modified or middle click opens it in a new tab, and clicking a control in the row does not navigate
