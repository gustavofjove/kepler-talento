# Quickstart Validation Guide: Gestion de CVs para RRHH

This guide defines validation scenarios for the MVP after implementation. It is
not implementation code.

## Prerequisites

- Supabase stack available with Auth, PostgreSQL, Storage, and Functions.
- Frontend container port `63151` available for RRHH BBDD, or an alternate
  `FRONTEND_PORT` defined when another local frontend is already using it.
- Frontend configuration provides `SUPABASE_URL`, `SUPABASE_ANON_KEY`,
  `APP_ENV`, and `APP_VERSION`.
- Test users exist for `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly`,
  and inactive/no-profile cases.
- Seed catalogs exist for statuses, availability, sources, languages, language
  levels, programs, program levels, education types, sectors, skills, skill
  levels, and document types.
- Private `candidate-cvs` bucket exists.
- Staging environment mirrors production auth, RLS, storage policies, and Edge
  Function configuration.
- Backup and restore procedure is documented for the active PostgreSQL
  deployment.

## Validation Commands

Use the concrete scripts that exist in the final repository. Expected names from
the project documents are:

```powershell
npm run lint
npm run format:check
npm run test
npm run e2e
npm run security:rls
npm run security:storage
```

Additional validation commands for production readiness (adapt to final toolchain):

```powershell
npm run build
npm run test:integration
npm run smoke:staging
```

## Scenario 1: Secure Access

1. Sign in as each configured role.
2. Verify protected screens require authentication.
3. Verify readonly and manager-reader users cannot create, edit, export, or
   download beyond their permissions.
4. Verify inactive/no-profile users receive no candidate data.

Expected outcome: unauthorized access fails closed and no candidate data leaks.

## Scenario 2: Candidate CRUD

1. Sign in as RRHH user with create/edit permissions.
2. Create a candidate with required fields.
3. Edit contact, status, availability, consent/review dates, and notes.
4. Apply logical deactivation.

Expected outcome: candidate lifecycle works and audit metadata is present.

## Scenario 3: Candidate Profile Enrichment

1. Add multiple languages and levels.
2. Add multiple programs/tools and levels.
3. Add education, experience, and skills.
4. Attempt duplicate language/program entries.

Expected outcome: valid relations are saved and duplicates are rejected or
explained clearly.

## Scenario 4: Private CV Document

1. Upload a PDF CV for a candidate.
2. Mark it as primary CV.
3. Open it as an authorized user.
4. Attempt to open it as a user without document permission.

Expected outcome: authorized users receive controlled temporary access; storage
paths and permanent URLs are not exposed; unauthorized users are denied.

## Scenario 5: Advanced Search

1. Search with empty filters.
2. Search by status and availability.
3. Search by multiple languages in ANY and ALL modes.
4. Search by multiple programs in ANY and ALL modes.
5. Search by combined language + program + education/sector criteria.
6. Search by CV availability.

Expected outcome: empty filters are ignored, different filter families combine
cumulatively, ANY/ALL behavior is correct, and duplicate candidates do not
appear.

## Scenario 6: Controlled Export

1. Run a search as a user with export permission.
2. Export default field set.
3. Inspect the file for permitted fields.
4. Attempt export as a user without permission.

Expected outcome: authorized export succeeds, unauthorized export fails, and no
internal storage paths or permanent CV links appear in the file.

## Scenario 7: Access/CSV Import

1. Run dry-run validation against representative CSV files.
2. Review row-level errors.
3. Load a small valid sample.
4. Validate at least 10 representative candidates with RRHH against source data.

Expected outcome: valid data loads into normalized entities, invalid rows are
reported, and Access remains a reference source only.

## Scenario 8: Security Regression

1. Run SQL/RLS checks for each role.
2. Run storage-policy checks for public access denial and authorized signed URL
   generation.
3. Run negative tests for unauthenticated users.

Expected outcome: all security checks pass and fail closed where expected.

## Scenario 9: Performance And Limits

1. Execute representative advanced searches with expected production-like
   filter combinations.
2. Validate response times for candidate create/edit/search interactions.
3. Validate export behavior below and above configured row thresholds.
4. Validate import behavior with realistic CSV batch sizes.

Expected outcome: interactive operations meet agreed response targets and large
operations fail safely or switch to asynchronous processing without data loss.

