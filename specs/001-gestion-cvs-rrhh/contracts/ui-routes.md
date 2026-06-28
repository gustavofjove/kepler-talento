# Contract: UI Routes And Screens

## Purpose

Define the minimum user-facing application surface for the RRHH CV management
MVP.

## Routes

| Route | Purpose | Access |
|-------|---------|--------|
| `/login` | User sign-in | Public unauthenticated |
| `/mfa` | MFA/TOTP verification | Authenticated pending assurance |
| `/app` | Internal dashboard | Authenticated active profile |
| `/app/candidates` | Candidate list | Candidate view permission |
| `/app/candidates/new` | Candidate creation | Candidate create permission |
| `/app/candidates/:id` | Candidate detail | Candidate view permission |
| `/app/candidates/:id/edit` | Candidate edit | Candidate edit permission |
| `/app/candidates/:id/documents` | Candidate documents | Candidate document permission |
| `/app/search` | Advanced search | Candidate view/search permission |
| `/app/catalogs` | Catalog management | Catalog management permission |
| `/app/admin/users` | User administration | User management permission |
| `/app/admin/roles` | Role administration | Role management permission |

## Screen Contracts

### Dashboard

Shows operational counters: active candidates, candidates received this month,
candidates pending review, and candidates without CV.

### Candidate List

Provides paginated list, quick search, candidate detail access, new candidate
action, edit action where permitted, and logical deactivation where permitted.

### Candidate Detail

Shows candidate main data, languages, programs, education, experience, skills,
documents, and basic audit information.

### Candidate Form

Supports create and edit flows. Required data validation must be visible before
submission and returned server-side errors must be presented as user-readable
messages.

### Advanced Search

Includes filter panel, multi-select for catalogs, ANY/ALL controls for languages
and programs, clear filters, search action, results table, export action where
permitted, and secure CV opening where permitted.

### Catalogs

Supports list, create, edit, deactivate, and sort order management for closed
lists used in the candidate domain.

### Administration

Supports users, roles, activation/deactivation, and MFA-related administration
according to permissions.

## UX Rules

- Spanish is the primary MVP language.
- Visible text belongs in translation files.
- Desktop and laptop productivity are the responsive priority for MVP.
- Unauthorized actions are hidden or disabled for UX, but backend/RLS still
  enforces the true boundary.
- Loading, empty, validation, unauthorized, and error states must be specified
  per screen before implementation.

## Traceability

US1, US2, US3, US4, US5, US6; FR-001 through FR-025.
