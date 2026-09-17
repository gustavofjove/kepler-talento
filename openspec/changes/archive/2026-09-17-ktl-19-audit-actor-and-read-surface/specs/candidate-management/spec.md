## MODIFIED Requirements

### Requirement: Candidate change auditing without personal data

Every candidate create, update, status change, logical removal, restoration, relation change and
document metadata change SHALL record an audit event carrying the acting identity, the affected
candidate identifier, the kind of change, its outcome and the request correlation identifier.

The acting identity SHALL be the actor's **internal user identifier**, never an email address, a
display name or the external subject issued by the identity provider.

Reading one candidate's record SHALL also record an audit event, because it is a read that
identifies a specific person. Searching and listing candidates SHALL NOT be audited.

Audit events SHALL NOT record candidate field values, which are personal data. An audit event
SHALL be recorded only for a change that was actually applied.

#### Scenario: Change is audited

- **WHEN** a candidate is created, updated, removed, restored, or has a relation or document
  collection changed
- **THEN** an audit event records the actor, the candidate identifier, the kind of change, the
  outcome and the correlation identifier

#### Scenario: Audit event carries no personal data

- **WHEN** an audit event for a candidate change is inspected
- **THEN** it contains no name, contact detail, note, or other candidate field value

#### Scenario: Refused change is not audited as applied

- **WHEN** a candidate write is refused for validation, authorization or concurrency
- **THEN** no audit event describing an applied change is stored

#### Scenario: Acting identity is inspected

- **WHEN** the acting identity on a candidate audit event is inspected
- **THEN** it is an internal user identifier, and it is not an email address, a display name or an
  external subject identifier

#### Scenario: Candidate record is read

- **WHEN** a permitted actor reads one candidate's record
- **THEN** an audit event records the actor, the candidate identifier and the read event type

#### Scenario: Candidate list is read

- **WHEN** a permitted actor searches or lists candidates
- **THEN** no audit event is recorded for that search or listing
