# Quickstart Validation Guide: Gestion de CVs para RRHH

This guide defines validation scenarios for the MVP after implementation. It is
not implementation code.

## Prerequisites

- Supabase stack available with Auth, PostgreSQL, Storage, and Functions.
- Frontend configuration provides `SUPABASE_URL`, `SUPABASE_ANON_KEY`,
  `APP_ENV`, and `APP_VERSION`.
- Test users exist for `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly`,
  and inactive/no-profile cases.
- Seed catalogs exist for statuses, availability, sources, languages, language
  levels, programs, program levels, education types, sectors, skills, skill
  levels, and document types.
- Private `candidate-cvs` bucket exists.

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
