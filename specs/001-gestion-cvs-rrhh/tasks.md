# Tasks: Gestion de CVs para RRHH

**Input**: Design documents from `specs/001-gestion-cvs-rrhh/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Required for this feature because the specification and constitution require evidence for user journeys, personal data protection, authorization, storage, search, export, and migration.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the project structure, tooling, and baseline configuration needed by all stories.

- [ ] T001 Create Angular/Supabase project structure from plan in `src/app`, `supabase/migrations`, `supabase/functions`, `tests`, and `scripts`
- [ ] T002 Align package scripts and dependencies with the referenced Angular 21/Supabase stack in `package.json`
- [ ] T003 Configure Angular application bootstrap, routing shell, and provider layout in `src/app/app.config.ts` and `src/app/app.routes.ts`
- [ ] T004 [P] Configure Tailwind, ESLint, Prettier, Husky, lint-staged, Jest, and Playwright project files at repository root
- [ ] T005 [P] Create translation file skeletons in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [ ] T006 [P] Create runtime configuration contract for frontend env values in `src/app/core/services/app-config.model.ts`
- [ ] T007 Create Docker frontend build/runtime files aligned to Node 22 Alpine and Nginx unprivileged in `Dockerfile.frontend` and `docker-compose.frontend.yml`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the data, auth, RLS, storage, and service foundations that all user stories depend on.

**Critical**: No user story work can begin until this phase is complete.

- [ ] T008 Create base Supabase migration for roles, profiles, authorization helpers, grants, and RLS in `supabase/migrations/001_auth_profiles_roles.sql`
- [ ] T009 Create system role protection migration in `supabase/migrations/002_protect_system_roles.sql`
- [ ] T010 Create candidate catalog migration and seed data in `supabase/migrations/003_candidate_catalogs.sql`
- [ ] T011 Create candidate core and relationship table migration in `supabase/migrations/004_candidates_core.sql`
- [ ] T012 Create candidate document, audit, export, and import tracking migration in `supabase/migrations/005_candidate_documents_audit_import_export.sql`
- [ ] T013 Create RLS policy migration for candidate business tables in `supabase/migrations/006_candidate_rls_policies.sql`
- [ ] T014 Create private CV storage bucket and policy migration in `supabase/migrations/007_candidate_cv_storage.sql`
- [ ] T015 Create advanced search RPC migration for `search_candidates(filters jsonb)` in `supabase/migrations/008_search_candidates_rpc.sql`
- [ ] T016 [P] Create Supabase client wrapper in `src/app/core/supabase/supabase-client.service.ts`
- [ ] T017 [P] Create typed app models for roles, profiles, permissions, and shared responses in `src/app/shared/models/auth.models.ts`
- [ ] T018 [P] Create typed candidate domain models in `src/app/features/candidates/models/candidate.models.ts`
- [ ] T019 [P] Create typed catalog models in `src/app/features/catalogs/models/catalog.models.ts`
- [ ] T020 [P] Create typed document, search, export, and import models in `src/app/features/search/models/search.models.ts` and `src/app/features/documents/models/document.models.ts`
- [ ] T021 Create base layout, protected shell, and navigation structure in `src/app/core/layout/app-layout.component.ts`
- [ ] T022 Create toast/error presentation service in `src/app/core/services/toast.service.ts`
- [ ] T023 [P] Create SQL/RLS check script skeleton in `scripts/check-rls.js`
- [ ] T024 [P] Create storage-policy check script skeleton in `scripts/check-storage-policies.js`
- [ ] T025 Create security seed/test fixtures for roles and users in `tests/security/fixtures/users.json`

**Checkpoint**: Foundation ready; user story implementation can now begin independently.

---

## Phase 3: User Story 1 - Acceso seguro a la aplicacion (Priority: P1) MVP

**Goal**: Authenticated users access only the screens and data allowed by role and security state.

**Independent Test**: Role-based access and negative authorization scenarios pass without requiring candidate CRUD completion.

### Tests for User Story 1

- [ ] T026 [P] [US1] Add AuthService unit tests for session, profile, permissions, and MFA readiness in `tests/unit/auth.service.spec.ts`
- [ ] T027 [P] [US1] Add route guard unit tests for authenticated, inactive, readonly, and MFA-required states in `tests/unit/auth-guards.spec.ts`
- [ ] T028 [P] [US1] Add Playwright secure-login flow covering login, MFA route, and unauthorized protected route access in `tests/e2e/secure-access.spec.ts`
- [ ] T029 [P] [US1] Add SQL/RLS tests for authenticated, readonly, inactive, no-profile, and unauthenticated access in `tests/security/rls-auth.sql`

### Implementation for User Story 1

- [ ] T030 [P] [US1] Implement AuthService session and profile loading in `src/app/core/auth/auth.service.ts`
- [ ] T031 [P] [US1] Implement MfaService wrappers for enrollment, challenge, verification, and assurance level in `src/app/core/auth/mfa.service.ts`
- [ ] T032 [P] [US1] Implement route guards for authentication, active profile, permissions, and MFA in `src/app/core/guards/auth.guard.ts`
- [ ] T033 [US1] Implement login page in `src/app/core/auth/login-page.component.ts`
- [ ] T034 [US1] Implement MFA verification page in `src/app/core/auth/mfa-page.component.ts`
- [ ] T035 [US1] Connect protected routes and role-aware navigation in `src/app/app.routes.ts` and `src/app/core/layout/app-layout.component.ts`
- [ ] T036 [US1] Add Spanish/English auth, MFA, unauthorized, and validation texts in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [ ] T037 [US1] Wire RLS and storage negative checks into `scripts/check-rls.js` and `scripts/check-storage-policies.js`

**Checkpoint**: Secure access is independently testable and forms the MVP gate.

---

## Phase 4: User Story 2 - Registrar y mantener candidatos (Priority: P1)

**Goal**: Authorized RRHH users create, view, edit, and logically deactivate candidates with audit metadata.

**Independent Test**: Candidate lifecycle works without advanced search, export, documents, or migration.

### Tests for User Story 2

- [ ] T038 [P] [US2] Add CandidateService unit tests for create, update, read, and logical deactivate in `tests/unit/candidate.service.spec.ts`
- [ ] T039 [P] [US2] Add CandidateForm validation unit tests in `tests/unit/candidate-form.spec.ts`
- [ ] T040 [P] [US2] Add Playwright candidate create/edit/logical deactivate flow in `tests/e2e/candidate-crud.spec.ts`
- [ ] T041 [P] [US2] Add SQL/RLS write-permission checks for candidates in `tests/security/rls-candidates.sql`

### Implementation for User Story 2

- [ ] T042 [P] [US2] Implement CandidateService CRUD methods in `src/app/features/candidates/services/candidate.service.ts`
- [ ] T043 [P] [US2] Implement candidate list page in `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [ ] T044 [P] [US2] Implement candidate detail page with audit summary in `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [ ] T045 [P] [US2] Implement candidate form component in `src/app/features/candidates/components/candidate-form.component.ts`
- [ ] T046 [US2] Implement candidate create/edit pages in `src/app/features/candidates/pages/candidate-edit-page.component.ts`
- [ ] T047 [US2] Implement logical deactivation flow in `src/app/features/candidates/services/candidate.service.ts` and `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [ ] T048 [US2] Add candidate validation, lifecycle, and audit translations in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Candidate CRUD is independently usable by RRHH users with correct role enforcement.

