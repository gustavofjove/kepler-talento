## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-16` from an up-to-date `main`

## 1. Permission vocabulary (do this first, on its own)

The rename is mechanical and touches most of `src/`. Landing it before any behaviour changes keeps a
missed call site a compile error in a small diff. Covers: Single permission vocabulary.

- [x] 1.1 In `backend/Application/Abstractions/Identity/ICurrentActor.cs`, add
      `Permissions.UsersManage = "users.manage"`, `Permissions.RolesManage = "roles.manage"` and
      `Permissions.CandidatesImport = "candidates.import"`, plus a `Permissions.All` array holding
      the whole catalogue. Comment `CandidatesImport` with the ticket that will guard it (KTL-17).
- [x] 1.2 Rewrite `src/app/shared/models/auth.models.ts`: `Permission` becomes the
      `<resource>.<action>` union matching `Permissions.All`; drop `export_candidates` and
      `view_all_candidates`; update `ALL_PERMISSIONS`; restate `DEFAULT_ROLES` in the new names
      (it is deleted later in task 7, but the intermediate state must compile and pass tests).
- [x] 1.3 Update every `usePermission('...')` call site, `nav-items.ts`, `NAV_PERMISSIONS`, route
      guards and unit-test doubles to the new names. Remove the `export_candidates` and
      `view_all_candidates` checkboxes and any UI that only they gated.
      Covers: Primary navigation entries and permission visibility.
- [x] 1.4 Add `tests/unit/permission-vocabulary.spec.ts` asserting `ALL_PERMISSIONS` matches the
      catalogue the API exposes, so the two cannot drift (design D10).
- [x] 1.5 Run `npm test` and `npm run build:all` and confirm the rename is complete before moving on.

## 2. Identity domain and persistence

Covers: User administration; Role administration; Users and roles are deactivated, never deleted;
Optimistic concurrency on administration writes.

- [x] 2.1 Add `backend/Domain/Identity/User.cs` and `Role.cs` as sealed aggregates: uuid v7 ids,
      `Version` (uint), `DateTimeOffset` UTC timestamps, `IsActive`, and behaviour methods
      (`Rename`, `AssignRole`, `Deactivate`, `Reactivate`, `ReplacePermissions`). `Role.Name` is
      settable only at construction (design D7).
- [x] 2.2 Add `IUserRepository` and `IRoleRepository` to `Application/Abstractions`, with
      `ExpectVersion` and a save-outcome enum carrying `ConcurrencyConflict`, matching the
      `ISearchPresetRepository` shape.
- [x] 2.3 Add EF configurations under `Infrastructure/Persistence/Configurations`: `ADM_Users` and
      `ADM_Roles`, quoted identifiers, `Permissions` as jsonb, `Version` as `IsRowVersion()` over
      `xmin`, unique indexes on `ExternalSubject`, `lower(Email)` and `Name`, the
      `RoleName` → `ADM_Roles.Name` foreign key, and check constraints for the name pattern and a
      non-empty permission set.
- [x] 2.4 Implement `UserRepository` and `RoleRepository` in `Infrastructure/Persistence`, including
      the duplicate-key-to-re-read path that makes concurrent first provisioning safe (design D4).
- [x] 2.5 Generate the migration with
      `dotnet ef migrations add AddIdentityTables --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`
      and extend it: seed the five system roles in the new vocabulary; seed the bootstrap
      administrator from configuration; `GRANT SELECT, INSERT, UPDATE ON "ADM_Users", "ADM_Roles" TO ktl_runtime`
      with **no** `DELETE`; a `Down` that drops both tables.
      Covers: Bootstrap administrator and recovery.
- [x] 2.6 Apply the migration against the local stack (`docker compose` running, `--migrate` entry
      point) and inspect the resulting schema, constraints and grants in PostgreSQL.

## 3. Authentication on the API

Covers: Token-based caller authentication; Users are provisioned on first sign-in; Deactivated users
are refused; Effective permissions are resolved per request; The development actor is bounded and is
never a fallback.

