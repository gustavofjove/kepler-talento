# Data Model: Gestion de CVs para RRHH

## Overview

The data model normalizes candidate CV information that previously existed in
Access-like forms and tables. Business data is protected by RLS and uses logical
deactivation for normal operations.

## Shared Rules

- Business tables use UUID primary keys.
- Business tables include `created_at`, `updated_at`, `created_by`, and
  `updated_by` where applicable.
- Personal-data tables enable RLS before application use.
- Normal user removal is logical through `is_active` or `deleted_at`.
- Migrations are idempotent and include explicit grants for Supabase roles.
- Storage paths are internal metadata, never user-facing links.

## Entities

### UserProfile

Represents an authenticated application user profile.

**Fields**: `id`, `display_name`, `email`, `role`, `is_active`,
`mfa_required`, `created_at`, `updated_at`.

**Relationships**: Belongs to one `Role` by role identifier/name.

**Validation**:

- Email is unique when present.
- Inactive profiles cannot access protected candidate data.
- Users cannot lower their own MFA requirement.

### Role

Represents functional permissions for application users.

**Fields**: `id`, `name`, `description`, `permissions`, `is_system`,
`is_technical`, `is_manager`, `all_companies`, `created_at`, `updated_at`.

**Initial roles**: `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly`,
`system_admin`.

**Validation**:

- System roles cannot be deleted or renamed.
- Permission checks use stable permission strings/flags rather than UI labels.

### Candidate

Represents a person whose CV is managed by RRHH.

**Fields**: `id`, `first_name`, `last_name`, `phone`, `email`, `location`,
`province`, `country`, `availability_id`, `status_id`, `source_id`, `notes`,
`received_at`, `consent_at`, `review_due_at`, `is_active`, `deleted_at`,
`created_at`, `updated_at`, `created_by`, `updated_by`.

**Relationships**:

- Belongs to candidate status, availability, and source catalogs.
- Has many language, program, education, experience, skill, document, and audit
  records.

**Validation**:

- `first_name` and `last_name` are required.
- Candidate duplicates are not merged automatically.
- Logical deactivation sets inactive/deleted state without destroying history.

**State transitions**:

- Draft/active candidate -> active searchable candidate.
- Active candidate -> logically inactive candidate.
- Candidate review date can become overdue and appear in retention checks.

### Catalog

Represents controlled lists used by RRHH.

**Catalog tables**: candidate statuses, availability, sources, languages,
language levels, programs, program levels, education types, sectors, skills,
skill levels, document types.

**Common fields**: `id`, `code`, `name_es`, `name_en`, `sort_order`,
`is_active`, `created_at`, `updated_at`.

**Validation**:

- `code` is unique within the catalog.
- Inactive catalog values remain available for historical records.

### CandidateLanguage

Represents one language attached to a candidate.

**Fields**: `id`, `candidate_id`, `language_id`, `level_id`, `certification`,
`notes`, `created_at`, `updated_at`, `created_by`, `updated_by`.

**Validation**:

- A candidate cannot have the same language twice.

### CandidateProgram

Represents one program or tool attached to a candidate.

**Fields**: `id`, `candidate_id`, `program_id`, `level_id`,
`years_experience`, `notes`, `created_at`, `updated_at`, `created_by`,
`updated_by`.

**Validation**:

- A candidate cannot have the same program twice.
- Years of experience cannot be negative.

### CandidateEducation

Represents academic or complementary training.

**Fields**: `id`, `candidate_id`, `education_type_id`, `degree`, `specialty`,
`institution`, `end_year`, `status`, `notes`, `created_at`, `updated_at`,
`created_by`, `updated_by`.

**Validation**:

- `degree` is required.
- End year must be plausible when provided.

### CandidateExperience

Represents work history.

**Fields**: `id`, `candidate_id`, `company`, `position`, `sector_id`,
`functions`, `start_date`, `end_date`, `is_current`, `years_experience`,
`notes`, `created_at`, `updated_at`, `created_by`, `updated_by`.

