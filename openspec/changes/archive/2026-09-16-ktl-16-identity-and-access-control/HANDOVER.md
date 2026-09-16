# KTL-16 handover — sections 0–4 done, resume at section 5

Written for: the agent picking up `/opsx:apply ktl-16-identity-and-access-control` from task 5.1.

## State

Branch `feat/KTL-16`, **27 of 69 tasks complete** (sections 0, 1, 2, 3, 4). Nothing is committed
yet — the whole change is in the working tree. `dotnet build` is clean (0 warnings, warnings are
errors) and `npm test` passed 242/242 at the end of section 1.

Start with `openspec instructions apply --change "ktl-16-identity-and-access-control" --json` and
read `proposal.md`, `design.md`, `specs/`, `tasks.md` as usual. This file only covers what the
artifacts do not already say.

## Two decisions taken during implementation that changed the artifacts

**1. `export_candidates` was kept, not removed.** The proposal claimed there was no export code
path in `src/app/features/**`. That was wrong: `advanced-search-page.tsx:179-222` has a working
client-side CSV export gated on the permission. Removing it would have ungated candidate personal
data. The user was asked and chose to keep it, renamed to **`candidates.export`**. `proposal.md`
and `design.md` (D10) were corrected to record this. It is in `Permissions.All` and
`ALL_PERMISSIONS`, documented as browser-only pending a server-side export ticket, alongside
`candidates.import` (KTL-17).

`view_all_candidates` **was** removed as planned.

**2. `feat/KTL-16` was branched from `main` after fast-forwarding `main` to
`hotfix/minor-updates`** (commit cc36c3b, the Vite :4300 port change), because the section 9 e2e
tasks depend on it. The user chose this over branching from `main` as written.

## Design choices made where the artifacts left room

These are all commented in the code; this is the index.

- **Identity resolution is middleware, not a lazy property.** `ICurrentActor` is synchronous and
  resolution is a database read, so `IdentityResolutionMiddleware` (in
  `backend/Web/Identity/TokenCurrentActor.cs`) does the async work once per request and calls
  `TokenCurrentActor.Resolve(...)`. An unresolved instance answers "not authenticated", so a
  middleware that did not run fails closed.
- **`.RequireAuthorization()` is a fallback policy, not per-group.** `Program.cs` sets
  `options.FallbackPolicy` so a business endpoint group added later without an authorization line
  fails closed by default. Health endpoints and `/api/dev/token` carry `.AllowAnonymous()`. The
  fallback and the resolution middleware are both **skipped when `DevelopmentActor:Enabled`**,
  because there is no token in that mode — that is the composition-time branch of design D11.
- **The bootstrap administrator is seeded by the `--migrate` entry point, not the migration.** A
  migration cannot read `Authentication:BootstrapAdministrator`. The migration seeds the five
  system roles (fixed data, fixed uuids, `ON CONFLICT DO NOTHING`) and the grants;
  `DatabaseInitializer.SeedBootstrapAdministratorAsync` seeds the administrator and **throws** when
  the installation would be left with none. `docs/ktl-16/runbook.md` (task 10.4, not written yet)
  is referenced by that exception message — write it.
- **`ADM_Users.ExternalSubject` is nullable with a partial unique index.** A user seeded or created
  by an administrator has no provider subject until that person first signs in;
  `User.LinkExternalSubject` attaches it then, matching on email. This is what makes both the
  bootstrap administrator and `POST /api/admin/users` work before anyone has signed in.
- **`Role.Permissions` is jsonb behind a value converter, so it has no SQL translation.** Two
  places work around this by materializing `ADM_Roles` (a handful of rows) and filtering in memory,
  then counting _users_ in the database: `UserRepository.CountActiveHoldersOfPermissionAsync` and
  `DatabaseInitializer.HasActiveAdministratorAsync`. The user count is the one that races, and it
  does reach the database. Do not "optimize" these into a join — it will not translate.
- **`IUserRepository` gained `CountActiveHoldersOfRoleAsync`** beyond what tasks.md listed; the
  role-edit lockout rule needs it to subtract the role's own holders from the administrator count.

## Verified by hand against the running stack (not yet by automated tests)

Section 5 is exactly the work of turning these into tests. All of the following were confirmed
with curl against `docker compose` on :4200:

- no token → 401; valid token → 200; tampered signature → 401
- readonly caller → 403 on `/api/admin/users`, 200 on `/api/me`
- JIT provisioning of an unknown subject into `readonly`
- bootstrap administrator seeded by email, subject linked on first sign-in
- `admin.user.selfRoleChange`, `admin.user.selfDeactivation`, `admin.role.systemDeactivation`,
  `admin.role.versionConflict`, `validation.failed` (unknown permission), and
  `admin.lastAdministrator` on the role-edit path — each as ProblemDetails with its stable code
- grants: `ktl_runtime` holds `SELECT, INSERT, UPDATE` and **no `DELETE`** on both tables

## Things that will bite you

- **`docker-compose.yml` now runs the real token flow** (`DevelopmentActor__Enabled: 'false'`,
  `Authentication__DevelopmentIssuer__Enabled: 'true'`). The frontend does **not** send a token
  yet — that is section 6 — so **the app at :4200 and :4300 is currently broken in the browser and
  every existing e2e spec will fail until section 6 lands.** This is expected mid-change, not a
  regression to chase. Backend integration tests are unaffected (they set their own configuration).
- **Existing integration tests still pass `DevelopmentActor:Enabled=true`** and therefore exercise
  the _other_ branch. Task 5.8 is about reviewing them; task 5.5 needs a test that boots with the
  dev actor **off** and proves an absent token does not resolve to it.
- `DevelopmentActorOptions.Permissions` now exists and defaults to the original nine values, so
  those tests keep passing untouched. Use it (task 5.3) to make an authenticated-but-unauthorized
  caller: override `DevelopmentActor:Permissions:0` and so on.
- **Test doubles implementing `ICurrentActor` need the new `Guid? UserId` member.** Six were
  already updated (`CandidateApiTests`, `CatalogApiTests`, `SearchApiTests`,
  `CandidateHandlerTests`, `CatalogHandlerTests`, `SearchHandlerTests`); any new one needs it too.
- `backend/Web/Identity/DevelopmentActor.cs` uses `using Catalogue = ...Identity.Permissions;`
  because the file's own namespace ends in `Identity` and shadows the catalogue. Same trap in
  `Domain/Identity/User.cs`, where the `RoleName` _property_ shadows the `RoleName` static class.
- Frontend object literals keyed by permission now need **quoted keys** (`'candidates.read':`), the
  dotted names are not valid identifiers. This already bit `primary-nav.tsx` and `nav-items.spec.ts`.

## How to get a token for manual probing

```sh
curl -s -X POST http://localhost:4200/api/dev/token -H 'Content-Type: application/json' \
  -d '{"subject":"dev-admin-oid","displayName":"Administrador local","email":"admin@kepler-talento.local"}'
```

That subject/email is the seeded administrator (`rrhh_admin`). Any other subject provisions a new
`readonly` user on first use.

## Remaining sections

5 (backend tests and security evidence) → 6 (frontend transport and session, MSAL, removes Supabase
and MFA) → 7 (admin screens) → 8 (frontend tests) → 9 (e2e) → 10 (security gates and docs) →
11 (done checks). Section 6 is what makes the app work in a browser again.
