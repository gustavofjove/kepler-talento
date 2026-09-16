## MODIFIED Requirements

### Requirement: Candidate status and primary-CV state

Search SHALL accept only the five candidate statuses `new`, `available`, `in_process`, `hired`,
and `rejected`. A candidate SHALL count as having a primary CV when KTL-9 records a non-removed
document as primary, independently of its pending, available, or refused scan state; document
downloadability SHALL continue to be enforced by the document capability.

Logically deleted candidates SHALL be excluded by default. A caller MAY request that they be
included, through an explicit include-removed option that defaults to excluding them; when included,
each result SHALL be distinguishable by its removed state so that the caller cannot mistake a
removed candidate for a live one. Omitting the option, or supplying it as false, SHALL behave
exactly as before.

#### Scenario: Status subset is selected

- **WHEN** an authorized actor selects one or more of the five statuses
- **THEN** only candidates whose status is in that subset satisfy the status family

#### Scenario: Unsupported status is supplied

- **WHEN** a search request contains a status outside the five supported values
- **THEN** the request is rejected with a stable validation problem and no results

#### Scenario: Candidate has a primary document pending scanning

- **WHEN** a candidate has a non-removed primary document whose scan state is pending and the CV
  filter is `yes`
- **THEN** the candidate satisfies the CV filter, while the search result grants no right to
  download that document

#### Scenario: Candidate has no primary document

- **WHEN** a candidate has documents but none is primary and the CV filter is `no`
- **THEN** the candidate satisfies the CV filter

#### Scenario: Candidate is logically deleted

- **WHEN** a candidate satisfies every supplied filter but is logically deleted, and the caller has
  not asked for removed candidates
- **THEN** the candidate is absent from every search page and from the total count

#### Scenario: Removed candidates are requested

- **WHEN** an authorized actor searches asking for removed candidates to be included
- **THEN** logically deleted candidates that satisfy every supplied filter appear in the pages and
  in the total count, each distinguishable by its removed state

### Requirement: Bounded deterministic pagination

Search SHALL return a page envelope containing items, requested page information, and the total
matching count. The server SHALL apply a documented default page size of 25 and a maximum page
size of 100, SHALL reject page numbers below 1 and sizes outside 1 through 100.

Results SHALL be ordered by a caller-selected sort field and direction, drawn from a closed,
documented set of sortable fields, defaulting to update time descending. The candidate identifier
ascending SHALL always be applied as the final tie-breaker, so that pages remain stable and
non-overlapping for unchanged data whichever sort is chosen.

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

#### Scenario: Unknown sort field is supplied

- **WHEN** a caller supplies a sort field outside the documented set
- **THEN** the request is rejected with a stable validation problem, the query does not execute, and
  the supplied text reaches no part of it

#### Scenario: Page beyond the end is requested

- **WHEN** a caller requests a page past the end of the matching set
- **THEN** an empty page is returned with the correct total matching count, rather than an error or
  the last populated page

### Requirement: Search authorization and visibility decision

Every search SHALL fail closed unless the current actor has `candidates.read`. The domain has no
ownership, team, or assignment attribute from which a narrower truthful scope could be derived, so
every actor holding `candidates.read` SHALL see the same eligible candidate population and the
system SHALL NOT offer a permission that widens or narrows it.

Including logically removed candidates SHALL require the permission that governs candidate removal
and restoration, checked before the request is validated. An actor holding `candidates.read` alone
SHALL receive the forbidden refusal when asking for removed candidates, and SHALL NOT instead
receive a silently narrowed result.

Search terms and filter values SHALL NOT be recorded in application, request, audit, or diagnostic
logs.

#### Scenario: Unauthenticated caller searches

- **WHEN** a caller without a real current actor invokes search
- **THEN** the request is refused and returns no candidate data or count

#### Scenario: Actor lacks candidate viewing permission

- **WHEN** an authenticated actor without `candidates.read` invokes search
- **THEN** the request is refused and returns no candidate data or count

#### Scenario: Two permitted actors see the same population

- **WHEN** two actors holding `candidates.read` search the same unchanged data
- **THEN** both receive the same matches, because no visibility scope narrows either one

#### Scenario: Actors differ only by view-all permission

- **WHEN** the permission vocabulary is inspected for a permission that widens candidate visibility
- **THEN** none exists: the inert `view_all_candidates` is gone, so no pair of actors can differ by
  it and no caller can be misled into believing it grants anything

#### Scenario: Removed candidates are requested without permission

- **WHEN** an actor holding `candidates.read` but not the removal permission asks for removed
  candidates to be included
- **THEN** the request is refused as forbidden before validation, rather than answered with the
  active-only result

#### Scenario: Personal search term reaches diagnostics

- **WHEN** a request containing a person's name succeeds or fails
- **THEN** logs identify the request only by safe operational metadata such as correlation ID and
  never contain the term or filter payload
