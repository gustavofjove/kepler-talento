## Context

See `proposal.md` for motivation and `specs/` for the required behaviour. This design covers how the
API acquires a caller identity and how users, roles and permissions are stored and enforced.

Current state:

- **Actor.** `Application/Abstractions/Identity/ICurrentActor.cs` is three members — `ExternalKey`,
  `IsAuthenticated`, `HasPermission(string)` — plus a `Permissions` class of nine constants.
  `Program.cs:74-75` registers `DevelopmentActor` as the only `ICurrentActor`. Its options are
  validated at start-up so it cannot be enabled in Production, and its permission list is a
  hard-coded `static readonly string[]`.
- **Guards.** Every handler repeats its guard (`CatalogGuards.RequireManage(actor)`), and endpoints
  call a local `Require(actor)` before dispatching through `ISender`. The pattern is right; only the
  actor behind it is fake.
- **Tests.** Integration tests flip `DevelopmentActor:Enabled` through
  `WebApplicationFactory` configuration overrides (`CandidateApiTests.cs:792`,
  `CatalogApiTests.cs:353`, `SearchApiTests.cs:731`). There is no way to run a test as an
  authenticated caller _without_ a given permission, because the permission list is compiled in.
- **HTTP.** FastEndpoints is registered but the feature endpoints are `MapGroup` extensions called
  from `Program.cs`. There is no authentication or authorization middleware at all, and
  `SwaggerDocument` sets `EnableJWTBearerAuth = false`.
- **Persistence.** `ApplicationDbContext` with EF configurations under
  `Infrastructure/Persistence/Configurations`, physical tables prefixed `CND_`, `CAT_`, `OPS_`,
  `AUD_`, `ADM_`, quoted identifiers, `uint Version` mapped to `xmin` via `IsRowVersion()`, ids from
  `Guid.CreateVersion7()`. Migrations carry their own `GRANT` statements to `ktl_runtime`;
  `RevokeCandidateDelete` is the precedent for revoking one.
- **Frontend.** `AuthService` holds a `signal<UserProfile | null>` restored from
  `localStorage['rrhh-demo-profile']`, and `signIn(email, password, role)` fabricates the profile
  from `DEFAULT_ROLES`. `ProfileService` and `RoleService` are synchronous `localStorage` services
  that also hold the last-administrator and system-role invariants. `ApiTransport.send` builds the
  headers and sends no credential. `evict-legacy-storage.ts` already exists with a one-entry
  `SUPERSEDED_KEYS` list.
- **Dependencies.** No authentication package on either side. `@supabase/supabase-js` is a
  dependency used only by `SupabaseClientService`, which `AuthService` calls and nothing else does.

## Goals / Non-Goals

**Goals:**

- Put a validated token in front of the existing guards without rewriting them. The handler-level
  `RequireX(actor)` calls stay exactly as they are; what changes is what `actor` is.
- Keep the whole authorization decision server-side and re-read per request, so revocation is
  immediate and the browser's copy is decoration.
- Make "authenticated but unauthorized" a state a test can produce deterministically, which it
  cannot today.
- Leave the installation administrable: no deployment path that ends with zero administrators, and a
  written way back if one is reached.

**Non-Goals:**

- Recording the actor on audit rows, or any read of `AUD_Events`. That is KTL-19.
- Any change to catalog, candidate, document, search or preset behaviour beyond the permission
  strings naming it.
- Per-record or ownership-based visibility. Removing `view_all_candidates` closes that door rather
  than opening it.
- Group- or claim-driven role assignment from the provider. A user's role is data in `ADM_Users`,
  set by an administrator.

## Decisions

### D1 — Entra ID JWT bearer, validated by `Microsoft.AspNetCore.Authentication.JwtBearer`

