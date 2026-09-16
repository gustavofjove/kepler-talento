## MODIFIED Requirements

### Requirement: Search authorization and visibility decision

Every search SHALL fail closed unless the current actor has `candidates.read`. The domain has no
ownership, team, or assignment attribute from which a narrower truthful scope could be derived, so
every actor holding `candidates.read` SHALL see the same eligible candidate population and the
system SHALL NOT offer a permission that widens or narrows it. Search terms and filter values SHALL
NOT be recorded in application, request, audit, or diagnostic logs.

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

#### Scenario: Personal search term reaches diagnostics

- **WHEN** a request containing a person's name succeeds or fails
- **THEN** logs identify the request only by safe operational metadata such as correlation ID and
  never contain the term or filter payload
