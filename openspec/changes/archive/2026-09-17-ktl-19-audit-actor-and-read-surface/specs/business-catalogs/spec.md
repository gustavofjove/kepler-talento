## MODIFIED Requirements

### Requirement: Catalog changes are audited

Creating, updating, reordering, and changing the activation of a catalog value SHALL record an
audit event identifying the acting actor, the affected family and value, the kind of change,
and the correlation identifier of the request. Reads SHALL NOT be audited.

The acting actor SHALL be recorded as the actor's **internal user identifier**, never an email
address, a display name or the external subject issued by the identity provider. A change made by a
system process rather than a person SHALL record the system actor.

#### Scenario: Change is audited

- **WHEN** an authorized administrator creates, updates, reorders, or changes the activation of
  a catalog value
- **THEN** an audit event is recorded with the actor, family, affected value, change kind, and
  request correlation identifier

#### Scenario: Rejected change is not audited as applied

- **WHEN** a catalog change is rejected by validation, authorization, or conflict
- **THEN** no audit event describing an applied change is recorded

#### Scenario: Acting actor is inspected

- **WHEN** the acting actor on a catalog audit event is inspected
- **THEN** it is an internal user identifier or the system actor, and it is not an email address, a
  display name or an external subject identifier
