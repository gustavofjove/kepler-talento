## Purpose

Define the secure lifecycle of recruitment positions, their reusable candidate-search requirements, and the live matching experience available to authorized HR users.

## Requirements

### Requirement: Position identity and lifecycle

The system SHALL represent a position with an opaque identifier, trimmed title, optional sanitized rich-text description, optional trimmed location, status, complete normalized candidate-search requirements, creation time, update time and concurrency version. The title SHALL be required, at most 200 characters and unique across all positions under comparison that ignores case and accents. Location SHALL be at most 200 characters, description input SHALL be at most 20,000 characters, and status SHALL be exactly `open` or `closed`.

A new position SHALL start as `open`. An authorized manager SHALL be able to change either status to the other. Closing SHALL retain the position and SHALL NOT alter candidates, presets or requirements; there SHALL be no position deletion operation.

#### Scenario: Position is created

- **WHEN** an authorized manager submits a unique valid title, optional fields and valid requirements
- **THEN** the system creates an open position with a server-assigned identifier, timestamps and concurrency version

#### Scenario: Title differs only by case and accents

- **WHEN** an authorized manager creates or renames a position to `programador SENIOR` while `Programador sénior` exists
- **THEN** the operation is rejected with a stable title-conflict problem and neither position changes

#### Scenario: Position is closed

- **WHEN** an authorized manager changes an open position to `closed` with its current version
- **THEN** the position remains addressable, its requirements remain unchanged and no row is deleted

#### Scenario: Position is reopened

- **WHEN** an authorized manager changes a closed position to `open` with its current version
- **THEN** the position becomes part of the default open listing again

#### Scenario: Unsupported status is submitted

- **WHEN** an update request contains a status outside the closed vocabulary
- **THEN** the request is rejected with a stable validation problem and no change is stored

### Requirement: Safe rich-text position description

Position descriptions SHALL permit only paragraphs, line breaks, bold, italic, ordered lists, unordered lists and list items. The server SHALL canonicalize and sanitize description input before persistence. Links, images, embedded content, scripts, event attributes, style attributes and every unapproved element or attribute SHALL NOT be stored or returned as active markup. Diagnostics SHALL NOT contain description values.

#### Scenario: Allowed formatting is submitted

- **WHEN** a manager submits paragraphs, emphasis and lists within the length limit
- **THEN** later reads return equivalent canonical safe formatting

#### Scenario: Active content is submitted

- **WHEN** a description contains script, event-handler, link, image, embedded or styled markup
- **THEN** only allowlisted canonical markup is persisted and returned and no active content executes

#### Scenario: Description is too long

- **WHEN** description input exceeds 20,000 characters
- **THEN** the request is rejected before any position change is stored

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

### Requirement: Position read and write API

The system SHALL expose `GET /api/positions`, `GET /api/positions/{id}`, `POST /api/positions` and `PUT /api/positions/{id}`. A single-position response SHALL contain the full position value, including sanitized description and normalized requirements. A successful creation SHALL identify the new resource. Updates SHALL replace editable fields and require the last-read concurrency version. No position `DELETE` endpoint SHALL exist.

#### Scenario: Existing position is read

- **WHEN** an authorized reader requests an existing position identifier
- **THEN** the full position is returned with its current version and no candidate result embedded in it

#### Scenario: Missing position is read

- **WHEN** an authorized reader requests a position identifier that does not exist
- **THEN** a stable not-found problem is returned

#### Scenario: Concurrent updates conflict

- **WHEN** two managers load the same version and the second saves after the first
- **THEN** the second update receives a stable concurrency-conflict problem and the first update remains stored

#### Scenario: Delete route is attempted

- **WHEN** a caller attempts to delete a position through the API
- **THEN** no business endpoint performs that operation

### Requirement: Position requirements use the candidate-search contract

