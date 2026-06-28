# Contract: search_candidates RPC

## Purpose

Provide advanced candidate search with empty-filter ignoring, cumulative filter
matching, ANY/ALL semantics for languages and programs, duplicate prevention,
and RLS-aware execution.

## Interface

`search_candidates(filters jsonb)`

## Request Shape

```json
{
  "text": "naval",
  "status_ids": ["uuid"],
  "availability_ids": ["uuid"],
  "language_ids": ["uuid1", "uuid2"],
  "language_mode": "ALL",
  "program_ids": ["uuid3", "uuid4"],
  "program_mode": "ANY",
  "education_type_ids": [],
  "sector_ids": [],
  "skill_ids": [],
  "min_years_experience": 3,
  "received_from": "2025-01-01",
  "received_to": "2026-12-31",
  "has_cv": true,
  "include_inactive": false
}
```

## Response Shape

```json
[
  {
    "candidate_id": "uuid",
    "first_name": "Juan",
    "last_name": "Perez",
    "phone": "+34...",
    "email": "optional@example.com",
    "status_name": "Disponible",
    "primary_cv_document_id": "uuid-or-null",
    "updated_at": "2026-06-28T10:00:00Z"
  }
]
```

## Rules

- Empty string, null, and empty-array filters are ignored.
- Different filter families are combined cumulatively.
- `language_mode = ANY` means candidate has at least one selected language.
- `language_mode = ALL` means candidate has all selected languages.
- `program_mode = ANY` means candidate has at least one selected program.
- `program_mode = ALL` means candidate has all selected programs.
- Results contain each candidate at most once.
- Default search excludes logically inactive candidates.
- The RPC must not bypass RLS for normal search.
- The response may include `email` only if permitted by role and field policy.
- The response must not include storage paths or permanent document URLs.

## Error Conditions

- Invalid mode values return a controlled validation error.
- Invalid UUID filters return a controlled validation error.
- Unauthorized callers receive no protected data.

## Traceability

FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, SC-002, SC-003.