**Validation**:

- End date cannot be before start date.
- Current experience should not require an end date.
- Years of experience cannot be negative.

### CandidateSkill

Represents a skill or competency attached to a candidate.

**Fields**: `id`, `candidate_id`, `skill_id`, `level_id`, `notes`,
`created_at`, `updated_at`, `created_by`, `updated_by`.

**Validation**:

- A candidate should not have duplicate active skill entries for the same skill.

### CandidateDocument

Represents private document metadata for a candidate.

**Fields**: `id`, `candidate_id`, `document_type_id`, `storage_bucket`,
`storage_path`, `original_filename`, `mime_type`, `size_bytes`, `is_primary`,
`uploaded_at`, `uploaded_by`, `file_hash`, `created_at`.

**Validation**:

- `storage_bucket` and `storage_path` are unique together.
- Only one primary CV is allowed per candidate.
- MVP accepts `application/pdf` for CV files.
- Storage location remains private and is not exported as a user-facing value.

### CandidateAuditLog

Represents important changes and operations.

**Fields**: `id`, `candidate_id`, `action`, `entity_name`, `entity_id`,
`old_data`, `new_data`, `created_at`, `created_by`.

**Validation**:

- Create, update, document, export, import, and logical delete operations should
  produce enough audit detail for MVP support and review.

### ImportBatch

Represents a controlled import from Access/CSV.

**Fields**: `id`, `source_name`, `source_type`, `status`, `total_rows`,
`loaded_rows`, `error_rows`, `started_at`, `finished_at`, `created_by`,
`summary`.

**Relationships**: Has many import errors.

**Validation**:

- Invalid rows are recorded with row number and reason.
- A partial valid import can complete with warnings.

### ImportError

Represents one failed row or validation issue in an import batch.

**Fields**: `id`, `batch_id`, `row_number`, `entity_name`, `raw_data`,
`error_code`, `message`, `created_at`.

### ExportEvent

Represents a controlled result export.

**Fields**: `id`, `requested_by`, `requested_at`, `format`, `filters`,
`row_count`, `field_set`, `status`.

**Validation**:

- Exported fields must match the permitted field set.
- Internal storage paths and service secrets are never included.

## Search Filter Model

`SearchFilterSet` is a transient user-selected object for `search_candidates`.

**Fields**: `text`, `status_ids`, `availability_ids`, `language_ids`,
`language_mode`, `program_ids`, `program_mode`, `education_type_ids`,
`sector_ids`, `skill_ids`, `min_years_experience`, `received_from`,
`received_to`, `has_cv`, `include_inactive`.

**Validation**:

- Empty arrays and empty scalar filters are ignored.
- `language_mode` and `program_mode` accept `ANY` or `ALL`.
- Search results return unique candidates.

## Authorization Model

Permission strings are grouped by domain. Required MVP permissions include:

- `view_candidates`
- `create_candidates`
- `edit_candidates`
- `delete_candidates`
- `view_all_candidates`
- `download_candidate_documents`
- `upload_candidate_documents`
- `export_candidates`
- `import_candidates`
- `manage_catalogs`
- `manage_users`
- `manage_roles`

RLS policies use helper functions for current profile, current role, permission
checks, and any tenant/company rules required by the existing project pattern.

## Storage Model

Bucket: `candidate-cvs`

Path convention: `{candidate_id}/{document_id}/{sanitized_filename}.pdf`

Access rules:

- Bucket is private.
- Authenticated users with upload permission can upload candidate CVs.
- Authenticated users with download permission can receive controlled temporary
  access.
- Direct public URLs are forbidden.

## Lifecycle Summary

- Candidate creation records audit metadata.
- Candidate update records audit metadata.
- Candidate logical deactivation removes the candidate from active default
  views while preserving history.
- Candidate document upload creates document metadata and storage object.
- CV opening validates authorization before returning temporary access.
- Import batch validates source rows, loads valid data, and records row errors.
- Export event records who exported, when, selected filters, and field set.
