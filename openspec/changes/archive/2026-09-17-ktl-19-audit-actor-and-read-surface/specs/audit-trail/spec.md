## Purpose

Defines the audit trail as something that can actually answer a question: what an audit event
records, who it names as the actor, which reads and writes produce one, why the application can
never rewrite one, and how an authorized person reads and filters the trail.

## ADDED Requirements

### Requirement: Every audit event names its actor

An audit event SHALL record the identity that caused it. That identity SHALL be stored as the
**internal user identifier** of the acting user. It SHALL NOT be stored as an email address, a
display name, or the external subject identifier issued by the identity provider.

An event caused by a system process rather than a person SHALL record an explicit **system actor**,
distinguishable from both a user and from an absent actor.

An event recorded before the trail carried actors SHALL retain no actor, and this state SHALL be
distinguishable from the system actor. The system SHALL NOT infer, guess or backfill an actor for
such a row.

#### Scenario: User causes an event

- **WHEN** an authenticated user performs an audited operation
- **THEN** the event records that user's internal identifier

#### Scenario: Audit event is inspected for identity data

- **WHEN** an audit event is inspected
- **THEN** it contains no email address, no display name and no external subject identifier

#### Scenario: System process causes an event

- **WHEN** a background process performs an audited operation with no person behind it
- **THEN** the event records the system actor, not an absent actor and not a user

#### Scenario: Historic row is read

- **WHEN** an event recorded before actors were carried is read
- **THEN** it reports an unknown actor, distinguishable from the system actor, and no actor has been
  inferred for it

### Requirement: No audited operation may omit the actor

Every operation that records an audit event SHALL supply an actor. The system SHALL NOT permit an
audited operation to record an event with an absent actor, other than the historic rows that predate
the actor being carried.

A trail in which some operations name the actor and others do not is worse than no trail, because it
presents itself as complete. The completeness of the actor across audited operations SHALL therefore
be verifiable rather than assumed.

#### Scenario: Audited operation is added without an actor

- **WHEN** an operation records an audit event without supplying an actor
- **THEN** the omission is detected rather than silently storing an actorless row

#### Scenario: Trail is inspected for completeness

- **WHEN** the events produced by the audited operations are inspected
- **THEN** every one written after this capability shipped names a user actor or the system actor

### Requirement: Reads of personal data are audited

Opening an individual candidate's record and downloading a document SHALL each record an audit
event, because each is a read that identifies a specific person. The event SHALL record the actor,
the subject identifier, the kind of read, its outcome and the request correlation identifier.

Searching and listing candidates SHALL NOT be audited. They name no individual, and recording them
would enlarge the trail by orders of magnitude while diluting what it is for.

A read that is refused SHALL NOT be recorded as a read that happened. Whether a refused attempt is
recorded as a refusal is a separate decision, and if it is recorded it SHALL be distinguishable from
a successful read.

#### Scenario: Candidate detail is opened

- **WHEN** a permitted actor reads one candidate's record
- **THEN** an audit event records the actor, the candidate identifier, the read event type and the
  correlation identifier

#### Scenario: Document is downloaded

- **WHEN** a permitted actor downloads a document
- **THEN** an audit event records the actor, the document and candidate identifiers and the outcome,
  and contains no file content, filename or storage key

#### Scenario: Candidates are searched or listed

- **WHEN** an actor searches or lists candidates
- **THEN** no audit event is recorded for the search or the listing

#### Scenario: Read is refused

- **WHEN** a read of a candidate or a document is refused for authorization
- **THEN** no event describing a successful read is stored, and any refusal that is recorded is
  distinguishable from a read that happened

### Requirement: Audit events carry identifiers and codes only

An audit event SHALL carry identifiers, event types, outcome codes and timestamps. It SHALL NOT
carry any candidate field value, any document content, filename or storage key, or any free text
supplied by a caller.

The event type SHALL be drawn from a closed catalogue defined in code, so that filtering by event
type is exhaustive and reliable rather than a substring guess.

#### Scenario: Event is inspected for candidate data

- **WHEN** any audit event is inspected
- **THEN** it contains no name, contact detail, note or other candidate field value

#### Scenario: Event type outside the catalogue is recorded

- **WHEN** an operation attempts to record an event type outside the catalogue
- **THEN** it is rejected rather than stored, so the catalogue remains exhaustive

#### Scenario: Filtering by event type

- **WHEN** a reader filters by an event type from the catalogue
- **THEN** every event of that type is returned and no event of another type is

### Requirement: The trail is append-only to the application

The application's runtime database role SHALL hold no privilege to update or delete audit events.
Correcting or removing an audit event SHALL NOT be possible through any application code path.

An audit event SHALL be written in the same transaction as the change it describes, so that a change
that is rolled back leaves no event, and an event that is written implies the change was applied.

#### Scenario: Runtime privileges are inspected

- **WHEN** the runtime database role's privileges on the audit table are inspected
- **THEN** it holds no update privilege and no delete privilege

