# Tasks: Gestion de CVs para RRHH

**Input**: Design documents from `specs/001-gestion-cvs-rrhh/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Required for this feature because the specification and constitution require evidence for user journeys, personal data protection, authorization, storage, search, export, and migration.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

**Continuation Input**: Prioritized continuation backlog in [backlog.md](./backlog.md).

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the project structure, tooling, and baseline configuration needed by all stories.

- [x] T001 Create Angular/Supabase project structure from plan in `src/app`, `supabase/migrations`, `supabase/functions`, `tests`, and `scripts`
- [x] T002 Align package scripts and dependencies with the referenced Angular 21/Supabase stack in `package.json`
- [x] T003 Configure Angular application bootstrap, routing shell, and provider layout in `src/app/app.config.ts` and `src/app/app.routes.ts`
- [x] T004 [P] Configure Tailwind, ESLint, Prettier, Husky, lint-staged, Jest, and Playwright project files at repository root
- [x] T005 [P] Create translation file skeletons in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [x] T006 [P] Create runtime configuration contract for frontend env values in `src/app/core/services/app-config.model.ts`
- [x] T007 Create Docker frontend build/runtime files aligned to Node 22 Alpine and Nginx unprivileged in `Dockerfile.frontend` and `docker-compose.frontend.yml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the data, auth, RLS, storage, and service foundations that all user stories depend on.

**Critical**: No user story work can begin until this phase is complete.

- [x] T008 Create base Supabase migration for roles, profiles, authorization helpers, grants, and RLS in `supabase/migrations/001_auth_profiles_roles.sql`
- [x] T009 Create system role protection migration in `supabase/migrations/002_protect_system_roles.sql`
- [x] T010 Create candidate catalog migration and seed data in `supabase/migrations/003_candidate_catalogs.sql`
- [x] T011 Create candidate core and relationship table migration in `supabase/migrations/004_candidates_core.sql`
- [x] T012 Create candidate document, audit, export, and import tracking migration in `supabase/migrations/005_candidate_documents_audit_import_export.sql`
- [x] T013 Create RLS policy migration for candidate business tables in `supabase/migrations/006_candidate_rls_policies.sql`
- [x] T014 Create private CV storage bucket and policy migration in `supabase/migrations/007_candidate_cv_storage.sql`
- [x] T015 Create advanced search RPC migration for `search_candidates(filters jsonb)` in `supabase/migrations/008_search_candidates_rpc.sql`
- [x] T016 [P] Create Supabase client wrapper in `src/app/core/supabase/supabase-client.service.ts`
- [x] T017 [P] Create typed app models for roles, profiles, permissions, and shared responses in `src/app/shared/models/auth.models.ts`
- [x] T018 [P] Create typed candidate domain models in `src/app/features/candidates/models/candidate.models.ts`
- [x] T019 [P] Create typed catalog models in `src/app/features/catalogs/models/catalog.models.ts`
- [x] T020 [P] Create typed document, search, export, and import models in `src/app/features/search/models/search.models.ts` and `src/app/features/documents/models/document.models.ts`
- [x] T021 Create base layout, protected shell, and navigation structure in `src/app/core/layout/app-layout.component.ts`
- [x] T022 Create toast/error presentation service in `src/app/core/services/toast.service.ts`
- [x] T023 [P] Create SQL/RLS check script skeleton in `scripts/check-rls.js`
- [x] T024 [P] Create storage-policy check script skeleton in `scripts/check-storage-policies.js`
- [x] T025 Create security seed/test fixtures for roles and users in `tests/security/fixtures/users.json`

**Checkpoint**: Foundation ready; user story implementation can now begin independently.

---

## Phase 3: User Story 1 - Acceso seguro a la aplicacion (Priority: P1) MVP

**Goal**: Authenticated users access only the screens and data allowed by role and security state.

**Independent Test**: Role-based access and negative authorization scenarios pass without requiring candidate CRUD completion.

### Tests for User Story 1

