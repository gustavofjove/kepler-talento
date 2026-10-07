## MODIFIED Requirements

### Requirement: Bounded deterministic pagination

Search SHALL return a page envelope containing items, requested page information, and the total
matching count. The server SHALL apply a documented default page size of 25 and a maximum page
size of 100, SHALL reject page numbers below 1 and sizes outside 1 through 100.

Results SHALL be ordered by a caller-selected sort field and direction, drawn from a closed,
documented set of sortable fields, defaulting to update time descending. The documented set SHALL
be update time, last name, availability check date and creation time. The candidate status SHALL
NOT be a sort field. When sorting by availability check date, candidates without a check date SHALL
come last in either direction. The candidate identifier ascending SHALL always be applied as the
final tie-breaker, so that pages remain stable and non-overlapping for unchanged data whichever sort
is chosen. Sorting by creation time SHALL order identically whether or not the search carries a
text filter.

A sort field outside the documented set SHALL be rejected with a stable validation problem before
the query executes. A caller-supplied sort field SHALL NOT be incorporated into the executed query
in any form other than selection from that closed set.

#### Scenario: Page size is omitted

- **WHEN** an authorized actor searches without a page size
- **THEN** the server returns at most 25 results and reports the effective pagination values

#### Scenario: Maximum page size is requested

- **WHEN** an authorized actor requests a page size of 100
- **THEN** the server returns at most 100 results and a correct total matching count

#### Scenario: Pagination is outside its bounds

- **WHEN** a caller requests page zero or a page size greater than 100
- **THEN** the request is rejected with a stable validation problem before executing an
  unbounded query

#### Scenario: Adjacent pages contain tied update times

- **WHEN** several matching candidates have the same update time
- **THEN** the identifier tie-breaker yields stable non-overlapping pages for unchanged data

#### Scenario: Sort field is omitted

- **WHEN** an authorized actor searches without naming a sort field
- **THEN** results are ordered by update time descending with the identifier as tie-breaker

#### Scenario: Documented sort field is selected

- **WHEN** an authorized actor sorts by a documented field in either direction
- **THEN** results are ordered by that field in that direction, with the identifier ascending as the
  final tie-breaker, and pages remain non-overlapping for unchanged data

#### Scenario: Sorted by availability check date

- **WHEN** an authorized actor sorts by availability check date descending, then ascending
- **THEN** descending lists the most recent check first and ascending the oldest first, and in both
  directions every `unknown` candidate follows every checked candidate

#### Scenario: Sorted by creation time

- **WHEN** an authorized actor sorts by `createdAt` descending, then ascending
- **THEN** descending lists the most recently created candidate first and ascending the oldest
  first, with the identifier ascending as the final tie-breaker and non-overlapping pages

#### Scenario: Creation sort with a text filter

- **WHEN** an authorized actor sorts by `createdAt` and also supplies a text filter
- **THEN** the matching candidates are ordered exactly as the same candidates would be without the
  text filter

#### Scenario: Former status sort is requested

- **WHEN** a caller asks to sort by `status`
- **THEN** the request is rejected with the unknown-sort-field validation problem

#### Scenario: Unknown sort field is supplied

- **WHEN** a caller supplies a sort field outside the documented set
- **THEN** the request is rejected with a stable validation problem, the query does not execute, and
  the supplied text reaches no part of it

#### Scenario: Page beyond the end is requested

- **WHEN** a caller requests a page past the end of the matching set
- **THEN** an empty page is returned with the correct total matching count, rather than an error or
  the last populated page