- [x] 3.1 Pin `Microsoft.AspNetCore.Authentication.JwtBearer` in
      `backend/Directory.Packages.props` and reference it from `Web`.
- [x] 3.2 Add `Web/Identity/AuthenticationOptions.cs` (authority, audience, subject claim name
      defaulting to `oid`, bootstrap administrator, dev issuer settings) bound and validated with
      `ValidateOnStart`, failing start-up if the dev issuer is configured in Production.
- [x] 3.3 Extend `ICurrentActor` with `Guid? UserId` and implement
      `Web/Identity/TokenCurrentActor.cs`: read the subject claim, load user and role once per
      request, provision on first sign-in with the least-privileged role, refresh display name and
      email from claims, answer `HasPermission` from the role's permission set, and treat an
      inactive user as unauthenticated (design D3, D4).
- [x] 3.4 Wire `Program.cs`: `AddAuthentication().AddJwtBearer()` with issuer, audience, lifetime and
      signing-key validation and 60-second clock skew; `UseAuthentication`/`UseAuthorization` after
      the correlation middleware and before `UseFastEndpoints`; `.RequireAuthorization()` on every
      business endpoint group and not on health; `EnableJWTBearerAuth = true` on the Swagger
      document.
- [x] 3.5 Register `ICurrentActor` as a composition-time **branch** — `DevelopmentActor` when
      enabled, `TokenCurrentActor` otherwise — never a chained fallback (design D11).
- [x] 3.6 Add `string[] Permissions` to `DevelopmentActorOptions`, defaulting to the nine values
      currently hard-coded, so a test can produce an authenticated-but-unauthorized caller.
- [x] 3.7 Add the `Development`/`Testing`-only token issuer and `POST /api/dev/token` (design D8),
      and make the JwtBearer handler trust it only outside Production.
- [x] 3.8 Extend `PersonalDataRedactionEnricher` to cover the subject claim, bearer tokens, emails
      and display names, and add the log assertions.
      Covers: Identity data is absent from logs and responses.

## 4. Administration endpoints

Covers: User administration; Role administration; Administration invariants protect against lockout
and self-escalation; Administration authorization fails closed; Caller profile endpoint.

- [x] 4.1 Add `Application/Features/Admin/AdminGuards.cs` (`RequireManageUsers`,
      `RequireManageRoles`) and the error types in `Application/Common/Errors` for lockout,
      self-target, system-role and role-in-use refusals, each with a stable code.
- [x] 4.2 Add `Application/Features/Admin/AdminInvariants.cs` holding the last-administrator,
      self-role-change, self-deactivation, system-role and role-in-use rules, defining
      "administrator" as an active user whose active role grants `users.manage`, and evaluated
      inside the write transaction (design D6).
- [x] 4.3 Add the user use cases under `Application/Features/Admin/Users/`, one file per use case
      (list, get, create, update, set role, set active) — sealed record command/query, FluentValidation
      validator, sealed handler with the guard repeated first.
- [x] 4.4 Add the role use cases under `Application/Features/Admin/Roles/` the same way (list, get,
      create, update label, set permissions, set active), validating every permission against
      `Permissions.All`.
- [x] 4.5 Add `Application/Features/Admin/Me/GetMeQuery.cs`: authentication only, no permission,
      returning the caller's own id, display name, email, role name and label, active flag and
      effective permissions — and never the external subject.
- [x] 4.6 Add `Web/Features/Admin/AdminEndpoints.cs` (`MapGroup("/api/admin")`) and
      `Web/Features/Admin/MeEndpoints.cs` (`MapGroup("/api/me")`) with nested request records,
      `.WithName()`, explicit `.Produces*` metadata and `ISender` dispatch; register both in
      `Program.cs`.
- [x] 4.7 Confirm every new error surfaces through `GlobalExceptionHandler` as ProblemDetails with
      its stable code, and that no handler builds an error response by hand.