- [x] T026 [P] [US1] Add AuthService unit tests for session, profile, permissions, and MFA readiness in `tests/unit/auth.service.spec.ts`
- [x] T027 [P] [US1] Add route guard unit tests for authenticated, inactive, readonly, and MFA-required states in `tests/unit/auth-guards.spec.ts`
- [x] T028 [P] [US1] Add Playwright secure-login flow covering login, MFA route, and unauthorized protected route access in `tests/e2e/secure-access.spec.ts`
- [x] T029 [P] [US1] Add SQL/RLS tests for authenticated, readonly, inactive, no-profile, and unauthenticated access in `tests/security/rls-auth.sql`

### Implementation for User Story 1

- [x] T030 [P] [US1] Implement AuthService session and profile loading in `src/app/core/auth/auth.service.ts`
- [x] T031 [P] [US1] Implement MfaService wrappers for enrollment, challenge, verification, and assurance level in `src/app/core/auth/mfa.service.ts`
- [x] T032 [P] [US1] Implement route guards for authentication, active profile, permissions, and MFA in `src/app/core/guards/auth.guard.ts`
- [x] T033 [US1] Implement login page in `src/app/core/auth/login-page.component.ts`
- [x] T034 [US1] Implement MFA verification page in `src/app/core/auth/mfa-page.component.ts`
- [x] T035 [US1] Connect protected routes and role-aware navigation in `src/app/app.routes.ts` and `src/app/core/layout/app-layout.component.ts`
- [x] T036 [US1] Add Spanish/English auth, MFA, unauthorized, and validation texts in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [x] T037 [US1] Wire RLS and storage negative checks into `scripts/check-rls.js` and `scripts/check-storage-policies.js`

**Checkpoint**: Secure access is independently testable and forms the MVP gate.

---

## Phase 4: User Story 2 - Registrar y mantener candidatos (Priority: P1)

**Goal**: Authorized RRHH users create, view, edit, and logically deactivate candidates with audit metadata.

**Independent Test**: Candidate lifecycle works without advanced search, export, documents, or migration.

### Tests for User Story 2

- [x] T038 [P] [US2] Add CandidateService unit tests for create, update, read, and logical deactivate in `tests/unit/candidate.service.spec.ts`
- [x] T039 [P] [US2] Add CandidateForm validation unit tests in `tests/unit/candidate-form.spec.ts`
- [x] T040 [P] [US2] Add Playwright candidate create/edit/logical deactivate flow in `tests/e2e/candidate-crud.spec.ts`
- [x] T041 [P] [US2] Add SQL/RLS write-permission checks for candidates in `tests/security/rls-candidates.sql`

### Implementation for User Story 2

