## MODIFIED Requirements

### Requirement: Candidate read and list

The system SHALL return a single candidate by identifier including its relation collections.

Listing candidates for the list screen SHALL be paged, SHALL be ordered and filtered by the server,
and SHALL return the minimal list projection rather than candidate aggregates. Listing SHALL exclude
logically deleted candidates unless the caller explicitly asks for them and holds the permission
that governs them. The system SHALL NOT offer an operation that returns every candidate in one
unbounded response.

Reading one candidate remains the way to obtain that candidate's relation collections; a list
SHALL NOT be a means of obtaining them in bulk.

#### Scenario: Candidate is read by identifier

- **WHEN** an authorized actor reads an existing candidate
- **THEN** the response carries the candidate's field set and its language, program, education,
  experience, skill and document collections

#### Scenario: Default listing excludes removed candidates

- **WHEN** an authorized actor lists candidates without asking for removed records
- **THEN** logically deleted candidates are absent from the result

#### Scenario: Removed candidates are listed on request

- **WHEN** an authorized actor lists candidates and explicitly asks to include removed records
- **THEN** logically deleted candidates appear, distinguishable by their inactive state

#### Scenario: List response is inspected

- **WHEN** an authorized actor lists candidates
- **THEN** each item carries the minimal list projection, and the response contains no relation
  collections, notes, consent metadata or retention metadata

#### Scenario: Whole table is requested

- **WHEN** a caller attempts to obtain every candidate in a single response
- **THEN** no such operation exists: the request is paged within the documented bounds like any
  other listing

#### Scenario: Listing is ordered and filtered

- **WHEN** an authorized actor lists candidates with a filter and a sort
- **THEN** the filtering and ordering are applied across the whole matching set by the server, and
  the returned page reflects that set rather than an ordering of the page alone