The API adds `AddAuthentication().AddJwtBearer()` configured from the tenant's OIDC discovery
document, with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` and
`ValidateIssuerSigningKey` all on and `ClockSkew` reduced from the five-minute default to 60
seconds. Signing keys come from the discovery document and refresh automatically, which satisfies
the key-rotation scenario without a restart.

`app.UseAuthentication()` and `app.UseAuthorization()` go after `UseForwardedHeaders` and the
correlation middleware (so a rejected request still gets a correlation id) and before
`UseFastEndpoints`. Every business endpoint group gets `.RequireAuthorization()`; the health
endpoints and the Swagger document do not.

**New dependency, justified:** `Microsoft.AspNetCore.Authentication.JwtBearer`, pinned in
`Directory.Packages.props`. It is the framework's own package for exactly this, maintained on the
same cadence as ASP.NET Core. The alternative — hand-validating JWS against JWKS — would be new
security-critical code for no benefit.

_Alternative considered:_ cookie-based sessions issued by the API after an OIDC code exchange. It
would avoid a token in the browser, but the API would then own session state and CSRF defence, and
the SPA already talks to the API same-origin through Nginx with bearer semantics everywhere else.

### D2 — `oid` is the subject, not `sub`

Entra's `sub` is pairwise: the same person gets a different `sub` per application registration, so a
`sub`-keyed user row would break the day a second registration appears. `oid` is the object id of
the user in the tenant and is stable across applications. The claim name is configuration
(`Authentication:SubjectClaim`), defaulting to `oid`, so a different provider can be pointed at a
different claim without code.

`ADM_Users` stores it in `ExternalSubject`, unique, and it never appears in a response or a log
(spec: _Identity data is absent from logs and responses_). Responses use the internal
`Guid.CreateVersion7()` id.

### D3 — Permissions resolved from the database on every request, cached for the request only

`ICurrentActor` gains `UserId` (the internal `Guid?`) alongside `ExternalKey`. A scoped
`TokenCurrentActor` reads the subject claim, loads the user and its role once per request, and
answers `HasPermission` from the role's permission set.

Permissions are deliberately **not** carried as token claims. Entra app roles would be free to
check but would leave a revoked permission live until the token expired, and the ticket requires a
change to take effect on the next request. Reading the user and role is one indexed lookup on a
table with as many rows as the company has HR staff.

Caching is per request only. A cross-request cache would reintroduce exactly the revocation delay
the claim approach was rejected for; if the lookup ever becomes a measured problem, the mitigation
is a short in-memory cache with an explicit invalidation on every role and user write, and that is a
later change with its own evidence.

Two consequences the specs already state: a user whose role is inactive holds no permissions but is
still authenticated, and a deactivated user is refused as _unauthenticated_ rather than forbidden,
because the point is that they have no standing at all.

### D4 — Just-in-time provisioning with a least-privileged default role

The first validated token for an unknown subject inserts a user with role `readonly`. The insert is
guarded by the unique index on `ExternalSubject` and a duplicate-key violation is caught and turned
into a re-read, which is how the concurrent-first-request scenario is satisfied without a lock.

_Alternative considered:_ invitation-only, refusing an unknown subject with 401. It keeps the user
list closed, but every new hire needs an administrator round-trip before they can even see the
dashboard, and the closed list buys little when the provider already decides who may authenticate at
all. JIT with a read-only default gives the same protection — the new user can do nothing
interesting — without the round-trip.

Display name and email are refreshed from the token's claims on every request where they differ, so
a rename at the provider propagates without an administrator touching anything.

### D5 — Roles are data; five system roles are seeded

`ADM_Roles` holds `Name` (immutable, `^[a-z0-9_]+$`, unique), `Label`, `IsSystem`, `Permissions`
(jsonb array of catalogue strings), `IsActive`, `Version`, timestamps. The five `DEFAULT_ROLES` from
`auth.models.ts` are seeded as system roles by the migration, keeping their current names so nothing
in the UI or the e2e suite has to learn new ones.

System roles may have their **label and permission set** changed but may not be renamed, emptied or
deactivated. An installation that wants `rrhh_user` to stop downloading documents should not have to
clone the role to do it; what must not happen is a system role vanishing or becoming meaningless
while users hold it.

The permission catalogue stays a `Permissions` class in `Application/Abstractions/Identity`, with a
new `Permissions.All` array that the role validator checks against and that a test compares with the
frontend's `ALL_PERMISSIONS`.

### D6 — Invariants live in one place, checked inside the transaction

The last-administrator rule, the self-role and self-deactivation rules and the system-role rules
move out of `profile.service.ts` into an `AdminInvariants` type in `Application/Features/Admin`,
called by the user and role handlers after the permission guard and before the save, inside the same
transaction as the write. Checking "is there another active administrator?" outside the transaction
is a race that deactivates the last two administrators concurrently.

"Administrator" means _an active user whose active role grants `users.manage`_ — not a role name.
Defining it by permission is what makes the rule survive an installation that renames or reshapes
its roles.

The rule is enforced on three write paths, not one: deactivating a user, changing a user's role, and
removing `users.manage` from a role. The third is the one `profile.service.ts` never had, because
roles could not be edited into a lockout from the users screen.

### D7 — `ADM_Users` and `ADM_Roles`, deactivation only

| Column      | `ADM_Users`                                                  | `ADM_Roles`                              |
| ----------- | ------------------------------------------------------------ | ---------------------------------------- |
| Key         | `Id` uuid v7                                                 | `Id` uuid v7                             |
| Natural key | `ExternalSubject` unique, `Email` unique lower               | `Name` unique                            |
| Data        | `DisplayName`, `RoleName`, `LastSignInAtUtc`                 | `Label`, `IsSystem`, `Permissions` jsonb |
| Lifecycle   | `IsActive`, `Version` (xmin), `CreatedAtUtc`, `UpdatedAtUtc` | same                                     |

`Email` is stored lower-cased with a unique index on the lower-cased value, matching how catalog
names are compared. `RoleName` is a foreign key to `ADM_Roles.Name`, which is why `Name` is
immutable: the alternative is cascading updates on a natural key.

Grants in the same migration: `GRANT SELECT, INSERT, UPDATE ON "ADM_Users", "ADM_Roles" TO
ktl_runtime` and no `DELETE`, mirroring `RevokeCandidateDelete`. The grants test asserts the absence
rather than trusting the migration text.

`Version` is `uint` mapped to `xmin` with `IsRowVersion()`, as every other aggregate does, so
optimistic concurrency needs no new pattern.

### D8 — A dev-only token issuer, not a second identity provider in Compose

Development and e2e need a real token flow without the corporate tenant. The stack gains a
`Development`/`Testing`-only issuer inside the API: a signing key from configuration, a
`POST /api/dev/token` endpoint that mints a token for a named subject and display name, and the
JwtBearer handler configured to additionally trust that issuer when — and only when — the
environment is not Production. Start-up fails if the dev issuer is configured in Production, by the
same `ValidateOnStart` mechanism that already guards `DevelopmentActor`.

This is a real token flow: the token is signed, and the same validation pipeline checks its
signature, issuer, audience and lifetime. Only the issuer differs. It is not a bypass — there is no
code path that skips validation.

_Alternative considered:_ Keycloak as a Compose service. It is the more faithful rehearsal of the
production path, but it is a third heavyweight container next to PostgreSQL and a ClamAV that
already wants 3 GiB, it needs its own realm fixture to stay reproducible, and it makes `npm run e2e`
depend on a container reaching readiness. Rejected for weight, and recorded here so a later change
can revisit it if the dev-vs-prod divergence in D9 starts costing real bugs.

**Divergence risk accepted:** the MSAL acquisition path is exercised in staging and production, not
locally. D9 keeps that path behind one interface so the untested part is as small as possible.

### D9 — One token source interface in the browser, two implementations

`ApiTransport` gains a `tokenProvider: () => Promise<string | null>` constructor dependency and
attaches `Authorization: Bearer` in `send()`, next to the correlation header. A 401 response is
already mapped to `AppError('UNAUTHENTICATED')`; the transport additionally notifies the session so
`AuthService` can clear and redirect. The token never goes to `localStorage` — MSAL keeps it in
session storage in memory-first configuration, and the dev implementation holds it in a module
variable.

`AuthService` keeps its `signal<UserProfile | null>` shape, so `usePermission`, the route guards and
every component keep working unchanged. What changes is where the profile comes from: sign-in
acquires a token, then `GET /api/me` fills the signal. `DEFAULT_ROLES` leaves the browser entirely.

**New dependency, justified:** `@azure/msal-browser`. Entra's authorization-code-with-PKCE flow,
token caching and silent renewal are not something to hand-roll in an application that holds
personal data. `@azure/msal-react` is _not_ added — the React binding is a thin wrapper and this
codebase wires services explicitly through `di/services.ts` rather than through context providers.

`@supabase/supabase-js` is removed in the same change, so the net dependency count is unchanged.

### D10 — The frontend permission rename is mechanical and enforced by a test

`Permission` becomes the union of `<resource>.<action>` strings, generated from the same list the
API exposes. A new unit test asserts that `ALL_PERMISSIONS` equals the catalogue returned by the
API contract, so the two cannot drift silently — which is the whole reason for choosing one
vocabulary over a mapping.

`view_all_candidates` is deleted rather than renamed: KTL-10 left it inert and the domain models no
scope it could narrow.

`export_candidates` was also slated for deletion on the belief that nothing used it. Implementation
found otherwise — `advanced-search-page.tsx` gates a working client-side CSV export on it — so it is
renamed to `candidates.export` and kept. Deleting it would have been a silent widening of access to
candidate personal data, which principle 1 does not allow on the strength of a claim that turned out
to be false.

`candidates.import` and `candidates.export` are therefore the two permissions whose only enforcement
point is still the browser. Both carry a comment naming the change that will give them a server-side
guard — KTL-17 for import, a dedicated export ticket for export. Recording them is better than
pretending the catalogue is clean.

### D11 — `DevelopmentActor` gains a configurable permission set

`DevelopmentActorOptions` gains `string[] Permissions`, defaulting to the nine currently hard-coded
values so existing tests keep passing untouched. A test that needs an authorized-but-unauthorized
caller sets `DevelopmentActor:Permissions:0` and so on through the same configuration override the
suite already uses.

`DevelopmentActor` is registered as `ICurrentActor` **only** when it is enabled; otherwise
`TokenCurrentActor` is. There is no chained fallback: the registration is a branch at composition
time, which is what makes "an absent token never resolves to the development actor" provable rather
than asserted.

## Risks / Trade-offs

- **Lockout on deploy.** A migration that seeds roles but no administrator leaves nobody able to
  administer. → The migration seeds the bootstrap administrator from
  `Authentication:BootstrapAdministrator` (an email plus a display name) and the `--migrate` entry
  point fails loudly when it is absent and no active administrator exists. The runbook documents
  restoring one with a single `UPDATE` as `ktl_migrator`.
- **Local role customisations are lost.** Roles live in each browser's `localStorage` with no
  central copy. → The seeded set is the source of truth; the release notes say so plainly rather
  than attempting a migration that cannot see the data.
- **Dev and production sign-in differ (D8/D9).** A bug in the MSAL path will not show up locally. →
  The divergence is confined to token acquisition behind one interface; validation, authorization
  and every screen are identical. A staging smoke test against the real tenant is part of the
  release checklist.
- **Per-request user lookup adds a query to every call.** → One indexed read on a small table,
  measured in the integration suite's query-plan test the way search already is. The revocation
  requirement makes it the correct trade, and D3 names the escape hatch if it stops being.
- **A permission-name rename touches most of `src/`.** A missed call site is a type error, not a
  silent hole, because `Permission` is a closed union — but `es.json` keys and `data-testid` values
  near them are not type-checked. → The rename lands as its own task before behaviour changes, so a
  failure is a compile error in a small diff rather than a mystery in a large one.
- **Entra tenant availability could block the whole change.** → D8 removes the dependency for
  development and CI entirely; only staging verification needs the tenant.
- **Deactivating a user is refused as 401, not 403.** A deactivated user sees "signed out" rather
  than "no longer permitted", which is slightly less informative. → Deliberate: a deactivated
  account should not be told it still exists, and the browser's redirect-to-sign-in on 401 is the
  right outcome for them anyway.

## Migration Plan

1. `AddIdentityTables` migration: create `ADM_Roles` and `ADM_Users` with their constraints and
   indexes; seed the five system roles; seed the bootstrap administrator from configuration; grant
   `SELECT, INSERT, UPDATE` to `ktl_runtime` and no `DELETE`. Runs through the existing `--migrate`
   entry point as `ktl_migrator`, never on normal start-up.
2. Deploy the API with authentication on. Until the SPA ships, the only callers are the dev issuer
   and the migrator, so there is no window where an unauthenticated browser reaches a business
   endpoint.
3. Deploy the SPA. `evictSupersededStorage` removes `rrhh-demo-profile`, `rrhh-admin-users` and
   `rrhh-admin-roles` on first load, before any API call.
4. Verify against staging with a real Entra token: sign in, `GET /api/me`, administer a user,
   confirm a revoked permission bites on the next request.

**Rollback.** The API rolls back by redeploying the previous image, whose `DevelopmentActor`
registration does not consult the new tables; `Down` drops `ADM_Users` and `ADM_Roles` and the
seeded rows with them. Rolling back after users have been created loses only their role assignments
— identities come from the provider and are recreated by JIT on the next roll-forward. The SPA
rolls back independently, but a rolled-back SPA against a rolled-forward API cannot authenticate at
all, so the documented order is SPA first, then API.

## Open Questions

- The exact Entra application registration (tenant id, client id, exposed API scope) comes from
  whoever administers the corporate tenant. It is configuration, not design: nothing above changes
  depending on the values, and D8 means development and CI do not wait for them.
- Whether the bootstrap administrator should be identified by email or by `oid`. Email is friendlier
  to write into a deployment variable and is what the runbook will use; `oid` is exact. The
  migration seeds by email and links the subject on first sign-in (D7), which works either way, so
  this can be revisited without touching the schema.