- [x] T042 [P] [US2] Implement CandidateService CRUD methods in `src/app/features/candidates/services/candidate.service.ts`
- [x] T043 [P] [US2] Implement candidate list page in `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [x] T044 [P] [US2] Implement candidate detail page with audit summary in `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [x] T045 [P] [US2] Implement candidate form component in `src/app/features/candidates/components/candidate-form.component.ts`
- [x] T046 [US2] Implement candidate create/edit pages in `src/app/features/candidates/pages/candidate-edit-page.component.ts`
- [x] T047 [US2] Implement logical deactivation flow in `src/app/features/candidates/services/candidate.service.ts` and `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [x] T048 [US2] Add candidate validation, lifecycle, and audit translations in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Candidate CRUD is independently usable by RRHH users with correct role enforcement.

---

## Phase 5: User Story 5 - Buscar candidatos con filtros combinados (Priority: P1)

**Goal**: RRHH can search active candidates using combined filters, ANY/ALL modes, and duplicate-free results.

**Independent Test**: Seeded candidates produce expected search results from the RPC and UI.

### Tests for User Story 5

- [x] T049 [P] [US5] Add SQL tests for `search_candidates` empty filters, AND combination, ANY/ALL languages, ANY/ALL programs, and duplicate prevention in `tests/security/search-candidates.sql`
- [x] T050 [P] [US5] Add CandidateSearchService unit tests for filter transformation and response mapping in `tests/unit/candidate-search.service.spec.ts`
- [x] T051 [P] [US5] Add Playwright advanced search flow in `tests/e2e/advanced-search.spec.ts`

### Implementation for User Story 5

- [x] T052 [P] [US5] Implement CandidateSearchService RPC integration in `src/app/features/search/services/candidate-search.service.ts`
- [x] T053 [P] [US5] Implement search filter models and validators in `src/app/features/search/models/search.models.ts`
- [x] T054 [P] [US5] Implement AdvancedSearchPageComponent in `src/app/features/search/pages/advanced-search-page.component.ts`
- [x] T055 [P] [US5] Implement SearchFiltersComponent with ANY/ALL controls in `src/app/features/search/components/search-filters.component.ts`
- [x] T056 [P] [US5] Implement SearchResultsComponent with secure CV action placeholder in `src/app/features/search/components/search-results.component.ts`
- [x] T057 [US5] Add route and navigation for advanced search in `src/app/app.routes.ts` and `src/app/core/layout/app-layout.component.ts`
- [x] T058 [US5] Add search labels, empty states, validation, and result messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Advanced search works independently with candidate data and catalogs.

---

## Phase 6: User Story 3 - Enriquecer el perfil profesional del candidato (Priority: P2)

**Goal**: RRHH maintains languages, programs, education, experience, and skills for each candidate.

**Independent Test**: Candidate profile sections save, display, edit, and reject duplicate/inconsistent records.

### Tests for User Story 3

- [x] T059 [P] [US3] Add unit tests for candidate language and program relation services in `tests/unit/candidate-relations.service.spec.ts`
- [x] T060 [P] [US3] Add form validation tests for education, experience, and skill sections in `tests/unit/candidate-profile-sections.spec.ts`
- [x] T061 [P] [US3] Add Playwright candidate profile enrichment flow in `tests/e2e/candidate-profile.spec.ts`
- [x] T062 [P] [US3] Add SQL/RLS relation access checks in `tests/security/rls-candidate-relations.sql`

### Implementation for User Story 3

- [x] T063 [P] [US3] Implement CandidateRelationsService in `src/app/features/candidates/services/candidate-relations.service.ts`
- [x] T064 [P] [US3] Implement CandidateLanguagesComponent in `src/app/features/candidates/components/candidate-languages.component.ts`
- [x] T065 [P] [US3] Implement CandidateProgramsComponent in `src/app/features/candidates/components/candidate-programs.component.ts`
- [x] T066 [P] [US3] Implement CandidateEducationComponent in `src/app/features/candidates/components/candidate-education.component.ts`
- [x] T067 [P] [US3] Implement CandidateExperienceComponent in `src/app/features/candidates/components/candidate-experience.component.ts`
- [x] T068 [P] [US3] Implement CandidateSkillsComponent in `src/app/features/candidates/components/candidate-skills.component.ts`
- [x] T069 [US3] Integrate profile enrichment sections into candidate detail/edit pages in `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [x] T070 [US3] Add relation section labels, duplicate warnings, and validation messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Professional profile enrichment is independently testable for an existing candidate.

---

## Phase 7: User Story 4 - Gestionar CVs y documentos privados (Priority: P2)

**Goal**: Authorized users upload, register, mark primary, and open candidate CVs through private storage and controlled access.

**Independent Test**: CV upload and opening pass positive and negative storage/security scenarios.

### Tests for User Story 4

- [x] T071 [P] [US4] Add DocumentService unit tests for upload, metadata, primary CV, and signed URL request in `tests/unit/document.service.spec.ts`
- [x] T072 [P] [US4] Add Edge Function tests for `candidate-create-signed-cv-url` authorization and validation in `tests/integration/candidate-create-signed-cv-url.spec.ts`
- [x] T073 [P] [US4] Add Playwright CV upload/opening flow in `tests/e2e/candidate-documents.spec.ts`
- [x] T074 [P] [US4] Add storage-policy tests for private bucket denial and authorized access in `tests/security/storage-candidate-cvs.sql`

### Implementation for User Story 4

- [x] T075 [P] [US4] Implement DocumentService upload and metadata operations in `src/app/features/documents/services/document.service.ts`
- [x] T076 [P] [US4] Implement CandidateDocumentsComponent in `src/app/features/candidates/components/candidate-documents.component.ts`
- [x] T077 [P] [US4] Implement `candidate-create-signed-cv-url` Edge Function in `supabase/functions/candidate-create-signed-cv-url/index.ts`
- [x] T078 [US4] Integrate secure CV actions into candidate detail and search result screens in `src/app/features/candidates/pages/candidate-detail-page.component.ts` and `src/app/features/search/components/search-results.component.ts`
- [x] T079 [US4] Add document upload/opening errors and security messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [x] T080 [US4] Complete storage policy checks for CV bucket in `scripts/check-storage-policies.js`

