# Implementation Plan: Gestion de CVs para RRHH

> **Actualizacion de stack (KTL-3, 2026-08-20).** El frontend migro de Angular 21
> a React 19 + Vite + React Router 7. La frontera de backend no cambia: Supabase
> sigue siendo el unico backend y las claves de `localStorage` son identicas. El
> registro de desviacion del principio 2 esta en
> `openspec/changes/migrate-frontend-to-react/design.md`.

**Branch**: `001-gestion-cvs-rrhh` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-gestion-cvs-rrhh/spec.md`

## Summary

Build an internal RRHH web application for secure candidate CV management. The
MVP delivers authenticated role-based access, candidate maintenance, professional
profile enrichment, private CV storage/opening, advanced search, controlled
export, and controlled Access/CSV import. The technical approach follows the
existing corporate React + Supabase pattern, with Supabase RLS and private
Storage as the real authorization boundary.

Current continuation focus: close usability and production-hardening gaps
identified during implementation iterations (see [backlog.md](./backlog.md)).
UX baseline and improvement roadmap are documented in [ux-audit.md](./ux-audit.md).

## Technical Context

**Language/Version**: TypeScript 5.9, SQL/PostgreSQL 17, Deno/TypeScript for
Supabase Edge Functions

**Primary Dependencies**: React 19 function components, React Router 7, Vite,
Tailwind CSS 3, `@ngx-translate`, `@supabase/supabase-js`, Supabase Auth,
Supabase Storage, Supabase Edge Functions

**Storage**: Supabase PostgreSQL for relational data; private Supabase Storage
bucket `candidate-cvs` for PDF CV documents; Access `.accdb`/CSV only as a
migration input and functional reference

**Testing**: Jest 30 with `jest-preset-angular`, Playwright E2E, SQL/RLS
validation scripts, storage-policy validation scripts

**Target Platform**: Internal browser-based SPA served by Nginx unprivileged
1.27 Alpine; Supabase self-hosted in Docker according to the documented project
pattern

**Project Type**: Web application with Supabase backend/BaaS and frontend SPA

**Performance Goals**: Primary RRHH flows complete within normal interactive
web expectations; advanced search returns acceptance-dataset results without
duplicates and supports combined filters; export and import provide progress or
clear completion/error feedback for representative MVP batches

**Constraints**: No service-role key in frontend; no public CV bucket; no Access
runtime dependency; all business tables use RLS; migrations are idempotent and
include required grants; frontend authorization checks are UX aids only and do
not replace RLS

**Scale/Scope**: MVP covers internal RRHH usage, manager/readonly access,
candidate records, related profile entities, document metadata, one principal
CV per candidate, controlled export, and initial Access/CSV migration

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-check after Phase 1 design._

- Personal data protection: PASS. Plan identifies candidate data, consent,
  retention/review dates, audit needs, export limits, logical deletion, and
  private CV document controls.
- Stack boundary: PASS with recorded departure. Plan stays within React 19 + Vite + Supabase + Docker/Nginx
  stack from the source documents. Access is migration/reference only.
- Authorization: PASS. RLS, roles, private Storage, signed URL generation, and
  service-role isolation are explicit design obligations.
- Test evidence: PASS. Jest, Playwright, SQL/RLS, and storage-policy checks are
  planned for the surfaces they cover.
- Traceability: PASS. User stories in `spec.md` map to plan contracts and task
  phases; MVP slices remain independently testable.

## Project Structure

### Documentation (this feature)

```text
specs/001-gestion-cvs-rrhh/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- edge-functions.md
|   |-- rpc-search-candidates.md
|   |-- ui-routes.md
|   `-- export-import.md
|-- checklists/
|   |-- requirements.md
|   `-- security-data.md
`-- tasks.md
```

### Source Code (repository root)

