<!--
Sync Impact Report
Version change: template -> 1.0.0
Modified principles:
- Template placeholders -> I. Personal Data Is Protected By Design
- Template placeholders -> II. Supabase And Existing Corporate Stack Are The Boundary
- Template placeholders -> III. RLS, Roles And Storage Security Are Non-Negotiable
- Template placeholders -> IV. Testable Delivery And Security Evidence
- Template placeholders -> V. Incremental MVP With Traceable Specifications
Added sections:
- Product Scope And Domain Constraints
- Delivery Workflow And Quality Gates
Removed sections:
- None; template placeholders replaced.
Templates requiring updates:
- updated: .specify/templates/plan-template.md
- updated: .specify/templates/spec-template.md
- updated: .specify/templates/tasks-template.md
- not applicable: .specify/templates/commands/*.md does not exist in this Spec Kit install
Follow-up TODOs:
- None.
-->
# RRHH CV Management Constitution

## Core Principles

### I. Personal Data Is Protected By Design
Candidate CV data, contact details, consent dates, review dates, audit data, and
documents are personal data. Every feature MUST minimize exposed data, preserve
consent and retention metadata, support logical deletion, and avoid public or
local file paths for CV documents. Exported data MUST be limited to the fields
needed by the business task and MUST never expose internal storage paths.

### II. Supabase And Existing Corporate Stack Are The Boundary
The application MUST follow the established Angular and Supabase stack described
in the project documents. Supabase is the backend boundary for authentication,
database access, storage, functions, and authorization-sensitive operations.
Access is only a reference and possible migration source; it MUST NOT become an
operational backend. New infrastructure choices require an explicit plan entry
and a documented reason.

### III. RLS, Roles And Storage Security Are Non-Negotiable
All business tables containing personal or operational data MUST have Row Level
Security enabled before being used by the application. Authorization MUST be
enforced in the database and storage layer, not only in the frontend. CV files
MUST live in private storage and be opened only through permission-checked,
time-limited access. Service-role operations MUST be isolated behind server-side
functions and MUST never expose service credentials to the frontend.

### IV. Testable Delivery And Security Evidence
Each delivered slice MUST include evidence that the user journey works and that
security boundaries hold. Plans and tasks MUST include unit, integration, E2E,
SQL/RLS, and storage-policy checks when the slice touches the corresponding
surface. Security and RLS checks MUST fail closed for unauthenticated or
unauthorized users before a feature is considered complete.

### V. Incremental MVP With Traceable Specifications
Work MUST move from specification to plan to tasks to implementation. User
stories MUST be independently testable and ordered so that an MVP can be
validated early: authentication and roles, candidate CRUD, candidate relations,
CV storage, advanced search, export, and controlled import. Every task MUST map
back to a requirement or user story in the active specification.

## Product Scope And Domain Constraints

The product is an internal RRHH application for registering, maintaining,
searching, exporting, and governing candidate CV information. The MVP MUST cover
candidate master data, languages, programs/tools, education, work experience,
skills, CV documents, advanced combined search, export, audit metadata, roles,
and controlled migration from the existing Access database or CSV exports.

The system MUST keep candidates logically removable through inactive/deleted
status instead of destructive deletion for normal operations. Search behavior
MUST ignore empty filters, combine different filter families with AND, support
ANY and ALL semantics for multi-value filters, and avoid duplicate candidates in
results.

## Delivery Workflow And Quality Gates

Before implementation, the active specification MUST state user value, actors,
functional requirements, key entities, assumptions, edge cases, and measurable
success criteria. The plan MUST record the technical stack, data model,
authorization model, storage model, deployment approach, and test strategy.

Implementation MUST follow the existing project conventions where referenced
project files are available. Database migrations MUST be idempotent and include
required grants, RLS policies, and helper functions in the same delivery slice.
Frontend services MUST centralize access to backend capabilities instead of
duplicating authorization logic that belongs in RLS or server-side functions.

## Governance

This constitution supersedes ad-hoc implementation preferences for this
project. Amendments require an update to this file, a Sync Impact Report, and a
review of affected Spec Kit templates and active specifications.

Versioning follows semantic versioning:
- MAJOR for removing or redefining a principle in a way that changes compliance.
- MINOR for adding a principle, mandatory section, or material quality gate.
- PATCH for clarifications that do not change project obligations.

Every plan and review MUST check compliance with the principles above. Any
approved exception MUST be documented in the plan Complexity Tracking section
with the reason, the simpler alternative considered, and the mitigation.

**Version**: 1.0.0 | **Ratified**: 2026-06-28 | **Last Amended**: 2026-06-28