**Checkpoint**: Private document handling is independently testable and fails closed.

---

## Phase 8: User Story 6 - Exportar resultados controlados (Priority: P3)

**Goal**: Authorized users export search results without exposing internal storage details or unnecessary personal data.

**Independent Test**: Export output field set and authorization are validated from search results.

### Tests for User Story 6

- [x] T081 [P] [US6] Add ExportService unit tests for field-set mapping and forbidden field exclusion in `tests/unit/export.service.spec.ts`
- [x] T082 [P] [US6] Add Edge Function tests for `candidate-export-results` authorization and output metadata in `tests/integration/candidate-export-results.spec.ts`
- [x] T083 [P] [US6] Add Playwright export flow for permitted and denied users in `tests/e2e/export-results.spec.ts`

### Implementation for User Story 6

- [x] T084 [P] [US6] Implement ExportService in `src/app/features/search/services/export.service.ts`
- [x] T085 [P] [US6] Implement `candidate-export-results` Edge Function in `supabase/functions/candidate-export-results/index.ts`
- [x] T086 [US6] Integrate export action into `src/app/features/search/pages/advanced-search-page.component.ts`
- [x] T087 [US6] Add export audit event recording in `supabase/migrations/005_candidate_documents_audit_import_export.sql`
- [x] T088 [US6] Add export labels, blocked-action messages, and completion messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Controlled export is independently testable after search.

---

## Phase 9: User Story 7 - Importar datos depurados desde Access o CSV (Priority: P3)

**Goal**: Authorized technical/admin users import depurated Access/CSV data, inspect row errors, and validate representative candidates.

**Independent Test**: Dry-run and sample import produce import summary, valid records, and row-level errors.

### Tests for User Story 7

- [x] T089 [P] [US7] Add import parser unit tests for candidate, catalog, relation, and document metadata CSVs in `tests/unit/import.service.spec.ts`
- [x] T090 [P] [US7] Add Edge Function tests for dry-run, partial import, row errors, and authorization in `tests/integration/candidate-import-access-csv.spec.ts`
- [x] T091 [P] [US7] Add Playwright admin import flow in `tests/e2e/import-access-csv.spec.ts`

### Implementation for User Story 7

- [x] T092 [P] [US7] Implement import data models in `src/app/features/admin/import/import.models.ts`
- [x] T093 [P] [US7] Implement ImportService in `src/app/features/admin/import/import.service.ts`
- [x] T094 [P] [US7] Implement admin import page in `src/app/features/admin/import/import-page.component.ts`
- [x] T095 [P] [US7] Implement `candidate-import-access-csv` Edge Function in `supabase/functions/candidate-import-access-csv/index.ts`
- [x] T096 [US7] Add import route and permission guard in `src/app/app.routes.ts`
- [x] T097 [US7] Add import summary, row-error, dry-run, and validation texts in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Controlled import is independently testable with representative CSV fixtures.

---

## Phase 10: Catalogs, Admin, Retention, and Cross-Cutting Polish

**Purpose**: Complete supporting management screens, retention review, quality hardening, and documentation.

