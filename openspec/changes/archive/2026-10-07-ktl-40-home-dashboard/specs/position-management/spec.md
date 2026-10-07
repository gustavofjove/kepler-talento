## MODIFIED Requirements

### Requirement: Bounded position listing

`GET /api/positions` SHALL return a page envelope containing items, effective page information and total matching count. It SHALL default to open positions, page 1, page size 25 and update time descending; page sizes SHALL be limited to 1 through 100. Text filtering SHALL ignore blank input and otherwise match title or location without regard to case or accents. Status filtering SHALL accept only `open`, `closed` or `all`.

The closed sortable fields SHALL be `title`, `location`, `status` and `updatedAt`, each in `asc` or `desc` direction. The position identifier ascending SHALL be the final tie-breaker. Each list item SHALL contain only identifier, title, location, status, update time, version, the number of candidates added to the position and the number of those candidates at each stage (`new`, `shortlisted`, `interview`, `hired`, `rejected`); it SHALL NOT contain description, requirements, candidate-match data or any candidate identity. The five stage counts SHALL always add up to the candidate count, which includes links to logically removed candidates. The candidate count and the stage counts SHALL require only `positions.read`, because they name no one.

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

#### Scenario: List shows how many candidates are at each stage

- **WHEN** an authorized reader lists positions and one position has links at `new` ×3, `interview` ×1 and `rejected` ×2
- **THEN** that item reports stage counts of 3, 0, 1, 0 and 2 for `new`, `shortlisted`, `interview`, `hired` and `rejected`, and a candidate count of 6

#### Scenario: Removed candidates are still counted per stage

- **WHEN** one of a position's linked candidates has been logically removed
- **THEN** its link is still counted in its stage and in the candidate count, so the stage counts add up to the candidate count

#### Scenario: Stage counts need only position read

- **WHEN** an actor holding `positions.read` but not `candidates.read` lists positions
- **THEN** the response includes the stage counts and contains no candidate identifier or name
