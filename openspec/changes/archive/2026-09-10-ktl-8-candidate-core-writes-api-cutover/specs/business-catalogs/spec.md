## ADDED Requirements

### Requirement: Referenced values remain resolvable

A catalog value that is referenced by a candidate relation SHALL remain resolvable. Such a
value SHALL NOT be renamed into a collision with another value of its family and SHALL NOT be
physically removed. A referenced value SHALL be deactivatable by any permitted caller, whether
through the API or the administration screen, so that it stops being offered for new selections
while existing candidate records keep their meaning.

No part of the product SHALL consult candidate data in order to decide whether to offer or
permit a deactivation.

#### Scenario: API deactivates a value in use

- **WHEN** a permitted caller asks the API to deactivate a value that candidate records
  reference
- **THEN** the deactivation succeeds and the referencing candidate records still resolve the
  value

#### Scenario: Administration screen deactivates a value in use

- **WHEN** an administrator deactivates a value on the administration screen while candidate
  records reference it
- **THEN** the deactivation succeeds, the value stops being offered for new selections, and no
  refusal message is shown

#### Scenario: Value not in use is deactivated from the screen

- **WHEN** an administrator deactivates a value that no candidate record references
- **THEN** the deactivation succeeds and the value stops being offered for new selections

#### Scenario: Value in use is renamed into a collision

- **WHEN** an authorized administrator renames a referenced value to a name already held by
  another value of the same family
- **THEN** the request is rejected and the referenced value keeps its name

#### Scenario: Deactivated value is still displayed on existing records

- **WHEN** a candidate record referencing a deactivated value is read
- **THEN** the value resolves and is displayed, even though it is no longer offered for new
  selections

## REMOVED Requirements

### Requirement: Values in use are protected

**Reason**: This requirement carried a transitional screen-level refusal, and said so: the
administration screen refused to deactivate a value it could see was in use because candidate
records lived in browser storage where the API could not evaluate the rule, and the requirement
stated the refusal "is removed once the API can evaluate the rule itself, after which the API's
permission to deactivate becomes the product's behavior". This change moves candidate relations
to the API, so the condition the requirement named as its own end has arrived. It is replaced by
`Referenced values remain resolvable`, which keeps every protection that was not transitional —
resolvability, no rename-into-collision, no physical removal — and drops only the refusal.

**Migration**: Administrators can now deactivate a catalog value that candidates reference; the
Spanish message `No se puede desactivar: el valor esta en uso por candidatos.` no longer appears.
The referencing candidate records are unaffected and continue to resolve and display the value,
because deactivation has never been deletion. No stored data changes.
