# Candidate Search Specification

## Purpose

Define la búsqueda autorizada, paginada y sin duplicados de candidatos mediante todos los filtros que utiliza la experiencia avanzada de RRHH.

## Requirements

### Requirement: Full candidate search filter contract

The system SHALL search active candidates using free text, candidate statuses, skill criteria,
language criteria, program criteria, tag criteria, and primary-CV presence. Blank text and
criteria values, an empty or complete status selection, empty criterion families, and an unset CV
filter SHALL place no restriction on the result. Free text SHALL preserve current behavior across
candidate identity, contact, and notes fields using case-insensitive substring matching; it SHALL
NOT be extended to candidate note entries.

#### Scenario: Empty filters are ignored

- **WHEN** an authorized actor searches with blank text, every status selected, no criteria,
  and no CV selection
- **THEN** every non-deleted candidate within the actor's visibility scope is eligible to appear

#### Scenario: Empty tag family is ignored

- **WHEN** an authorized actor searches supplying no tag criteria
- **THEN** the tag family places no restriction on the result

#### Scenario: Filter families combine

- **WHEN** an authorized actor supplies non-empty text, status, skill, language, program, tag,
  and CV filters
- **THEN** a candidate appears only when it satisfies every non-empty filter family

#### Scenario: Text matching preserves parity

- **WHEN** text occurs as a case-insensitive substring in a candidate identity, contact, or notes
  field
- **THEN** the candidate satisfies the text family without the response exposing the field that
  matched

#### Scenario: Note entries are not searched by text

- **WHEN** a search term occurs only in a candidate's note entries and nowhere in its identity,
  contact or notes field
- **THEN** the candidate does not satisfy the text family

#### Scenario: Unknown criterion value is supplied

- **WHEN** a non-empty skill, language, program, or tag value matches no known candidate relation
- **THEN** that criterion matches no candidate rather than broadening the result or failing open

### Requirement: Multi-value criteria semantics

Each skill, language, program, and tag family SHALL independently support `ANY` and `ALL` modes.
`ANY` SHALL match when at least one criterion in that family matches; `ALL` SHALL match only when
every distinct criterion in that family matches, including when matches occur in different
relation rows. A criterion with an empty level SHALL match any level for the same value. A
non-empty level SHALL match the same value at that level or at any later position in the same
level family's configured catalog order, using case-insensitive normalized comparison to resolve
the named level. Active and inactive levels SHALL both retain their position in that ranking. A
non-empty level that does not resolve in the applicable family SHALL match no candidate.

A tag criterion SHALL carry no level; a tag criterion supplying a level SHALL be rejected with a
stable validation problem. Repeated criteria SHALL remain as supplied: in `ANY` mode a lower
minimum can make a higher minimum redundant, while in `ALL` mode every distinct minimum SHALL
still be satisfied. Applying a saved preset or matching a position SHALL use these same semantics
without rewriting the stored filters.

#### Scenario: ANY mode matches one criterion

- **WHEN** a candidate satisfies one but not all criteria in a family whose mode is `ANY`
- **THEN** the candidate satisfies that family

#### Scenario: ALL mode spans relation rows

- **WHEN** a candidate satisfies every distinct criterion in an `ALL` family through separate
  relation rows
- **THEN** the candidate satisfies that family exactly once

#### Scenario: ALL mode is incomplete

- **WHEN** a candidate is missing one distinct criterion in an `ALL` family
- **THEN** the candidate does not satisfy that family

#### Scenario: Tags are matched in ANY mode

- **WHEN** an authorized actor supplies several tags with mode `ANY`
- **THEN** a candidate holding at least one of them satisfies the tag family

#### Scenario: Tags are matched in ALL mode

- **WHEN** an authorized actor supplies several tags with mode `ALL`
- **THEN** only a candidate holding every one of them satisfies the tag family, and it appears
  once

#### Scenario: Tag criterion supplies a level

- **WHEN** a search request contains a tag criterion carrying a level
- **THEN** the request is rejected with a stable validation problem and no results are returned

#### Scenario: Criterion omits its level

- **WHEN** a criterion names a value and has an empty level
- **THEN** a candidate relation with that value satisfies it regardless of the stored level

#### Scenario: Criterion matches its level and higher levels

- **WHEN** a language criterion names level B2 and candidates hold that language at B1, B2, and C1
- **THEN** the candidates at B2 and C1 satisfy the criterion and the candidate at B1 does not

#### Scenario: Highest level matches only itself

- **WHEN** a criterion names the final level in its family's configured order
- **THEN** only a relation at that level satisfies the criterion

#### Scenario: Reordered levels change the ranking

- **WHEN** an administrator moves a level before the selected minimum in the applicable family
- **THEN** a relation at that moved level no longer satisfies the criterion

#### Scenario: Inactive level retains its rank

- **WHEN** a candidate holds an inactive level positioned at or above the selected minimum
- **THEN** that relation satisfies the criterion

#### Scenario: Unknown level fails closed

- **WHEN** a criterion names a level that does not exist in its applicable level family
- **THEN** the criterion matches no candidate

#### Scenario: ALL mode applies independent minimums

- **WHEN** an `ALL` skill family requires Java at Medio and SQL at Alto
- **THEN** a candidate with Java at Experto and SQL at Alto satisfies the family exactly once, and
  a candidate with SQL at Medio does not

#### Scenario: Saved filters use minimum-level semantics

- **WHEN** a saved preset or position requirement contains a criterion at level B2
- **THEN** applying it matches B2 and higher levels without changing the stored filter

#### Scenario: Duplicate joins and criteria are present

- **WHEN** several relation rows or repeated normalized criteria can satisfy the same candidate
- **THEN** the candidate appears no more than once and repeated criteria do not change `ALL`
  semantics

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

### Requirement: Minimal search result projection

Each search item SHALL contain only candidate identifier, first name, last name, phone, email,
status, primary-CV presence, the primary document identifier when one exists, and update time.
It SHALL NOT return a full candidate aggregate, relation collections, tags, notes, note entries,
consent or retention metadata, document paths, storage keys, filenames, or scan internals.

Tags SHALL be usable as a filter without becoming part of this projection: a candidate matched by
a tag SHALL be returned with the same fields as a candidate matched any other way.

#### Scenario: Search item is returned

- **WHEN** a candidate matches a search
- **THEN** its item contains exactly the documented search projection and no excluded personal
  or storage data

#### Scenario: Candidate is matched by a tag

- **WHEN** a candidate matches because it holds a tag the actor filtered on
- **THEN** its item carries the documented projection and does not carry the tag that matched

#### Scenario: Primary document identifier is returned

- **WHEN** a matching candidate has a non-removed primary document
- **THEN** its identifier may be present for subsequent permission-checked document actions but
  reveals no storage location and grants no download capability

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

### Requirement: Superseded search cancellation

The search experience SHALL debounce user-driven searches and SHALL abort an in-flight request
through the shared transport when a newer search supersedes it. An aborted request SHALL NOT
update results, counts, loading errors, or user-facing notifications.

#### Scenario: User changes filters during a request

- **WHEN** a debounced search is in flight and a newer filter state starts another search
- **THEN** the earlier network request is aborted through its cancellation signal and only the
  newer response may update the page

#### Scenario: Superseded request fails after cancellation

- **WHEN** an abandoned request reports a transport error after it has been superseded
- **THEN** no error toast or stale state from that request is presented