## Scenario 10: Backup And Recovery

1. Execute a backup in staging.
2. Restore the backup into a clean database instance.
3. Run smoke validation for auth, candidate search, and document metadata.

Expected outcome: restore works with consistent data and no RLS/storage policy
regression.

## Scenario 11: Candidate Operations Center

1. Use candidate list quick filters, sorting, and pagination on representative data.
2. Execute at least one sensitive action requiring explicit confirmation.
3. Validate resulting candidate states and user feedback.

Expected outcome: operational tasks are fast, explicit, and safe for daily RRHH work.

## Scenario 12: Saved Searches And Batch Histories

1. Save a recurring advanced search and reload it.
2. Validate last-search restoration when returning to the search page.
3. Execute import/export and inspect batch history entries.

Expected outcome: recurring workflows are repeatable and operationally traceable.

## Go-Live Exit Criteria

- All mandatory scenarios (1..12) pass in staging.
- No open high-severity security defects.
- RRHH business sign-off completed for representative end-to-end workflows.
- Runbook validated for incidents, secret rotation, and rollback.

## Implementation Notes

- User story 3 relation-section translations are now present in both Spanish and English for languages, programs, education, experience, and skills.
- Shared validation strings are available for duplicate relation prevention, negative years, invalid date ranges, required degrees, and missing candidates.
- The repository still has broader formatting debt outside the touched files, so repo-wide formatting checks should be treated separately from feature-specific validation.

## Validation Evidence

- `npm run lint` completes without reported issues.
- `npm run e2e` now includes operational candidate-list, preset, import/export, and security flows.
- `npm run test -- --runInBand` passes with 20 suites and 100 tests in current local validation.
- `npm run test:integration` validates edge-contract envelopes, idempotency context, and guardrail integration checks.
- `npm run security:rls` validates required auth/RLS policies and guardrail trigger presence.
- `npm run security:storage` validates private candidate bucket and required storage policies.
- `npm run build` generates the Angular production bundle successfully.
- `npm run release:gate` executes build + tests + security checks and completed successfully (`All gates passed`).
- `docker compose -f docker-compose.frontend.yml build` completes successfully and produces the frontend image.
- `docker compose -f docker-compose.frontend.yml up -d` serves RRHH BBDD on `63151`; in the shared local environment `KeplerDesk` is mapped to `63153` to avoid port collisions.
- `npx prettier --check src/assets/i18n/es.json src/assets/i18n/en.json` passes for the updated translation files.
- `get_errors` across `src`, `supabase`, `tests`, and `scripts` reported no current TypeScript or script errors in the workspace snapshot.

## UX Before And After Evidence (Phase 16)

1. High-risk action confirmation consistency
   Before: destructive actions relied on mixed native browser confirms.
   After: one shared dialog pattern is used across candidates, catalogs, admin users/roles, documents, and presets.
   Evidence: added shared confirm primitives in src/app/shared/components and migrated consumers in features pages/components.

2. Contextual empty-states
   Before: low-context empty results and limited guidance by role/intent.
   After: contextual copy was added for read-only candidate list mode, search export permissions, import onboarding, and admin user empty table state.
   Evidence: candidate list, advanced search, import page, and admin users page now render intent-aware helper messages.

3. Import step clarity
   Before: dry-run and commit outcomes were visible but step status was implicit.
   After: explicit step labels communicate pending, validated-with-errors, ready-for-commit, and committed states with inline guidance.
   Evidence: import summary now includes current step label and scenario-specific helper text.

4. Accessibility and keyboard UX checks
   Before: no dedicated UX E2E checks for keyboard/focus and risky action confirmation modal behavior.
   After: E2E coverage validates skip-link keyboard path to main landmark and keyboard cancellation of custom confirmation dialogs.
   Evidence: tests/e2e/ux-accessibility.spec.ts.

5. Automated validation executed for this UX wave

- npm run test -- --runInBand: 20 suites, 100 tests passed.
- npm run build: production build generated successfully.
- npx playwright test tests/e2e/candidate-list-operations.spec.ts tests/e2e/advanced-search-presets.spec.ts tests/e2e/catalogs-crud.spec.ts tests/e2e/ux-accessibility.spec.ts: 5 passed.
