## Why

The API does not know who is calling. `ICurrentActor` is only ever `DevelopmentActor`, the browser
invents its own `UserProfile` on any email and password, and users and roles live in each browser's
`localStorage`. Every handler does call its permission guard first, so non-negotiable 3 holds in
form — but the guard is checking an actor that configuration handed it, so it decides nothing.

This is workstream 1 of `openspec/KTL-16.md`, and it blocks the other three (server-side import,
server-side candidate list, audit with an actor) as well as KTL-15. Until a request carries a
validated token, there is no actor to record in an audit row and no identity to authorize an import
against.

## What Changes

- **BREAKING** The API authenticates every business request with an OIDC JWT bearer token issued by
  Microsoft Entra ID. `ICurrentActor` is built from the validated token plus the caller's row in the
  database. A request with no token, an expired token, or a token failing signature or
  audience/issuer validation answers 401 — it never falls back to a permissive actor.
- **BREAKING** `AuthService.signIn(email, password)` is gone. The browser acquires a token from the
  identity provider and reads its profile from `GET /api/me`. MFA becomes the provider's
  responsibility; `mfa.service.ts`, `mfa-page.tsx` and the MFA route are removed, along with
  `supabase-client.service.ts` and the `@supabase/supabase-js` dependency.
- **BREAKING** Users and roles move from `localStorage` to `ADM_Users` and `ADM_Roles`. A user is
  keyed by the Entra `oid` claim. There is no central copy of the existing browser data, so the
  seeded set is the source of truth and local role customisations are lost.
- **BREAKING** The frontend `Permission` vocabulary is renamed to the server's
  `<resource>.<action>` form: `view_candidates` becomes `candidates.read`, `manage_catalogs` becomes
  `catalogs.manage`, and so on across the whole table. One vocabulary, no mapping layer.
- **BREAKING** `view_all_candidates` is removed. It guards no operation anywhere: the KTL-10
  visibility decision left it inert, and the domain models no scope it could narrow.
- `export_candidates` is **renamed to `candidates.export`, not removed.** An earlier draft of this
  proposal claimed there was no export code path in `src/app/features/**`; that was wrong.
  `advanced-search-page.tsx` has a working client-side CSV export whose button this permission
  gates, so removing it would ungate candidate personal data for every caller who can reach the
  search page. Like `candidates.import`, it is recorded as browser-only pending the ticket that
  gives server-side export a guard.
- Users are provisioned **just in time**: the first successful sign-in of an unknown subject creates
  an active user row holding a default read-only role. An administrator changes the role afterwards.
- Permissions are resolved **from the database on every request**, not carried in the token. A
  permission or role change takes effect on the caller's next request without a new sign-in, and
  deactivating a user refuses their still-valid token immediately.
- New endpoints under `/api/admin`: list, create, update and deactivate users; list, create, update
  and deactivate roles; set a role's permissions. Plus `GET /api/me`, which needs authentication
  only. All writes carry an optimistic-concurrency version.
- `ProfileService` and `RoleService` become API gateways shaped like `CandidateApi`. The
  `rrhh-demo-profile`, `rrhh-admin-users` and `rrhh-admin-roles` keys are evicted at startup through
  `evict-legacy-storage.ts`.
- `DevelopmentActor` stays, for local development and tests only, and gains a configurable
  permission set. It remains impossible to enable in Production and is never the fallback for a
  missing or invalid token.
- Users and roles are **deactivated, never deleted**. `ProfileService.remove()` — which physically
  dropped a user — is replaced by deactivation.

**Actors:**

- HR administrators holding `users.manage` and/or `roles.manage`, who govern who may do what.
- Every authenticated user, who reads their own profile and effective permissions from `GET /api/me`.
- Unauthenticated callers, who are refused 401 before any validation runs.
- Authenticated callers lacking a permission, who are refused 403 before any validation runs.
- Operators recovering a locked-out installation against the database.

**Key entities:**

- **User** — internal id, external subject (the Entra `oid`), display name, email, role name,
  active flag, version, created/updated timestamps, last sign-in.
- **Role** — name, label, system flag, permission set, active flag, version, timestamps.
- **Permission** — a `<resource>.<action>` string; the catalogue is code, the assignment is data.
- **Caller identity** — the resolved `ICurrentActor`: internal user id, external key, active flag,
  effective permission set.

**Assumptions:**

- Microsoft Entra ID is the corporate provider, and `oid` is its stable per-tenant subject claim.
  `sub` is not used: it is pairwise per application registration.
- Roles stay freely editable, as the current UI already allows, so the permission set is data. The
  five `DEFAULT_ROLES` are seeded as system roles.
- A local development alternative to Entra is required (the corporate tenant is not assumed
  available); the design names it and it must be a real token flow, not a bypass.
- Email and display name come from the token's claims and are refreshed on each sign-in; the
  application is not their source of truth.
- `candidates.import` stays in the vocabulary although its only enforcement point is still the
  browser import page. Workstream 2 gives it a server-side guard. This is stated rather than hidden.
- The `AUD_Events` actor column is workstream 4. This change stores the internal user id that
  workstream 4 will record, but does not change the audit schema.

**Edge cases:**

- A valid token whose subject has no user row (first sign-in → provision; or provisioning disabled).
- A valid token for a user who was deactivated after the token was issued.
- The last active user holding `users.manage` is deactivated or has their role changed.
- A user tries to change their own role or deactivate themselves.
- A system role is renamed, emptied of permissions, or deactivated.
- A role is deactivated while users still hold it.
- Two administrators edit the same user or role concurrently.
- A token whose clock skew puts it just outside its validity window.
- The identity provider's signing keys rotate while the API is running.
- The browser holds a stale `rrhh-demo-profile` from a previous build.

**Success criteria:**

