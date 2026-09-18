# Business Catalogs Specification

## Purpose

Defines the server-owned business vocabulary that candidate records and search criteria draw
from: the catalog families, the lifecycle of a catalog value, the uniqueness and referential
rules that keep the vocabulary coherent, and the authorization and audit obligations that
apply to changing it.

## Requirements

### Requirement: Server-owned catalog vocabulary

Catalog values SHALL be stored by the application backend and served through the API. The
browser SHALL NOT be the system of record for catalog values, and the application SHALL NOT
fall back to a locally generated vocabulary when the API is unavailable.

#### Scenario: Catalog values are read from the API

- **WHEN** an authorized user opens a screen that presents catalog values
- **THEN** the values shown come from the API, and the same values are visible to every user
  of the deployment

#### Scenario: Catalog values are unavailable

- **WHEN** the API cannot be reached or returns an error while catalog values are being loaded
- **THEN** the screen reports the failure to the user and presents no catalog options, rather
  than substituting a locally generated default vocabulary

### Requirement: Catalog families and item shape

The system SHALL organize catalog values into the ten business families used by candidate
records and search: languages, programs, skills, language levels, program levels, skill
levels, education types, education statuses, sectors, and tags. Each catalog item SHALL carry a
stable identifier, a code, a Spanish name, an optional English name, a position within its
family, and an active flag.

The tags family SHALL have the same item shape as every other family. A tag SHALL NOT carry a
level, a status, or any attribute the other families do not have.

#### Scenario: Family is listed

- **WHEN** an authorized user requests the values of a family
- **THEN** the API returns that family's items in their configured order, each with its
  identifier, code, Spanish name, optional English name, position, and active flag

#### Scenario: Unknown family is requested

- **WHEN** a caller requests a family that is not one of the ten business families
- **THEN** the API rejects the request with a stable validation code and returns no items

#### Scenario: Tags family is administered like any other

- **WHEN** an authorized administrator creates, renames, reorders, deactivates or reactivates a
  value of the tags family
- **THEN** the operation behaves exactly as it does for any other family, through the same
  operations, with no additional field to supply

### Requirement: Catalog value update

An authorized administrator SHALL be able to change a value's Spanish name, code, and English
name. Updating a value SHALL NOT change its identifier, its position, or its active flag.

#### Scenario: Value is renamed

- **WHEN** an authorized administrator submits a new Spanish name for an existing value
- **THEN** the value is stored with the new name and retains its identifier, position, and
  active flag

#### Scenario: Value does not exist

- **WHEN** an update targets an identifier that does not exist in the family
- **THEN** the API returns a not-found problem with a stable code and the Spanish message
  "No se encontró el elemento del catálogo."

#### Scenario: Value changed since it was read

- **WHEN** an update is submitted against a version of the value that is no longer current
- **THEN** the API returns a conflict problem with a stable code and the stored value is not
  overwritten

### Requirement: Catalog family reordering

An authorized administrator SHALL be able to change the order in which a family's values are
presented. Reordering SHALL apply to the family as a whole in one operation and SHALL leave
the family with consecutive positions and no duplicates or gaps.

#### Scenario: Value is moved within its family

- **WHEN** an authorized administrator reorders a family
- **THEN** subsequent listings return the family in the new order with consecutive positions

#### Scenario: Reorder does not cover the family

- **WHEN** a reorder request omits a value of the family or names a value from another family
- **THEN** the request is rejected with a stable validation code and the existing order is
  unchanged

#### Scenario: Concurrent reorders

- **WHEN** two reorder requests for the same family are submitted concurrently
- **THEN** the resulting order is one of the two submitted orders in full, never an interleaved
  mixture of them

### Requirement: Values are deactivated, never deleted

A catalog value SHALL be removable from use only by being deactivated. The system SHALL NOT
expose any operation that physically deletes a catalog value. A deactivated value SHALL remain
readable by administration screens and SHALL be reactivatable.

#### Scenario: Value is deactivated

- **WHEN** an authorized administrator deactivates a value
- **THEN** the value stops appearing in selection screens, remains visible on the
  administration screen marked inactive, and is still present in the database

#### Scenario: Value is reactivated

- **WHEN** an authorized administrator activates a previously deactivated value
- **THEN** the value appears again in selection screens in its family position

#### Scenario: No physical delete exists

- **WHEN** the published API contract is inspected
- **THEN** it exposes no operation that physically removes a catalog value

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

### Requirement: Catalog authorization fails closed

Reading catalog values SHALL require an authenticated actor holding the catalog read
capability. Creating, updating, reordering, and changing the activation of catalog values SHALL
require an authenticated actor holding the catalog management capability. Authorization SHALL
be enforced by the API; hiding or disabling user-interface controls is not the control.

#### Scenario: Unauthenticated caller

- **WHEN** a caller with no resolved actor invokes any catalog operation
- **THEN** the request is denied, no catalog data is returned, and no change is stored

#### Scenario: Reader attempts a change

- **WHEN** an authenticated actor holding only the catalog read capability attempts to create,
  update, reorder, activate, or deactivate a value
- **THEN** the request is denied and no change is stored

#### Scenario: Manager makes a change

- **WHEN** an authenticated actor holding the catalog management capability submits a valid
  change
- **THEN** the change is applied

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

### Requirement: Default families are seeded at deployment

The ten families SHALL be populated with their default Spanish values by an explicit,
idempotent deployment action. The application SHALL NOT create catalog values implicitly at
runtime when a family is found empty.

A seeded value's code SHALL be derived from its Spanish name by the same normalization the
catalog uses for its own uniqueness. Where that derivation cannot distinguish two names within
one family, the seed SHALL carry an explicit code for those values rather than relying on
derivation. Seeded codes SHALL be unique within their family, and seeded Spanish names SHALL be
unique within their family once trimmed, case-folded, and compared without accents — the same
comparison the family's stored uniqueness uses.

Unlike a value created through the API, a seeded value SHALL NOT have its code silently
uniquified: the seed is authored, so a collision is an authoring error to be surfaced, not a
condition to be resolved automatically.

#### Scenario: Seed runs on an empty deployment

- **WHEN** the deployment seed runs against a database with no catalog values
- **THEN** the ten families are created with their default Spanish values, accents intact, in
  their documented order, all active

#### Scenario: Seed runs again

- **WHEN** the deployment seed runs against a database that already holds catalog values
- **THEN** it makes no change, and administrator edits made since the previous seed are
  preserved

#### Scenario: Tags family is seeded on an existing deployment

- **WHEN** the deployment seed runs against a database that already holds the other families but
  no tags
- **THEN** the default tags are created and no value of any other family is altered

#### Scenario: Family is emptied by deactivation

- **WHEN** every value of a family has been deactivated
- **THEN** selection screens present that family as having no options, and no default values
  reappear

#### Scenario: Two seeded names derive the same code

- **WHEN** a family's default vocabulary holds two Spanish names that derive the same code,
  because they differ only in characters the derivation folds away
- **THEN** each carries an explicit distinct code in the seed, both values are created, and
  neither displaces the other

#### Scenario: Default vocabulary contains a collision

- **WHEN** the default vocabulary of a family holds two values with the same code, or two
  Spanish names equal under trimmed, case-folded, accent-insensitive comparison
- **THEN** the collision is detected before deployment rather than as a failed insert against
  the database
