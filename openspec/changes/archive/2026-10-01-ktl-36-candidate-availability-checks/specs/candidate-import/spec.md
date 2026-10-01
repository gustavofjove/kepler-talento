## MODIFIED Requirements

### Requirement: Imported candidates are ordinary candidates

A candidate created by a commit SHALL be subject to the same domain rules as one created through the
candidate endpoints: the same field validation, the same consent and retention handling, and the
same audit event. A commit SHALL NOT bypass a rule that a single candidate write enforces, and SHALL
NOT set a field no candidate write can set. An imported candidate SHALL start with an `unknown`
availability check, because the candidate create operation cannot set one. The import file contract
SHALL NOT contain a `status` or an `availability` column.

A row that would produce a candidate the domain refuses SHALL be rejected with its reason code
rather than loaded in a weakened form.

#### Scenario: Imported candidate is read back

- **WHEN** a candidate created by a commit is read through the candidate endpoint
- **THEN** it carries the same shape and obeys the same rules as a candidate created directly, and
  its availability is `unknown`

#### Scenario: Row would break a domain rule

- **WHEN** a row would produce a candidate that the domain refuses
- **THEN** the row is rejected with a stable reason code and no weakened candidate is created

#### Scenario: Commit is audited

- **WHEN** a commit creates candidates
- **THEN** each creation produces the same audit event a direct creation produces

#### Scenario: File still carries the former columns

- **WHEN** an uploaded file has a `status` or an `availability` column
- **THEN** validation reports the unknown-column structural problem naming that column, and no rows
  are judged
