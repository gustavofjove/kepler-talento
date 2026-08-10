# Contract: Export And Import

## Export Contract

### Purpose

Allow authorized RRHH users to export search results for internal work while
protecting candidate personal data and document storage details.

### Formats

- CSV
- XLSX if project dependencies and corporate pattern support it during planning
  or implementation

### Operational Constraints

- Export requests are asynchronous when row count is high and must return
  progress status.
- Default synchronous mode is acceptable for small result sets.
- Maximum rows and maximum file size for generated exports must be explicitly
  configured per environment.
- MVP limits for local/staging:
  - export row limit: `1000`
  - import row limit per batch: `2000`
  - max import files per request: `8`

### Default Field Set

- First name
- Last name
- Phone
- Email when permitted
- Candidate status
- Location
- Province
- Reception date
- Last update date
- CV available indicator

### Forbidden Fields

- Internal storage paths
- Service credentials
- Permanent private document URLs
- Raw audit payloads
- Unrequested personal data outside the selected field set

### Required Events

Each export records requester, timestamp, filters, field set, row count, status,
and format.

### Export History Requirement

- Each export request MUST appear in user-visible batch history with status,
  timestamps, and row count.

### Export Error Cases

- User without `export_candidates` permission -> `FORBIDDEN`
- Invalid field set -> `VALIDATION_ERROR`
- Requested row count above configured limit -> `VALIDATION_ERROR`
- Unexpected generation failure -> `INTERNAL_ERROR` with request id

## Import Contract

### Purpose

Load depurated data from Access or CSV exports into the normalized candidate
model.

### Accepted Source Classes

- Candidate master data CSV
- Catalog CSVs
- Candidate-language relations CSV
- Candidate-program relations CSV
- Education CSV
- Experience CSV
- Skill CSV
- Document metadata CSV

### Minimum CSV Contract

The import process requires a header row and UTF-8 encoding.

- Candidates CSV required columns: `external_id`, `first_name`, `last_name`
- Candidate relations required columns: candidate reference + catalog/value
  reference
- Dates must use ISO format (`YYYY-MM-DD`) where provided
- Unknown columns are ignored unless strict mode is enabled

### Processing Order

1. Validate file structure.
2. Normalize catalog values.
3. Load catalogs.
4. Load candidates.
5. Load candidate relations.
6. Load document metadata if available.
7. Produce summary and row-level errors.
8. Validate representative candidates with RRHH.

### Dry-Run To Commit Contract

- Dry-run returns a `batch_id` and validation summary without business writes.
- Commit requires explicit user action referencing `batch_id`.
- Commit must fail if payload differs from the validated dry-run batch.

### Error Handling

- Invalid rows are recorded with row number, source file, field, and reason.
- Valid rows may be loaded while invalid rows are reported.
- Dry-run mode validates without writing business records.
- Imports do not create public document links.
- Exceeding configured limits MUST return `VALIDATION_ERROR` with a stable code/message pair.

### Idempotency And Retry

- Import requests SHOULD provide an idempotency key.
- Retries with the same key and same source files MUST return the existing batch
  result rather than duplicating records.
- Retries with the same key but different payload MUST fail with `CONFLICT`.

### Import History Requirement

- Import batches MUST be queryable in history with actor, source name, status,
  error count, and downloadable error artifact reference.

## Traceability

US6, US7; FR-019, FR-020, FR-021, FR-022, FR-023, SC-006, SC-007, SC-008.