- Every business endpoint answers 401 without a token and 403 without the specific permission,
  **before** validation runs, proven by integration tests per endpoint for: no token, expired token,
  invalid signature, wrong audience, valid token without the permission, and deactivated user with a
  still-valid token.
- A test proves `DevelopmentActor` cannot start in Production and that an absent token never resolves
  to it.
- Revoking a permission from a role changes the outcome of the affected user's very next request,
  with no new sign-in.
- Invariant tests cover: last administrator, self-role change, self-deactivation, system-role edits.
- A grants test shows `ktl_runtime` holds no `DELETE` on `ADM_Users` or `ADM_Roles`.
- Log assertions show no email, display name, raw token or subject claim reaches the logs.
- `ALL_PERMISSIONS` contains no permission whose only enforcement is hiding a UI control, except
  `candidates.import`, recorded as pending workstream 2, and `candidates.export`, recorded as
  pending the server-side export ticket.
- The three `localStorage` keys are absent after first run of the new build.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Capabilities

### New Capabilities

- `identity-and-access-control`: how a caller is authenticated, how a user and a role are stored and
  governed, how effective permissions are resolved per request, the permission vocabulary itself,
  the administration endpoints and screens, and the development actor's bounded role.

### Modified Capabilities

- `backend-platform`: "Production identity fails closed" is strengthened — the current actor is
  built from a validated bearer token rather than from a replaceable configured context, and an
  absent or invalid token is refused rather than falling through to any actor.
- `frontend-api-transport`: requests carry a bearer token acquired from the identity provider and a
  401 triggers re-authentication rather than a generic error; the eviction list gains
  `rrhh-demo-profile`, `rrhh-admin-users` and `rrhh-admin-roles`; users and roles join the list of
  features whose persistence has been cut over.
- `primary-navigation`: the entry permissions are restated in the `<resource>.<action>` vocabulary.
  The entries themselves do not change.
- `saved-search-presets`: `manage_presets` and `view_candidates` are restated as `presets.manage`
  and `candidates.read`. No preset behaviour changes.
- `candidate-search`: `view_candidates` is restated as `candidates.read`, and the inert
  `view_all_candidates` permission is removed from the visibility decision.

## Impact

- **Backend:**
  - `Application/Abstractions/Identity`: `ICurrentActor` gains the internal user id; `Permissions`
    gains `UsersManage`, `RolesManage`, `CandidatesImport`; a permission catalogue replaces the loose
    constants.
  - New `Domain/Identity` (`User`, `Role`), `Application/Features/Admin/{Users,Roles,Me}`,
    `Application/Abstractions/IUserRepository`/`IRoleRepository`.
  - `Infrastructure/Persistence`: repositories, EF configurations, a migration creating `ADM_Users`
    and `ADM_Roles` with their constraints, indexes and `ktl_runtime` grants (no `DELETE`), and
    seeding of the five system roles plus the bootstrap administrator.
  - `Web`: JWT bearer authentication, a `TokenCurrentActor` replacing the unconditional
    `DevelopmentActor` registration, `Web/Features/Admin/AdminEndpoints.cs`, `MeEndpoints.cs`,
    `Program.cs` wiring, and the Swagger document regaining `EnableJWTBearerAuth`.
- **Frontend:**
  - `auth.models.ts` (permission rename, `UserProfile` shape, `DEFAULT_ROLES` removal),
    `auth.service.ts` (provider session + `GET /api/me`), `login-page.tsx`,
    `api-transport.ts` (bearer header), `evict-legacy-storage.ts`, `di/services.ts`.
  - `features/admin/users/` and `features/admin/roles/` services become API gateways; their pages
    lose the local invariant checks that move to the server.
  - `nav-items.ts` and every `usePermission('...')` call site take the renamed permissions.
  - Removed: `core/supabase/`, `core/auth/mfa.service.ts`, `core/auth/mfa-page.tsx`.
  - `es.json` gains the new admin and sign-in copy; `LEGACY_HARDCODED_COPY` shrinks by the files
    touched.
- **Tests:** backend handler, schema, grants and per-endpoint authorization integration tests;
  Vitest specs for the auth service, the two admin services, navigation and the transport; Playwright
  sign-in and administration specs; a new `tests/security/` check for the admin surface.
- **Docs:** new `docs/ktl-16/` (authentication and authorization contract, the filled permission
  table, the administrator-recovery and rollback runbook), `README.md` (Spanish, local sign-in), and
  `AGENTS.md` (the Supabase/`localStorage` legacy note loses the paths this change closes).
- **Dependencies:** two new runtime dependencies, each justified in the design —
  `Microsoft.AspNetCore.Authentication.JwtBearer` (NuGet) and an MSAL browser package (npm). One
  dependency is removed: `@supabase/supabase-js`.
- **Personal data, storage and roles:**
  - This change touches personal data. A user row holds a display name, an email and an external
    subject identifier, and tokens carry the same claims.
  - Principle 1 is upheld as follows: responses carry the internal user id, never the external
    subject; the raw token, the subject claim, emails and display names are kept out of logs and
    added to the redaction enricher's coverage; `GET /api/me` returns only the caller's own profile;
    the user list is behind `users.manage`; no internal storage path or provider secret reaches the
    browser.
  - Principle 3 is upheld as follows: authentication runs before endpoint dispatch and each handler
    repeats its permission guard before validating; `ktl_runtime` receives `SELECT, INSERT, UPDATE`
    on the new tables and no `DELETE`; the migration runs as `ktl_migrator`; `DevelopmentActor`
    keeps its Production start-up guard and gains a test proving it is not a fallback.
  - Role definitions change substantially: roles become database rows, the permission vocabulary is
    renamed, and two permissions are removed.
  - No RLS policy and no document storage access changes.