## 5. Backend tests and security evidence

- [x] 5.1 Unit tests for the invariants with hand-written doubles: last administrator by
      deactivation, by role change and by role edit; self-role change; self-deactivation; system
      role rename, empty permissions and deactivation; role still in use.
- [x] 5.2 Unit tests for `TokenCurrentActor`: unknown subject provisions once; inactive user is
      unauthenticated; inactive role yields no permissions; claim-carried permissions are ignored.
- [x] 5.3 Integration tests in `backend/Tests/IntegrationTests/AdminApiTests.cs` covering every new
      endpoint for: **no token, expired token, invalid signature, wrong audience, valid token
      without the permission, deactivated user with a still-valid token** — asserting the refusal
      comes before validation by sending a malformed body with each.
- [x] 5.4 Integration test proving a permission revoked from a role changes the outcome of the same
      caller's next request, with the same token.
- [x] 5.5 Integration test proving `DevelopmentActor` cannot start in Production and that an absent
      or invalid token never resolves to it.
- [x] 5.6 Extend the grants evidence test: `ktl_runtime` holds no `DELETE` on `ADM_Users` or
      `ADM_Roles`, asserted by querying the catalog rather than reading the migration.
- [x] 5.7 Log-assertion test: no email, display name, subject claim or token reaches the logs through
      authentication, authorization or administration writes.
- [x] 5.8 Review and update the existing integration tests that flip `DevelopmentActor:Enabled`
      (`CandidateApiTests`, `CatalogApiTests`, `SearchApiTests`) for the renamed permissions and the
      new options shape.
- [x] 5.9 Run `npm run test:backend` with Docker running and inspect the output.

## 6. Frontend transport and session

Covers: Browser sign-in and profile; Shared API request behavior; Eviction of superseded browser
storage.

- [x] 6.1 Add `@azure/msal-browser` to `package.json` and remove `@supabase/supabase-js`; delete
      `src/app/core/supabase/`.
- [x] 6.2 Add `src/app/core/auth/token-source.ts`: one interface with an MSAL implementation and a
      development implementation that calls `POST /api/dev/token`, selected by build configuration
      (design D9). The token is never written to `localStorage`.
- [x] 6.3 Give `ApiTransport` a `tokenProvider` dependency, attach `Authorization: Bearer` in
      `send()`, and notify the session on a 401 so it can clear and redirect. Refuse locally rather
      than sending a credential-less request when no session is held.
- [x] 6.4 Rewrite `AuthService`: keep the `signal<UserProfile | null>` shape; `signIn()` acquires a
      token then fills the profile from `GET /api/me`; `signOut()` ends both the local and the
      provider session; remove `restoreProfile` and the `rrhh-demo-profile` key entirely.
- [x] 6.5 Rewrite `login-page.tsx` to delegate to the provider — no email/password the application
      validates itself. Delete `mfa.service.ts`, `mfa-page.tsx` and the MFA route.
- [x] 6.6 Add `rrhh-demo-profile`, `rrhh-admin-users` and `rrhh-admin-roles` to `SUPERSEDED_KEYS` in
      `evict-legacy-storage.ts`, with a comment saying what each held.
- [x] 6.7 Wire the new services in `src/app/core/di/services.ts` and update the test doubles in
      `tests/unit/support/`.

## 7. Frontend administration screens

Covers: User and role administration screens.

- [x] 7.1 Rewrite `src/app/features/admin/users/profile.service.ts` as an API gateway shaped like
      `CandidateApi`: async reads, versioned writes, no `localStorage`, no local invariant checks,
      and `remove()` replaced by deactivation. Add a `useUsers()` subscribing hook.
- [x] 7.2 Rewrite `src/app/features/admin/roles/role.service.ts` the same way; delete
      `DEFAULT_ROLES` from `auth.models.ts` now that roles come from the API.