- [x] T098 [P] Implement CatalogService in `src/app/features/catalogs/services/catalog.service.ts`
- [x] T099 [P] Implement CatalogManagementPageComponent in `src/app/features/catalogs/pages/catalog-management-page.component.ts`
- [x] T100 [P] Implement ProfileService and role administration services in `src/app/features/admin/users/profile.service.ts` and `src/app/features/admin/roles/role.service.ts`
- [x] T101 [P] Implement AdminUsersPageComponent in `src/app/features/admin/users/admin-users-page.component.ts`
- [x] T102 [P] Implement AdminRolesPageComponent in `src/app/features/admin/roles/admin-roles-page.component.ts`
- [x] T103 Implement `candidate-retention-check` Edge Function in `supabase/functions/candidate-retention-check/index.ts`
- [x] T104 Add retention dashboard indicators to `src/app/core/layout/app-layout.component.ts` or `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [x] T105 Complete RLS smoke test runner in `scripts/check-rls.js`
- [x] T106 Complete Playwright authentication and seed setup in `tests/e2e/global-setup.ts`
- [x] T107 Run and fix definition-aligned lint, format, unit, E2E, RLS, and storage validation issues across `src`, `supabase`, `tests`, and `scripts`
- [x] T108 Update implementation notes and validation evidence in `specs/001-gestion-cvs-rrhh/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup; blocks all user stories.
- **US1 Secure access (Phase 3)**: Depends on Foundational; first MVP slice.
- **US2 Candidate CRUD (Phase 4)**: Depends on Foundational and US1 role/auth services.
- **US5 Advanced search (Phase 5)**: Depends on Foundational and candidate data from US2.
- **US3 Profile enrichment (Phase 6)**: Depends on Foundational and US2 candidate shell.
- **US4 Private documents (Phase 7)**: Depends on Foundational, US1, and US2.
- **US6 Controlled export (Phase 8)**: Depends on US5.
- **US7 Access/CSV import (Phase 9)**: Depends on Foundational, catalogs, and candidate model.
- **Polish (Phase 10)**: Depends on relevant preceding stories.

### User Story Dependencies

- **US1**: No user-story dependency after foundation.
- **US2**: Needs US1 security services for protected UI.
- **US5**: Needs candidate data model and candidate records.
- **US3**: Needs candidate detail/edit surfaces.
- **US4**: Needs candidate detail and document metadata/storage foundation.
- **US6**: Needs search result set.
- **US7**: Can be developed after foundation but should validate against candidate/catalog model.

### Within Each User Story

- Tests are written before implementation tasks for that story.
- Models and services precede components/pages.
- Security and RLS checks precede story acceptance.
- Story checkpoints must pass before marking the story complete.

---

## Requirements Coverage Matrix

| Requirement | Primary Coverage                               |
| ----------- | ---------------------------------------------- |
| FR-001      | T026-T037, T105                                |
| FR-002      | T008-T009, T017, T030-T037, T100-T102          |
| FR-003      | T038-T048                                      |
| FR-004      | T011, T018, T038-T048                          |
| FR-005      | T011, T059-T070                                |
| FR-006      | T011, T059-T070                                |
| FR-007      | T011, T059-T070                                |
| FR-008      | T011, T059-T070                                |
| FR-009      | T011, T059-T070                                |
| FR-010      | T012, T071-T080                                |
| FR-011      | T014, T071-T080                                |
| FR-012      | T014, T071-T080, T081-T088                     |
| FR-013      | T015, T049-T058                                |
| FR-014      | T015, T049-T058                                |
| FR-015      | T015, T049-T058                                |
| FR-016      | T015, T049-T058                                |
| FR-017      | T015, T049-T058                                |
| FR-018      | T052-T058                                      |
| FR-019      | T081-T088                                      |
| FR-020      | T081-T088                                      |
| FR-021      | T012, T047, T078, T087, T095                   |
| FR-022      | T089-T097                                      |
| FR-023      | T089-T097                                      |
| FR-024      | T022, T036, T048, T058, T070, T079, T088, T097 |
| FR-025      | T005, T036, T048, T058, T070, T079, T088, T097 |
| FR-026      | T119-T123                                      |
| FR-027      | T124-T128                                      |
| FR-028      | T121, T137                                     |
| FR-029      | T136, T139                                     |
| FR-030      | T129-T135                                      |
| FR-031      | T137-T140                                      |

| Non-Functional Requirement | Primary Coverage |
| -------------------------- | ---------------- |
| NFR-001                    | T116, T118       |
| NFR-002                    | T116, T118       |
| NFR-003                    | T109, T111, T115 |
| NFR-004                    | T026-T037, T110  |
| NFR-005                    | T014, T071-T074  |
| NFR-006                    | T081-T088, T115  |
| NFR-007                    | T012, T087, T113 |
| NFR-008                    | T113, T114       |
| NFR-009                    | T117, T118       |
| NFR-010                    | T107, T111       |

