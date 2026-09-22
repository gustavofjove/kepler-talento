## Why

HR users can search candidates and save reusable criteria, but they cannot yet organize those criteria around a vacancy or return to a durable position record. KTL-15 adds that missing operational context so recruiters can maintain an open/closed position and see its current matching candidates without duplicating search logic or storing a separate candidate snapshot.

## What Changes

- Add a top-level **Posiciones** section with permission-controlled, responsive list, create, detail and edit routes.
- Add API-owned position records with a unique title, optional sanitized rich-text description, optional location, `open`/`closed` lifecycle, optimistic concurrency and a complete normalized candidate-search filter document.
- Provide bounded position listing with text/status filters, deterministic sorting and a minimal projection.
- Reuse the shared criteria editor, summary, preset library and candidate-search endpoint. Applying a preset copies its filters; positions do not retain a preset relationship, candidate assignment or historical match snapshot.
- Show live paged candidate matches only when the actor also holds `candidates.read`; preserve the existing search, document-download and preset permission boundaries.
- Add `positions.read` and `positions.manage`, seed them to the agreed system roles and enforce both endpoint and handler guards before validation or lookup.
- Close and reopen positions without physical deletion. Add `"OPS_Positions"` through an EF Core migration with `SELECT`, `INSERT` and `UPDATE`—but no `DELETE`—for `ktl_runtime`.
- Add Spanish localized copy, accessibility/responsive coverage, security evidence and KTL-15 operating/API documentation.

The change touches candidate personal data because position requirements may contain identifying search text and the detail page can display candidate matches. List projections exclude requirements and candidate facts; the API reuses the minimal search result, permissions fail closed, and logs/audit payloads contain neither filters nor candidate values. It changes role definitions and runtime grants, with seeded permissions updated idempotently and no direct browser/database path. It does not change RLS policies or private document storage; existing permission-checked document download remains authoritative.

## Capabilities

### New Capabilities

- `position-management`: Position lifecycle, rich-text safety, requirements, listing, live candidate matching, preset-copy behavior, frontend journeys and API/database boundaries.

### Modified Capabilities

- `primary-navigation`: Add the permission-filtered **Posiciones** top-level entry in the required order on desktop and mobile.
- `identity-and-access-control`: Add the position permissions, their independence rules and seeded-role assignments.
- `postgresql-persistence`: Extend the registered `OPS_` ownership to position workflow state and enforce least-privilege, non-delete runtime access for the new table.
- `saved-search-presets`: Allow the position editor to apply a shared preset as an independent filter copy and, with `presets.manage`, create a preset from its current requirements.

## Impact

- **Frontend:** new `src/app/features/positions/` models, service/hook, pages, components and styles; route and navigation tables; DI composition; permission vocabulary; Spanish resources; reuse of search/preset components and services.
- **Backend:** new Positions vertical slice across Domain, Application, Infrastructure and Web; endpoint registration; current-actor permissions; role seeds; rich-text sanitization; search-filter serialization reuse.
- **Database:** new quoted `"OPS_Positions"` table, constraints and indexes; permission seed update; runtime `SELECT`/`INSERT`/`UPDATE` grants without `DELETE`; representative query-plan evidence.
- **Contracts:** four new `/api/positions` operations; existing `/api/candidates/search` and `/api/search-presets` contracts are reused rather than duplicated.
- **Dependencies:** a production-grade HTML sanitizer and any rich-text editing dependency require an explicit design decision, centrally pinned version and justification; no Supabase or new `localStorage` path is introduced.
- **Tests/docs:** backend unit/integration and security coverage, frontend unit/integration and Playwright journeys, navigation/accessibility checks, `docs/ktl-15/`, and the database-conventions registry.
