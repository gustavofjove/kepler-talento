# Candidate Search Specification

## Purpose

Define la búsqueda autorizada, paginada y sin duplicados de candidatos mediante todos los filtros que utiliza la experiencia avanzada de RRHH.

## Requirements

### Requirement: Full candidate search filter contract

The system SHALL search active candidates using free text, candidate statuses, skill criteria,
language criteria, program criteria, and primary-CV presence. Blank text and criteria values,
an empty or complete status selection, empty criterion families, and an unset CV filter SHALL
place no restriction on the result. Free text SHALL preserve current behavior across candidate
identity, contact, and notes fields using case-insensitive substring matching.

#### Scenario: Empty filters are ignored

- **WHEN** an authorized actor searches with blank text, every status selected, no criteria,
  and no CV selection
- **THEN** every non-deleted candidate within the actor's visibility scope is eligible to appear

#### Scenario: Filter families combine

- **WHEN** an authorized actor supplies non-empty text, status, skill, language, program, and CV
  filters
- **THEN** a candidate appears only when it satisfies every non-empty filter family

#### Scenario: Text matching preserves parity

- **WHEN** text occurs as a case-insensitive substring in a candidate identity, contact, or notes
  field
- **THEN** the candidate satisfies the text family without the response exposing the field that
  matched

#### Scenario: Unknown criterion value is supplied

- **WHEN** a non-empty skill, language, or program value matches no known candidate relation
- **THEN** that criterion matches no candidate rather than broadening the result or failing open

### Requirement: Multi-value criteria semantics

Each skill, language, and program family SHALL independently support `ANY` and `ALL` modes.
`ANY` SHALL match when at least one criterion in that family matches; `ALL` SHALL match only when
every distinct criterion in that family matches, including when matches occur in different
relation rows. A criterion with an empty level SHALL match any level for the same value, while a
non-empty level SHALL require the same value and level using case-insensitive normalized
comparison.

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

#### Scenario: Criterion omits its level

- **WHEN** a criterion names a value and has an empty level
- **THEN** a candidate relation with that value satisfies it regardless of the stored level

#### Scenario: Duplicate joins and criteria are present

- **WHEN** several relation rows or repeated normalized criteria can satisfy the same candidate
- **THEN** the candidate appears no more than once and repeated criteria do not change `ALL`
  semantics

### Requirement: Candidate status and primary-CV state

Search SHALL accept only the five candidate statuses `new`, `available`, `in_process`, `hired`,
and `rejected`. A candidate SHALL count as having a primary CV when KTL-9 records a non-removed
document as primary, independently of its pending, available, or refused scan state; document
downloadability SHALL continue to be enforced by the document capability. Logically deleted
candidates SHALL never appear in search.

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

- **WHEN** a candidate satisfies every supplied filter but is logically deleted
- **THEN** the candidate is absent from every search page and from the total count

### Requirement: Bounded deterministic pagination

Search SHALL return a page envelope containing items, requested page information, and the total
matching count. The server SHALL apply a documented default page size of 25 and a maximum page
size of 100, SHALL reject page numbers below 1 and sizes outside 1 through 100, and SHALL order
results deterministically by update time descending and candidate identifier ascending as a
tie-breaker.

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

### Requirement: Minimal search result projection

Each search item SHALL contain only candidate identifier, first name, last name, phone, email,
status, primary-CV presence, the primary document identifier when one exists, and update time.
It SHALL NOT return a full candidate aggregate, relation collections, notes, consent or retention
metadata, document paths, storage keys, filenames, or scan internals.

#### Scenario: Search item is returned

- **WHEN** a candidate matches a search
- **THEN** its item contains exactly the documented search projection and no excluded personal
  or storage data

#### Scenario: Primary document identifier is returned

- **WHEN** a matching candidate has a non-removed primary document
- **THEN** its identifier may be present for subsequent permission-checked document actions but
  reveals no storage location and grants no download capability

### Requirement: Search authorization and visibility decision

Every search SHALL fail closed unless the current actor has `view_candidates`. For KTL-10,
`view_all_candidates` SHALL remain inert because the domain has no ownership, team, or assignment
attribute from which a narrower truthful scope can be derived; every actor with `view_candidates`
SHALL therefore see the same eligible candidate population. Search terms and filter values SHALL
NOT be recorded in application, request, audit, or diagnostic logs.

#### Scenario: Unauthenticated caller searches

- **WHEN** a caller without a real current actor invokes search
- **THEN** the request is refused and returns no candidate data or count

#### Scenario: Actor lacks candidate viewing permission

- **WHEN** an authenticated actor without `view_candidates` invokes search
- **THEN** the request is refused and returns no candidate data or count

#### Scenario: Actors differ only by view-all permission

- **WHEN** two actors with `view_candidates` search the same unchanged data and only one also has
  `view_all_candidates`
- **THEN** both receive the same matches because KTL-10 defines no unsupported visibility scope

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