#### Scenario: Application attempts to change history

- **WHEN** any application code path attempts to modify or remove a stored audit event
- **THEN** the attempt fails, and no such path is exposed through the API

#### Scenario: Audited change is rolled back

- **WHEN** an operation records an audit event and its transaction is then rolled back
- **THEN** no audit event remains

#### Scenario: Event implies an applied change

- **WHEN** an audit event describing an applied change exists
- **THEN** the change it describes was applied

### Requirement: Audit read surface

The system SHALL expose a paged, filterable read of the audit trail. It SHALL support filtering by
date range, event type, actor and subject, and SHALL page deterministically with a documented
default and maximum page size.

The response SHALL carry identifiers and codes. It SHALL NOT resolve a subject identifier to a
candidate's name, nor include any candidate data: resolving a subject remains the candidate
endpoint's responsibility, behind the candidate permission.

An unknown event type or a malformed filter SHALL be rejected with a stable validation problem
rather than silently ignored, so that a reader is never shown a narrower trail than they asked for
without being told.

#### Scenario: Trail is read

- **WHEN** an actor holding the audit permission reads the trail
- **THEN** a page of events is returned with their type, subject, actor, outcome and timestamp

#### Scenario: Trail is filtered by date range and actor

- **WHEN** a reader filters by a date range and an actor
- **THEN** only that actor's events within that range are returned, with a correct total count

#### Scenario: Response is inspected for candidate data

- **WHEN** an audit response is inspected
- **THEN** it contains subject identifiers and no candidate name or other candidate field value

#### Scenario: Malformed filter is supplied

- **WHEN** a reader supplies an unknown event type or a malformed date range
- **THEN** the request is rejected with a stable validation problem rather than answered with a
  silently narrowed result

#### Scenario: Paging is deterministic

- **WHEN** a reader pages through a filtered trail over unchanged data
- **THEN** no event appears on two pages and none is skipped

### Requirement: Audit read authorization fails closed

Reading the audit trail SHALL require an authenticated actor holding `audit.read`. The permission
SHALL be checked after authentication and before the request is validated. A caller without a valid
token SHALL receive the unauthenticated refusal; an authenticated caller without the permission SHALL
receive the forbidden refusal.

Holding a candidate, catalog or document permission SHALL NOT confer the ability to read the trail.
The permission SHALL be granted narrowly: a trail readable by everyone it records is not a control.

#### Scenario: Unauthenticated caller reads the trail

- **WHEN** a caller with no valid token requests the audit trail
- **THEN** the request is refused as unauthenticated and no event data is returned

#### Scenario: Authenticated caller lacks the permission

- **WHEN** an authenticated actor without `audit.read` requests the audit trail
- **THEN** the request is refused as forbidden and no event data is returned

#### Scenario: Refusal precedes validation

- **WHEN** an unauthorized caller supplies a malformed filter
- **THEN** the refusal is the authorization problem, identical to the refusal for a valid filter, and
  no validation detail is returned

#### Scenario: Candidate permission does not confer audit access

- **WHEN** an actor holding every candidate, catalog and document permission but not `audit.read`
  requests the trail
- **THEN** the request is refused as forbidden

#### Scenario: Seeded roles are inspected

- **WHEN** the seeded role definitions are inspected
- **THEN** `audit.read` is held only by the roles documented as governing the installation, and not
  by the general recruiter roles

### Requirement: Auditoría administration screen

The application SHALL provide an administration screen listing audit events, reachable only by
actors holding `audit.read`. It SHALL offer the documented filters, paging, and shall resolve each
actor's display name through the users endpoint rather than storing or receiving it on the event.

An event whose actor is unknown SHALL be rendered as explicitly unknown, and one whose actor is the
system SHALL be rendered as the system — neither as a blank cell, which reads as "nobody" and is a
different claim.

The screen SHALL reach the navigation as an entry in the navigation table with its permission in the
navigation permission list, not as a link added to the layout. All copy SHALL be Spanish, with
correct accents, rendered from the translation resources.

#### Scenario: Administrator opens the screen

- **WHEN** an actor holding `audit.read` opens the Auditoría section
- **THEN** a page of events is listed with their type, subject, actor, outcome and timestamp

#### Scenario: Actor display name is resolved

- **WHEN** an event naming a user actor is rendered
- **THEN** that user's display name is shown, resolved through the users endpoint

#### Scenario: Unknown and system actors are rendered

- **WHEN** an event with no actor and an event with the system actor are rendered
- **THEN** each is labelled distinctly, and neither appears as an empty value

#### Scenario: Screen is reached without permission

- **WHEN** an actor without `audit.read` navigates directly to the Auditoría route
- **THEN** the route guard redirects them, and the API refuses the underlying request independently

#### Scenario: Navigation entry is added as data

- **WHEN** the navigation is inspected
- **THEN** the Auditoría entry is declared in the navigation table with its permission, and no link
  for it has been added to the layout component