---

## Phase 5: User Story 5 - Buscar candidatos con filtros combinados (Priority: P1)

**Goal**: RRHH can search active candidates using combined filters, ANY/ALL modes, and duplicate-free results.

**Independent Test**: Seeded candidates produce expected search results from the RPC and UI.

### Tests for User Story 5

- [ ] T049 [P] [US5] Add SQL tests for `search_candidates` empty filters, AND combination, ANY/ALL languages, ANY/ALL programs, and duplicate prevention in `tests/security/search-candidates.sql`
- [ ] T050 [P] [US5] Add CandidateSearchService unit tests for filter transformation and response mapping in `tests/unit/candidate-search.service.spec.ts`
- [ ] T051 [P] [US5] Add Playwright advanced search flow in `tests/e2e/advanced-search.spec.ts`

### Implementation for User Story 5

- [ ] T052 [P] [US5] Implement CandidateSearchService RPC integration in `src/app/features/search/services/candidate-search.service.ts`
- [ ] T053 [P] [US5] Implement search filter models and validators in `src/app/features/search/models/search.models.ts`
- [ ] T054 [P] [US5] Implement AdvancedSearchPageComponent in `src/app/features/search/pages/advanced-search-page.component.ts`
- [ ] T055 [P] [US5] Implement SearchFiltersComponent with ANY/ALL controls in `src/app/features/search/components/search-filters.component.ts`
- [ ] T056 [P] [US5] Implement SearchResultsComponent with secure CV action placeholder in `src/app/features/search/components/search-results.component.ts`
- [ ] T057 [US5] Add route and navigation for advanced search in `src/app/app.routes.ts` and `src/app/core/layout/app-layout.component.ts`
- [ ] T058 [US5] Add search labels, empty states, validation, and result messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Advanced search works independently with candidate data and catalogs.