```text
src/
|-- app/
|   |-- core/
|   |   |-- auth/
|   |   |-- guards/
|   |   |-- interceptors/
|   |   |-- layout/
|   |   |-- services/
|   |   `-- supabase/
|   |-- shared/
|   |   |-- components/
|   |   |-- directives/
|   |   |-- models/
|   |   |-- pipes/
|   |   `-- utils/
|   |-- features/
|   |   |-- admin/
|   |   |-- candidates/
|   |   |-- catalogs/
|   |   |-- documents/
|   |   `-- search/
|   |-- app.config.ts
|   `-- app.routes.ts
|-- assets/
|   `-- i18n/
|       |-- es.json
|       `-- en.json
supabase/
|-- functions/
|   |-- candidate-create-signed-cv-url/
|   |-- candidate-export-results/
|   |-- candidate-import-access-csv/
|   `-- candidate-retention-check/
`-- migrations/
tests/
|-- e2e/
|-- integration/
|-- security/
`-- unit/
scripts/
|-- check-rls.js
`-- check-storage-policies.js
```

**Structure Decision**: Use a domain-oriented React SPA under `src/app`, with
Supabase migrations and Edge Functions under `supabase/`. Tests are grouped by
risk surface: unit, integration, E2E, and security.

## Phase 0: Research Summary

See [research.md](./research.md). All technical unknowns from this plan are
resolved by documented project constraints.

## Phase 1: Design Summary

See [data-model.md](./data-model.md) for entities, relationships, validation
rules, and state transitions.

See [contracts/](./contracts/) for the application interface contracts:

- [rpc-search-candidates.md](./contracts/rpc-search-candidates.md)
- [edge-functions.md](./contracts/edge-functions.md)
- [ui-routes.md](./contracts/ui-routes.md)
- [export-import.md](./contracts/export-import.md)

See [quickstart.md](./quickstart.md) for validation scenarios that prove the MVP
without implementing code in this phase.

## Post-Design Constitution Check

- Personal data protection: PASS. Data model records consent/review metadata,
  logical deletion, audit events, document metadata, and controlled export.
- Stack boundary: PASS. Contracts keep Supabase as backend boundary and React
  as frontend boundary.
- Authorization: PASS. Contracts include RLS, storage denial, signed URL, role,
  and Edge Function authorization expectations.
- Test evidence: PASS. Quickstart and tasks require unit, E2E, SQL/RLS, and
  storage checks before implementation is accepted.
- Traceability: PASS. Tasks are grouped by user story and map back to FR/SC
  identifiers and contracts.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --------- | ---------- | ------------------------------------ |
| None      | N/A        | N/A                                  |

## Continuation Execution Waves (Post-MVP)

### Wave 1 - Operations And Security Closure

- Complete import/export operational flow (`dry-run -> commit`, history, error
  artifacts).
- Complete missing integration/security suites (RLS auth/storage and Edge
  Functions).
- Enforce non-bypassable governance rules in admin and catalog domains.

### Wave 2 - Productivity UX

- Upgrade candidate list into an operations center (quick filters, sorting,
  pagination, multi-action safeguards).
- Add saved searches and last-search restore in advanced search.
- Standardize explicit confirmation patterns for sensitive actions.

### Wave 3 - Production Readiness

- Structured observability (`request_id`, error catalog, correlated audit logs).
- Staging smoke automation and runbook validation.
- Backup/restore proof and go-live checklist sign-off.

### Wave 4 - UX And Accessibility Improvement Program

- Execute UX-1 quick wins from [ux-audit.md](./ux-audit.md) on app shell,
  candidate operations, catalogs, and import/search feedback loops.
- Standardize confirmation and status interaction patterns in high-risk actions.
- Expand keyboard-first and screen-reader compatibility checks for critical
  workflows.

## Exit Conditions For Continuation

- All open Phase 11+ tasks in [tasks.md](./tasks.md) completed.
- `FR-026`..`FR-031` and `SC-010`..`SC-012` validated with evidence.
- Product owner and RRHH operations sign-off recorded.