| Success Criterion | Primary Coverage                  |
| ----------------- | --------------------------------- |
| SC-001            | T038-T048                         |
| SC-002            | T049-T058                         |
| SC-003            | T049-T058                         |
| SC-004            | T026-T037, T041, T062, T074, T105 |
| SC-005            | T071-T080                         |
| SC-006            | T081-T088                         |
| SC-007            | T089-T097                         |
| SC-008            | T089-T097, T108                   |
| SC-009            | T026-T088, T107-T108              |
| SC-010            | T119-T128                         |
| SC-011            | T137-T140                         |
| SC-012            | T129-T135                         |

---

## Parallel Opportunities

- T004, T005, and T006 can run in parallel after T001.
- T016 through T020 can run in parallel after migrations are drafted.
- Test tasks within each story can run in parallel because they target different files.
- Components within US3 can run in parallel after CandidateRelationsService is defined.
- Admin/catalog polish tasks T098 through T102 can run in parallel.

## Parallel Example: US4 Private Documents

```text
Task: T071 DocumentService unit tests in tests/unit/document.service.spec.ts
Task: T072 Edge Function tests in tests/integration/candidate-create-signed-cv-url.spec.ts
Task: T073 Playwright CV upload/opening flow in tests/e2e/candidate-documents.spec.ts
Task: T074 Storage-policy tests in tests/security/storage-candidate-cvs.sql
```

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete US1 secure access.
3. Complete US2 candidate CRUD.
4. Complete US5 advanced search.
5. Validate login, candidate creation, and search end to end before moving into
   P2/P3 stories.

### Incremental Delivery

1. Add US3 profile enrichment.
2. Add US4 private CV documents.
3. Add US6 export.
4. Add US7 import.
5. Complete catalogs, admin, retention, and hardening.

### Definition Before Implementation

Do not start implementation until `spec.md`, `plan.md`, `research.md`,
`data-model.md`, `contracts/`, `quickstart.md`, checklists, and this `tasks.md`
are consistent and contain no unresolved clarification markers.

## Notes

- Each task uses an exact target file path.
- `[P]` tasks touch different files or can be prepared independently.
- `[US#]` labels map directly to user stories in `spec.md`.
- Security checks are part of the definition of done, not optional polish.

---

## Phase 11: Production Readiness And Release Governance

**Purpose**: Ensure the application is implementation-ready for production with
clear operability, safety controls, and release gates.

- [x] T109 [P] Add integration tests for Edge Function contracts (auth,
      validation, error envelopes, idempotency) in `tests/integration/`
- [x] T110 [P] Add missing role/auth guard unit tests for inactive, no-profile,
      readonly, and MFA-required states in `tests/unit/auth.service.spec.ts` and
      `tests/unit/auth-guards.spec.ts`
- [x] T111 [P] Add E2E coverage for candidate documents, export permissions, and
      admin import flow in `tests/e2e/`
- [x] T112 Complete SQL security suites for auth and storage negative/positive
      cases in `tests/security/rls-auth.sql` and
      `tests/security/storage-candidate-cvs.sql`
- [x] T113 Define and implement structured error catalog for frontend and Edge
      Functions in `src/app/shared/models/` and `supabase/functions/_shared/`
- [x] T114 Add observability baseline (request ids, structured logs, audit
      correlation) in frontend and Edge Functions
- [x] T115 Define export/import operational limits and enforce them in contracts,
      services, and functions
- [x] T116 Add staging smoke tests and release gate automation scripts in
      `scripts/`
- [x] T117 Document backup/restore and rollback runbook in `docs/`
- [x] T118 Execute staging go-live checklist and attach validation evidence in
      `specs/001-gestion-cvs-rrhh/quickstart.md`

---

## Phase 12: Candidate Operations Center (Backlog EPIC A, P1)

**Purpose**: Transform candidate list/detail flows into daily-use operational surfaces.

- [x] T119 [P] Add quick filters, text search, sorting, and pagination to candidate list in `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [x] T120 [P] Add list empty-state guidance and operational counters in candidate list/dashboard in `src/app/features/candidates/pages/candidate-list-page.component.ts` and `src/app/features/dashboard/dashboard-page.component.ts`
- [x] T121 Add bulk logical deactivation with confirmation and summary in `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [x] T122 Add candidate list unit tests for filters/sorting/pagination in `tests/unit/candidate-list-page.spec.ts`
- [x] T123 Add Playwright operational list flow in `tests/e2e/candidate-list-operations.spec.ts`