- [x] 7.3 Update the users page: list with display name, email, role and state; create; change role;
      deactivate and reactivate with explicit confirmation; render API refusals as distinct Spanish
      messages through `errorText(err, t)` and keep the entered values.
- [x] 7.4 Update the roles page the same way, including setting a role's permissions and the
      system-role restrictions surfaced as disabled controls with the refusal still coming from the
      API.
- [x] 7.5 Move all new and touched copy into `src/assets/i18n/es.json` under flat
      `admin.users.*` / `admin.roles.*` / `auth.*` keys, using whole sentences with interpolation,
      and remove every file touched in this change from `LEGACY_HARDCODED_COPY` in
      `eslint.config.js`. The list only shrinks.
- [x] 7.6 Keep the `name=` attribute and `data-testid` on every form control the Playwright suite
      binds to.

## 8. Frontend tests

- [x] 8.1 Review and update the existing unit tests affected by the rename and the service rewrites:
      navigation, route guards, the auth doubles, and any spec asserting on `DEFAULT_ROLES`.
- [x] 8.2 New specs for `AuthService` (sign-in fills the profile from `/api/me`; 401 clears the
      session), `ApiTransport` (token attached; no token means no request), the two admin services,
      and `evictSupersededStorage` removing all four keys.
- [x] 8.3 Specs for the users and roles pages: rendering, the confirmation flow, and each distinct
      Spanish refusal message. Locate elements by role, accessible name or `data-testid`.
- [x] 8.4 Run `npm test` and inspect the output.

## 9. End-to-end verification

- [x] 9.1 Add `tests/e2e/authentication.spec.ts`: sign in through the dev token issuer, land on the
      dashboard, sign out, and confirm a protected route redirects when signed out. No selector may
      hardcode Spanish text.
- [x] 9.2 Add `tests/e2e/admin-users-roles.spec.ts`: create a user, change their role, deactivate
      and reactivate them; create a role, set its permissions, deactivate it.
- [x] 9.3 **Run** `npm run e2e` with `docker compose up` running, inspect the output, and restore
      seed data afterwards.
- [x] 9.4 **Run** the existing candidate, search, catalog, preset and navigation e2e specs to confirm
      the permission rename and the new sign-in did not break them.

## 10. Security gates and documentation

- [x] 10.1 Add `tests/security/ktl-16-admin-boundary.spec.ts` asserting the administration surface
      fails closed for unauthenticated and unauthorized callers.
- [x] 10.2 **Run** `npm run security:rls` and `npm run security:storage` and inspect the output.
- [x] 10.3 Write `docs/ktl-16/authentication-and-authorization.md`: the token contract, the subject
      claim, how effective permissions are resolved, and the filled permission table from the ticket
      with every row resolved.
- [x] 10.4 Write `docs/ktl-16/runbook.md`: deploying with the bootstrap administrator, recovering an
      administrator against the database, and the rollback order from design.
- [x] 10.5 Write `docs/ktl-16/release-notes.md` stating plainly that browser-local role
      customisations are lost and the seeded set is the source of truth.
- [x] 10.6 Update `README.md` (Spanish) with how to sign in for local development, keeping commands,
      paths, flags and identifiers untranslated.
- [x] 10.7 Update `AGENTS.md`: remove the legacy Supabase/`localStorage` note for the paths this
      change closes, and correct the permission vocabulary description.

## 11. Done checks

- [x] 11.1 **Run** `npm run build:all` and confirm it is clean (warnings are errors).
- [x] 11.2 **Run** `npm run lint` and `npm run format:check`.
- [x] 11.3 **Run** `npm test` and `npm run test:backend` once more against the final tree and inspect
      both outputs.
- [x] 11.4 Confirm no `localStorage` or Supabase data path was introduced, and that
      `rrhh-demo-profile`, `rrhh-admin-users` and `rrhh-admin-roles` are absent after a first load of
      the new build.
- [x] 11.5 Run `openspec validate ktl-16-identity-and-access-control --strict`.