---

## Phase 6: User Story 3 - Enriquecer el perfil profesional del candidato (Priority: P2)

**Goal**: RRHH maintains languages, programs, education, experience, and skills for each candidate.

**Independent Test**: Candidate profile sections save, display, edit, and reject duplicate/inconsistent records.

### Tests for User Story 3

- [ ] T059 [P] [US3] Add unit tests for candidate language and program relation services in `tests/unit/candidate-relations.service.spec.ts`
- [ ] T060 [P] [US3] Add form validation tests for education, experience, and skill sections in `tests/unit/candidate-profile-sections.spec.ts`
- [ ] T061 [P] [US3] Add Playwright candidate profile enrichment flow in `tests/e2e/candidate-profile.spec.ts`
- [ ] T062 [P] [US3] Add SQL/RLS relation access checks in `tests/security/rls-candidate-relations.sql`

### Implementation for User Story 3

- [ ] T063 [P] [US3] Implement CandidateRelationsService in `src/app/features/candidates/services/candidate-relations.service.ts`
- [ ] T064 [P] [US3] Implement CandidateLanguagesComponent in `src/app/features/candidates/components/candidate-languages.component.ts`
- [ ] T065 [P] [US3] Implement CandidateProgramsComponent in `src/app/features/candidates/components/candidate-programs.component.ts`
- [ ] T066 [P] [US3] Implement CandidateEducationComponent in `src/app/features/candidates/components/candidate-education.component.ts`
- [ ] T067 [P] [US3] Implement CandidateExperienceComponent in `src/app/features/candidates/components/candidate-experience.component.ts`
- [ ] T068 [P] [US3] Implement CandidateSkillsComponent in `src/app/features/candidates/components/candidate-skills.component.ts`
- [ ] T069 [US3] Integrate profile enrichment sections into candidate detail/edit pages in `src/app/features/candidates/pages/candidate-detail-page.component.ts`
- [ ] T070 [US3] Add relation section labels, duplicate warnings, and validation messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Professional profile enrichment is independently testable for an existing candidate.

---

## Phase 7: User Story 4 - Gestionar CVs y documentos privados (Priority: P2)

**Goal**: Authorized users upload, register, mark primary, and open candidate CVs through private storage and controlled access.

**Independent Test**: CV upload and opening pass positive and negative storage/security scenarios.

### Tests for User Story 4