## Phase 13: Search Productivity Enhancements (Backlog EPIC B, P1)

**Purpose**: Reduce repetitive effort for recurring RRHH search operations.

- [x] T124 [P] Add saved searches model/service in `src/app/features/search/models/search.models.ts` and `src/app/features/search/services/search-presets.service.ts`
- [x] T125 [P] Integrate save/load/delete search presets in advanced search page in `src/app/features/search/pages/advanced-search-page.component.ts`
- [x] T126 Add active-filter chips and quick-remove UX in `src/app/features/search/components/search-filters.component.ts`
- [x] T127 Add unit tests for saved-search persistence and restore in `tests/unit/search-presets.service.spec.ts`
- [x] T128 Add Playwright scenario for saved searches in `tests/e2e/advanced-search-presets.spec.ts`

## Phase 14: Import/Export Operationalization (Backlog EPIC C, P0)

**Purpose**: Move import/export from basic interaction to traceable batch operations.

- [x] T129 [P] Add import batch history model and persistence in `src/app/features/admin/import/import.models.ts` and `src/app/features/admin/import/import.service.ts`
- [x] T130 [P] Add export batch history model and persistence in `src/app/features/search/services/export.service.ts`
- [x] T131 Add import page with `dry-run -> commit` explicit flow in `src/app/features/admin/import/import-page.component.ts`
- [x] T132 Add import error CSV download action in `src/app/features/admin/import/import-page.component.ts`
- [x] T133 Add export history panel and status indicator in `src/app/features/search/pages/advanced-search-page.component.ts`
- [x] T134 Add unit tests for import/export history and commit transitions in `tests/unit/import.service.spec.ts` and `tests/unit/export.service.spec.ts`
- [x] T135 Add Playwright E2E for import/export batch history in `tests/e2e/import-export-ops.spec.ts`

## Phase 15: Governance Guardrails Completion (Backlog EPIC D/E, P0/P1)

**Purpose**: Ensure administrative operations cannot create lockout or integrity failures.

- [x] T136 Enforce catalog in-use protection on deactivate/remove in `src/app/features/catalogs/services/catalog.service.ts`
- [x] T137 Enforce self-lockout and last-admin protections in `src/app/features/admin/users/profile.service.ts` and `src/app/features/admin/roles/role.service.ts`
- [x] T138 Add unit tests for admin guardrails in `tests/unit/profile.service.spec.ts` and `tests/unit/role.service.spec.ts`
- [x] T139 Mirror admin/catalog guardrails in Supabase policies/functions and migrations in `supabase/migrations/` and `supabase/functions/`
- [x] T140 Add integration tests for guardrails in `tests/integration/`

## Phase 16: UX And Accessibility Improvement Program (Backlog EPIC F, P1)

**Purpose**: Improve operational usability, consistency, and accessibility for daily RRHH workflows.

- [x] T141 [P] Add shell-level accessibility improvements (skip link, toast live-region semantics, focusable landmarks) in `src/app/core/layout/app-layout.component.ts`
- [x] T142 [P] Add active-filter chips and one-click filter removal in candidate list in `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [x] T143 [P] Improve catalog operation context and editing-state feedback in `src/app/features/catalogs/pages/catalog-management-page.component.ts`
- [x] T144 Replace ad-hoc `window.confirm` usage with unified confirm dialog pattern in `src/app/shared/components/` and consumer pages
- [x] T145 Add contextual empty-states by permission and screen intent in candidate/search/import/admin pages under `src/app/features/**/pages/`
- [x] T146 Improve import step-state clarity (`validated`, `ready`, `committed`) and inline guidance in `src/app/features/admin/import/import-page.component.ts`
- [x] T147 Add E2E UX flow checks for keyboard/focus and high-risk actions in `tests/e2e/`
- [x] T148 Add unit tests for list filter-chip interaction logic in `tests/unit/candidate-list-page.spec.ts`
- [x] T149 Add UX implementation evidence and before/after notes in `specs/001-gestion-cvs-rrhh/quickstart.md`
- [x] T150 Add UX baseline and wave progress governance in `specs/001-gestion-cvs-rrhh/ux-audit.md`
