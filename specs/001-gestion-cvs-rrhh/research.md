# Research: Gestion de CVs para RRHH

## Decision: Use the existing Angular 21 + Supabase stack

**Rationale**: The project documents define Angular 21, TypeScript 5.9,
standalone components, RxJS, Angular CDK, Tailwind CSS 3, `@ngx-translate`, and
`@supabase/supabase-js` as mandatory. Reusing the existing corporate pattern
reduces integration risk and preserves consistency with the referenced
KeplerDesk/Kepler_tickets project.

**Alternatives considered**: A separate custom backend or different frontend
framework was rejected because the constitution and source documents require
Supabase as backend boundary and Angular as frontend stack.

## Decision: Treat Supabase RLS as the security boundary

**Rationale**: Both source documents state that frontend checks are not a real
security barrier. Business tables with personal data must enable RLS, use helper
authorization functions, and include grants in migrations. This satisfies the
constitution's fail-closed security requirement.

**Alternatives considered**: Frontend-only role checks were rejected because they
can hide or show UI but cannot protect data. Inline duplicated RLS logic was
rejected in favor of shared helper functions.

## Decision: Use private Storage and signed/controlled CV access

**Rationale**: CVs contain sensitive personal data. The `candidate-cvs` bucket
must be private, initially accept PDF files, and expose documents only through a
permission-checked temporary or controlled URL flow.

**Alternatives considered**: Public buckets, permanent links, local file paths,
or exposing `storage_path` to users were rejected by the specification and
constitution.

## Decision: Centralize advanced search in a database RPC

**Rationale**: Search requires combined filters, empty-filter handling, ANY/ALL
matching for languages and programs, RLS awareness, and duplicate prevention.
A `search_candidates(filters jsonb)` RPC keeps that logic testable and avoids
fragile frontend query composition.

**Alternatives considered**: Building dynamic search entirely in the frontend
was rejected because it duplicates domain logic, makes RLS-aware testing harder,
and resembles the Access-style dynamic queries the new application must replace.

## Decision: Use Edge Functions for privileged operations

**Rationale**: Signed CV URL generation, CSV import, controlled export, user
administration, MFA reset, and retention checks may require privileged
server-side behavior. Edge Functions isolate service-role access and allow
authorization checks before privileged operations.

**Alternatives considered**: Calling privileged Supabase APIs directly from the
frontend was rejected because service-role keys must never be exposed.

## Decision: Use controlled Access/CSV migration, not live Access integration

**Rationale**: Access is a functional reference and possible initial source. The
new normalized model must correct orphan data, duplicate fields, mixed IDs/text,
and weak relationships. Import must validate, report errors, and allow RRHH
sample validation.

**Alternatives considered**: Using Access as a production backend was rejected
by the source documents and constitution. Blind bulk import without validation
was rejected because it would preserve known source-data risks.

## Decision: Use self-hosted Docker pattern for planning baseline

**Rationale**: The integration guide identifies self-host Docker as the pattern
used by the existing project, with idempotent migrations, explicit grants, and
volume-mounted Edge Functions. The frontend is served via Nginx unprivileged and
the documented compose port.

**Alternatives considered**: Supabase Cloud remains possible only as a later
explicit deployment decision. It is not the baseline because the project
documents point to the existing self-host pattern.

## Decision: Include security evidence in task generation

**Rationale**: The constitution requires evidence that security boundaries hold.
Tasks must include Jest, Playwright, SQL/RLS checks, storage-policy checks, and
negative authorization scenarios for the relevant slices.

**Alternatives considered**: Deferring all tests to a final hardening phase was
rejected because it would weaken independent story validation and increase
security rework risk.