- [ ] T071 [P] [US4] Add DocumentService unit tests for upload, metadata, primary CV, and signed URL request in `tests/unit/document.service.spec.ts`
- [ ] T072 [P] [US4] Add Edge Function tests for `candidate-create-signed-cv-url` authorization and validation in `tests/integration/candidate-create-signed-cv-url.spec.ts`
- [ ] T073 [P] [US4] Add Playwright CV upload/opening flow in `tests/e2e/candidate-documents.spec.ts`
- [ ] T074 [P] [US4] Add storage-policy tests for private bucket denial and authorized access in `tests/security/storage-candidate-cvs.sql`

### Implementation for User Story 4

- [ ] T075 [P] [US4] Implement DocumentService upload and metadata operations in `src/app/features/documents/services/document.service.ts`
- [ ] T076 [P] [US4] Implement CandidateDocumentsComponent in `src/app/features/candidates/components/candidate-documents.component.ts`
- [ ] T077 [P] [US4] Implement `candidate-create-signed-cv-url` Edge Function in `supabase/functions/candidate-create-signed-cv-url/index.ts`
- [ ] T078 [US4] Integrate secure CV actions into candidate detail and search result screens in `src/app/features/candidates/pages/candidate-detail-page.component.ts` and `src/app/features/search/components/search-results.component.ts`
- [ ] T079 [US4] Add document upload/opening errors and security messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`
- [ ] T080 [US4] Complete storage policy checks for CV bucket in `scripts/check-storage-policies.js`

**Checkpoint**: Private document handling is independently testable and fails closed.

---

## Phase 8: User Story 6 - Exportar resultados controlados (Priority: P3)

**Goal**: Authorized users export search results without exposing internal storage details or unnecessary personal data.

**Independent Test**: Export output field set and authorization are validated from search results.

### Tests for User Story 6

- [ ] T081 [P] [US6] Add ExportService unit tests for field-set mapping and forbidden field exclusion in `tests/unit/export.service.spec.ts`
- [ ] T082 [P] [US6] Add Edge Function tests for `candidate-export-results` authorization and output metadata in `tests/integration/candidate-export-results.spec.ts`
- [ ] T083 [P] [US6] Add Playwright export flow for permitted and denied users in `tests/e2e/export-results.spec.ts`

### Implementation for User Story 6

- [ ] T084 [P] [US6] Implement ExportService in `src/app/features/search/services/export.service.ts`
- [ ] T085 [P] [US6] Implement `candidate-export-results` Edge Function in `supabase/functions/candidate-export-results/index.ts`
- [ ] T086 [US6] Integrate export action into `src/app/features/search/pages/advanced-search-page.component.ts`
- [ ] T087 [US6] Add export audit event recording in `supabase/migrations/005_candidate_documents_audit_import_export.sql`
- [ ] T088 [US6] Add export labels, blocked-action messages, and completion messages in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Controlled export is independently testable after search.

---

## Phase 9: User Story 7 - Importar datos depurados desde Access o CSV (Priority: P3)

**Goal**: Authorized technical/admin users import depurated Access/CSV data, inspect row errors, and validate representative candidates.

**Independent Test**: Dry-run and sample import produce import summary, valid records, and row-level errors.

### Tests for User Story 7

- [ ] T089 [P] [US7] Add import parser unit tests for candidate, catalog, relation, and document metadata CSVs in `tests/unit/import-parser.spec.ts`
- [ ] T090 [P] [US7] Add Edge Function tests for dry-run, partial import, row errors, and authorization in `tests/integration/candidate-import-access-csv.spec.ts`
- [ ] T091 [P] [US7] Add Playwright admin import flow in `tests/e2e/import-access-csv.spec.ts`

### Implementation for User Story 7

- [ ] T092 [P] [US7] Implement import data models in `src/app/features/admin/import/import.models.ts`
- [ ] T093 [P] [US7] Implement ImportService in `src/app/features/admin/import/import.service.ts`
- [ ] T094 [P] [US7] Implement admin import page in `src/app/features/admin/import/import-page.component.ts`
- [ ] T095 [P] [US7] Implement `candidate-import-access-csv` Edge Function in `supabase/functions/candidate-import-access-csv/index.ts`
- [ ] T096 [US7] Add import route and permission guard in `src/app/app.routes.ts`
- [ ] T097 [US7] Add import summary, row-error, dry-run, and validation texts in `src/assets/i18n/es.json` and `src/assets/i18n/en.json`

**Checkpoint**: Controlled import is independently testable with representative CSV fixtures.

---

## Phase 10: Catalogs, Admin, Retention, and Cross-Cutting Polish

**Purpose**: Complete supporting management screens, retention review, quality hardening, and documentation.

- [ ] T098 [P] Implement CatalogService in `src/app/features/catalogs/services/catalog.service.ts`
- [ ] T099 [P] Implement CatalogManagementPageComponent in `src/app/features/catalogs/pages/catalog-management-page.component.ts`
- [ ] T100 [P] Implement ProfileService and role administration services in `src/app/features/admin/users/profile.service.ts` and `src/app/features/admin/roles/role.service.ts`
- [ ] T101 [P] Implement AdminUsersPageComponent in `src/app/features/admin/users/admin-users-page.component.ts`
- [ ] T102 [P] Implement AdminRolesPageComponent in `src/app/features/admin/roles/admin-roles-page.component.ts`
- [ ] T103 Implement `candidate-retention-check` Edge Function in `supabase/functions/candidate-retention-check/index.ts`
- [ ] T104 Add retention dashboard indicators to `src/app/core/layout/app-layout.component.ts` or `src/app/features/candidates/pages/candidate-list-page.component.ts`
- [ ] T105 Complete RLS smoke test runner in `scripts/check-rls.js`
- [ ] T106 Complete Playwright authentication and seed setup in `tests/e2e/global-setup.ts`
- [ ] T107 Run and fix definition-aligned lint, format, unit, E2E, RLS, and storage validation issues across `src`, `supabase`, `tests`, and `scripts`
- [ ] T108 Update implementation notes and validation evidence in `specs/001-gestion-cvs-rrhh/quickstart.md`

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

| Requirement | Primary Coverage |
|-------------|------------------|
| FR-001 | T026-T037, T105 |
| FR-002 | T008-T009, T017, T030-T037, T100-T102 |
| FR-003 | T038-T048 |
| FR-004 | T011, T018, T038-T048 |
| FR-005 | T011, T059-T070 |
| FR-006 | T011, T059-T070 |
| FR-007 | T011, T059-T070 |
| FR-008 | T011, T059-T070 |
| FR-009 | T011, T059-T070 |
| FR-010 | T012, T071-T080 |
| FR-011 | T014, T071-T080 |
| FR-012 | T014, T071-T080, T081-T088 |
| FR-013 | T015, T049-T058 |
| FR-014 | T015, T049-T058 |
| FR-015 | T015, T049-T058 |
| FR-016 | T015, T049-T058 |
| FR-017 | T015, T049-T058 |
| FR-018 | T052-T058 |
| FR-019 | T081-T088 |
| FR-020 | T081-T088 |
| FR-021 | T012, T047, T078, T087, T095 |
| FR-022 | T089-T097 |
| FR-023 | T089-T097 |
| FR-024 | T022, T036, T048, T058, T070, T079, T088, T097 |
| FR-025 | T005, T036, T048, T058, T070, T079, T088, T097 |

| Success Criterion | Primary Coverage |
|-------------------|------------------|
| SC-001 | T038-T048 |
| SC-002 | T049-T058 |
| SC-003 | T049-T058 |
| SC-004 | T026-T037, T041, T062, T074, T105 |
| SC-005 | T071-T080 |
| SC-006 | T081-T088 |
| SC-007 | T089-T097 |
| SC-008 | T089-T097, T108 |
| SC-009 | T026-T088, T107-T108 |

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
