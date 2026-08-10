# Contract: Edge Functions

## Shared Function Rules

- All requests use `Authorization: Bearer <access_token>` and `Content-Type: application/json`.
- Require an authenticated caller except cron-only retention jobs.
- Validate input payloads before privileged operations.
- Load caller profile and permissions server-side.
- Use service-role access only inside the function.
- Never return service credentials, storage paths as public links, or raw
  internal errors.
- Return controlled JSON errors with stable error codes.
- Record audit events for privileged or data-exporting operations.

## Shared Error Contract

All functions return business-safe errors with this envelope:

```json
{
  "error": {
    "code": "FORBIDDEN",
    "message": "Operacion no autorizada.",
    "request_id": "uuid",
    "details": {}
  }
}
```

Standard error codes for MVP:

- `UNAUTHENTICATED` -> missing/invalid token
- `FORBIDDEN` -> authenticated but without permission/scope
- `VALIDATION_ERROR` -> invalid payload/fields
- `NOT_FOUND` -> referenced entity does not exist or is not visible
- `CONFLICT` -> duplicate/idempotency conflict
- `RATE_LIMITED` -> request throttled
- `INTERNAL_ERROR` -> unexpected server error (without stack trace)

All responses include `request_id` for log correlation.

## candidate-create-signed-cv-url

**Purpose**: Return temporary or controlled access for a candidate CV document.

**Request**

```json
{
  "document_id": "uuid"
}
```

**Success Response**

```json
{
  "url": "temporary-url",
  "expires_in_seconds": 300,
  "document_id": "uuid"
}
```

**Rules**

- Caller must be authenticated and active.
- Caller must have document download permission for the candidate.
- Function validates document metadata before issuing access.
- Response must not expose permanent storage path.
- Signed URL expiration MUST be configurable with secure default <= 300 seconds.
- Access attempts MUST be audit logged with outcome.

## candidate-import-access-csv

**Purpose**: Import depurated Access/CSV data and record load errors.

**Request**

```json
{
  "source_name": "access-export-2026-06-28",
  "dry_run": true,
  "files": [{ "kind": "candidates", "storage_reference": "import-batches/.../candidates.csv" }]
}
```

**Success Response**

```json
{
  "batch_id": "uuid",
  "status": "validated",
  "total_rows": 100,
  "loaded_rows": 95,
  "error_rows": 5
}
```

**Rules**

- Caller must have import permission.
- Dry run validates without committing business records.
- Import maps catalogs before candidate relations.
- Row errors include row number and reason.
- Import request MUST support an idempotency key to avoid duplicate batch loads
  when retries occur.
- Function MUST return per-file validation summary (rows read, valid rows,
  invalid rows) in dry-run and commit modes.

## candidate-export-results

**Purpose**: Generate controlled tabular exports from a search result set.

**Request**

```json
{
  "filters": {},
  "format": "csv",
  "field_set": "rrhh-default"
}
```

**Success Response**

```json
{
  "export_id": "uuid",
  "download_url": "temporary-url",
  "row_count": 42,
  "expires_in_seconds": 300
}
```

**Rules**

- Caller must have export permission.
- Export fields are limited by field set and role.
- Export must not include storage paths or permanent CV links.
- Export operation records an audit/export event.
- Temporary download URL expiration MUST be configurable with secure default <=
  300 seconds.
- Export payload MUST enforce a maximum row limit per request and return
  `VALIDATION_ERROR` when exceeded.

## candidate-retention-check

**Purpose**: Identify candidates/CVs with overdue review dates.

**Request**

```json
{
  "as_of": "2026-06-28"
}
```

**Success Response**

```json
{
  "as_of": "2026-06-28",
  "overdue_count": 10,
  "candidate_ids": ["uuid"]
}
```

**Rules**

- Cron execution uses a shared secret or equivalent server-side protection.
- Manual execution requires technical/admin permission.
- The function does not delete candidate data automatically in MVP.
- Function returns deterministic status for empty overdue sets (`overdue_count =
0`, empty list).

## Traceability

FR-001, FR-002, FR-011, FR-012, FR-019, FR-020, FR-021, FR-022, SC-004,
SC-005, SC-006, SC-007.
