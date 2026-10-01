## ADDED Requirements

### Requirement: Candidate availability check

Every candidate SHALL carry exactly one availability check, made of:

- a value: `unknown`, `available` or `unavailable`;
- the calendar date it was checked;
- an optional calendar date until which the candidate is unavailable;
- the user who recorded it.

A new candidate, whether created through the API or the CSV import, SHALL start as `unknown`. An
`unknown` check SHALL carry no check date, no until date and no checker.

A known check SHALL carry a check date. The check date SHALL NOT be later than the current UTC date
plus one day, and it MAY be earlier than the stored check, so a mistaken check can be amended. An
until date SHALL be accepted only with `unavailable` and SHALL NOT be earlier than the check date.
The checker SHALL be assigned by the server from the current actor and SHALL never be supplied by
the caller. Only the latest check SHALL be kept.

The candidate SHALL have no other status and no free-text availability. The general candidate
create and update operations SHALL NOT accept or change the availability check.

#### Scenario: New candidate is unchecked

- **WHEN** a candidate is created through the API
- **THEN** its availability reads back as `unknown` with no check date, no until date and no checker

#### Scenario: Unavailable until a date

- **WHEN** an authorized actor records `unavailable` with until date 2027-01-15 and check date today
- **THEN** the candidate reads back as `unavailable` until 2027-01-15, checked today, with the actor
  as checker

#### Scenario: Check date in the future

- **WHEN** a check carries a date more than one day after the current UTC date
- **THEN** it is refused with a stable validation code and the stored check is unchanged

#### Scenario: Earlier check date amends a mistake

- **WHEN** a check dated today is stored and an authorized actor records the same value dated ten
  days earlier
- **THEN** the write succeeds and the earlier date is stored

#### Scenario: Until date with the wrong value or before the check

- **WHEN** a check supplies an until date with `available` or `unknown`, or an until date earlier
  than its check date
- **THEN** it is refused with a stable validation code and nothing changes

#### Scenario: Unknown value is supplied

- **WHEN** a check supplies a value outside `unknown`, `available` and `unavailable`
- **THEN** it is refused with a stable validation code and nothing changes

#### Scenario: Reset to unknown

- **WHEN** an authorized actor records `unknown` for a candidate with a stored check
- **THEN** the check date, until date and checker are cleared

#### Scenario: General update leaves availability alone

- **WHEN** a candidate with a stored check is updated through the general candidate update
- **THEN** its availability value, dates and checker are unchanged

### Requirement: Availability is recorded through its own write

The system SHALL expose `PUT /api/candidates/{id}/availability`, which accepts the value, the
check date, the optional until date and the candidate's last-read version, and returns the
complete candidate. Every availability write, including reconfirming an unchanged value, SHALL
use this operation.

The operation SHALL require authentication and `candidates.update`. Both SHALL be checked before
the request is validated or the candidate is looked up, so that an unauthorized caller receives the
same refusal for a valid body, an invalid body, an existing candidate and a missing one. A stale
version SHALL be refused with the candidate concurrency conflict. A check on a logically removed
candidate SHALL be refused with the removed-candidate validation code.

Reading a candidate SHALL return its availability value, check date, until date and the checker's
display name. It SHALL NOT return the checker's user identifier.

#### Scenario: Reconfirming an unchanged value

- **WHEN** an authorized actor records the stored value and until date again with today's date
- **THEN** the value is unchanged, the check date is today, the checker is that actor and the
  candidate's version advances

#### Scenario: Unauthenticated caller

- **WHEN** a caller without a real current actor sends an availability write, with a valid or an
  invalid body
- **THEN** the request is refused before validation and nothing changes

#### Scenario: Actor without update permission

- **WHEN** an actor holding `candidates.read` but not `candidates.update` sends an availability
  write for an existing or a missing candidate
- **THEN** the request is refused as forbidden before validation or lookup

#### Scenario: Stale version

- **WHEN** an availability write carries a version older than the stored one
- **THEN** it is refused with the candidate concurrency conflict and nothing changes

#### Scenario: Removed candidate

- **WHEN** an availability write targets a logically removed candidate
- **THEN** it is refused with the removed-candidate validation code

#### Scenario: Checker is read back

- **WHEN** a reader holding `candidates.read` reads a candidate with a known check
- **THEN** the response carries the checker's display name and no user identifier

### Requirement: Availability checks are audited

Every successful availability write SHALL record a `candidate.availability_checked` audit event in
the same transaction. The event SHALL carry the actor and the candidate identifier only, never the
value or the dates. A general candidate update SHALL record `candidate.updated`.
`candidate.status_changed` SHALL no longer be recorded, but SHALL remain in the event catalogue so
that existing events can still be read and filtered.

#### Scenario: Check is audited

- **WHEN** an authorized actor records an availability check
- **THEN** one `candidate.availability_checked` event names the actor and the candidate and contains
  no availability value or date

#### Scenario: Historical status event is filtered

- **WHEN** an auditor filters the trail by `candidate.status_changed`
- **THEN** the filter is accepted and returns the events recorded before this change

## REMOVED Requirements

### Requirement: Constrained candidate status

**Reason**: The global candidate status overlapped with the per-position stage and could contradict
it. Its pipeline values (`in_process`, `hired`, `rejected`) belong to a position, and `available`
had no date. The position stage is now the only pipeline state, and the candidate availability
check replaces `available`.
**Migration**: Existing values are not carried over; all current data is test data. API clients
stop sending `status` on create and update, and record availability through
`PUT /api/candidates/{id}/availability`. Pipeline progress is read from the candidate's position
links.