Each position SHALL store a complete normalized copy of the current candidate-search filter contract, including its schema version. Create and update SHALL apply the same status, criterion, mode, CV-state, length and normalization rules as candidate search. Empty requirements SHALL be valid and SHALL mean that every active candidate is eligible under the existing search semantics. An unreadable stored filter document SHALL be refused and SHALL never be replaced by empty requirements.

#### Scenario: Valid requirements are saved

- **WHEN** a manager saves a position with valid multi-family ANY and ALL criteria
- **THEN** later position reads return the normalized complete filter value with the same meaning

#### Scenario: Invalid requirements are submitted

- **WHEN** a position write contains an unsupported candidate status, mode, CV selection or malformed criterion
- **THEN** the write is rejected with stable validation details and no partial position change is stored

#### Scenario: Stored filter version is unsupported

- **WHEN** the persisted requirements cannot be parsed under a supported filter schema
- **THEN** reading or evaluating them fails with a stable problem and does not run an empty search

### Requirement: Presets are copied rather than linked

The position editor SHALL let an actor permitted by the saved-search capability apply a shared preset to the draft requirements. Applying SHALL copy the returned normalized filters and SHALL retain no preset identifier or synchronization relationship. A permitted preset manager SHALL be able to create a new shared preset from the current draft requirements. A preset failure SHALL leave the current draft unchanged.

#### Scenario: Preset is applied to a draft

- **WHEN** a permitted actor applies a shared preset in the position editor
- **THEN** the draft requirements are replaced by a copy of its normalized filters and the preset use is recorded under the existing preset rules

#### Scenario: Source preset later changes

- **WHEN** a position was saved from a preset and that preset is later changed or deleted
- **THEN** the position requirements remain unchanged and usable

#### Scenario: Missing preset is selected

- **WHEN** a selected preset was deleted before it is applied
- **THEN** a localized error is shown and the current position draft remains unchanged

#### Scenario: Current requirements are saved as a preset

- **WHEN** an actor holding the preset-management permission supplies a valid unique name
- **THEN** a new shared preset is created with a copy of the draft requirements and no link is stored on the position

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

### Requirement: Position authorization fails closed

Every position request SHALL authenticate and check `positions.read` for reads or `positions.manage` for writes before validating input, querying a position or disclosing whether it exists. Application handlers SHALL repeat the same guards. The two position permissions SHALL NOT imply each other or imply candidate, preset, catalog or document permissions.

#### Scenario: Unauthenticated caller sends an invalid request

- **WHEN** an unauthenticated caller sends malformed input to any position endpoint
- **THEN** the caller receives the same forbidden refusal as for valid input and no validation detail

#### Scenario: Unauthorized caller requests an existing or missing id

- **WHEN** an authenticated actor without the required permission requests either identifier
- **THEN** both requests are refused without revealing which identifier exists

#### Scenario: Reader attempts a write

- **WHEN** an actor with `positions.read` but without `positions.manage` creates, changes, closes or reopens a position
- **THEN** the operation is forbidden and no position changes

### Requirement: Position user experience is localized and accessible

The application SHALL provide list, create, detail and edit routes for positions. All user-facing copy SHALL be Spanish through the localization catalogue. Forms SHALL have programmatic labels, stable names and test identifiers; status, loading, empty, error, validation and conflict states SHALL be announced or exposed accessibly. The experience SHALL remain operable by keyboard and at narrow viewports without horizontal page overflow, except that a wide table MAY scroll within its own container.

#### Scenario: Position is created in the SPA

- **WHEN** an authorized manager completes the controlled create form and saves
- **THEN** the application opens the new detail page and presents the saved Spanish-labelled values

#### Scenario: Position edit is cancelled

- **WHEN** a manager changes draft values and activates Cancelar
- **THEN** no write request is made and stored values remain unchanged

#### Scenario: Narrow viewport is used

- **WHEN** the position list or form is shown at 390 pixels wide
- **THEN** all controls remain reachable and the page does not scroll horizontally

#### Scenario: Rich description is read with assistive technology

- **WHEN** a position description contains allowed structure
- **THEN** headings are not invented, list semantics are retained and focus order remains logical
