# Kepler Talento — agent rules

Shared instructions for every coding agent (Codex reads this file directly; Claude Code
imports it from `CLAUDE.md`). Edit rules here, never in a tool-specific copy.

Kepler Talento is an internal HR application for registering, searching, exporting and
governing candidate CVs. It holds **personal data**. The frontend is a React SPA; the
target backend is ASP.NET Core 10 + application-owned PostgreSQL, rolled out one vertical
slice at a time (KTL-5 onwards). Remaining `localStorage` paths are legacy and stay only
until their slice migrates; identity, users, roles and candidate import (KTL-17) are API-owned.

## Sources of truth

Read these before non-trivial work instead of guessing:

- `openspec/config.yaml` — standing principles, domain rules and per-artifact rules. It
  wins over anything else when they disagree.
- `openspec/specs/<capability>/spec.md` — current requirements per capability.
- `openspec/KTL-<n>.md` — ticket briefs. `openspec/changes/` — in-flight work;
  `openspec/changes/archive/` — delivered changes with their design decisions.
- `docs/ktl-<n>/` — contracts, runbooks and release notes per slice. Start with
  `docs/ktl-5/database-conventions.md` for anything touching PostgreSQL.
- `specs/001-gestion-cvs-rrhh/` — historical design record of the original app. New work
  goes through OpenSpec, not here.

## Commands

Requirements: Node 22 / npm 10, .NET SDK 10.0.102 (`global.json`), Docker Desktop.

The SPA is a self-contained npm project in `frontend/` (its `package.json`, `node_modules/`,
configs, `src/` and `tests/`). **Run every `npm`/`npx` command from `frontend/`**; the
`dotnet` and `docker compose` commands run from the repository root. `backend/`, `docs/`,
`openspec/`, `scripts/` and `supabase/` stay at the root, and frontend specs that read them
resolve paths through `frontend/tests/repo-root.ts` rather than `process.cwd()`.

```sh
dotnet tool restore && dotnet restore backend/KeplerTalento.slnx   # setup (root)
cd frontend && npm ci         # setup (frontend/); every npm/npx line below runs here
npm start                     # Vite dev server on :4300 (strict port), proxies /api to the stack on :4200
docker compose up --build     # (root) full stack behind Nginx on http://localhost:4200 (copy .env.example to .env first)

npm test                      # Vitest: unit + integration + security projects
npx vitest run tests/unit/candidate.service.spec.ts   # single frontend spec
npm run test:backend          # xUnit; needs Docker running (Testcontainers PostgreSQL) and a prior restore
dotnet test backend/KeplerTalento.slnx --no-restore --filter "FullyQualifiedName~CatalogHandlerTests"   # (root)
npm run e2e                   # Playwright against the dev server on :4300 (starts it itself); needs `docker compose up`
npx playwright test tests/e2e/candidate-crud.spec.ts

npm run build:all             # tsc + vite build + dotnet build (warnings are errors)
npm run lint && npm run format:check                 # required before any change is done; format:check covers the whole repo
npm run security:rls && npm run security:storage     # security gates
```

EF Core migrations (schema changes only through these, never hand-written SQL):

```sh
dotnet ef migrations add <Name> --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations
```

The API never migrates on normal startup; migrations run through the explicit `--migrate`
entry point (the `migrator` container in Compose).

## Non-negotiables

These come from `openspec/config.yaml`. Breaking one is a defect even if tests pass.

1. **Personal data by design.** Candidate identity, contact details, consent and
   retention data, audit data and documents are personal data. Expose the minimum, never
   log it (Serilog runs `PersonalDataRedactionEnricher`; do not work around it), and never
   put internal storage paths or keys in responses.
2. **The API is the boundary.** Browser code reaches PostgreSQL, files, scanning and
   privileged work only through the ASP.NET Core API. Do not add new Supabase or
   `localStorage` data paths. Any new runtime dependency (npm or NuGet) needs a documented
   reason in the change's design.
3. **Fail closed.** Every business endpoint checks `ICurrentActor` authentication and the
   specific permission **before** validating or dispatching the request. Hiding UI is never
   the control. The `DevelopmentActor` must stay impossible to enable in Production.
   Permission names use the single `<resource>.<action>` vocabulary shared by API and SPA;
   do not add underscore-style aliases or permission claims to tokens.
4. **Least privilege.** DDL runs as `ktl_migrator`; the API runs as `ktl_runtime` with
   DML limited to approved tables. A migration ships its constraints, indexes and runtime
   grants in the same slice.
5. **Private documents.** Binaries live outside the webroot under opaque keys, stay
   quarantined until ClamAV reports `Clean`, and download only via permission-checked API
   responses.
6. **Search semantics.** Empty filters are ignored; different filter families combine with
   AND; multi-value families support ANY and ALL; results never contain duplicate candidates.
7. **Never commit** `.accdb`/`.mdb` files, `.env`, exported candidate data, CV files,
   `backups/`, credentials or private storage paths.

## Workflow

- Work is ticket-driven: `KTL-<n>` briefs → OpenSpec change → implementation. Use the
  OpenSpec skills rather than hand-editing change artifacts. Both tools have them in git:
  Codex in `.agents/skills/` (`$openspec-new-change`, `$openspec-continue-change`,
  `$openspec-ff-change`, `$openspec-apply-change`, `$openspec-verify-change`,
  `$openspec-sync-specs`, `$openspec-archive-change`), Claude Code in `.claude/skills/`
  plus the `/opsx:*` shortcuts. Regenerate them with `openspec update`, never by hand.
- To turn a thin brief into a complete user story before creating a change, use the
  `enrich-us` skill: `$enrich-us KTL-<n>` (Codex) or `/enrich-us KTL-<n>` (Claude Code).
  Its single source is `.agents/skills/enrich-us/SKILL.md`.
- Change folders are named `ktl-<n>-<slug>`; archived ones keep the ticket segment after the
  date prefix (`2026-08-25-ktl-6-catalog-write-slice-api-cutover`).
- Do not mark a task done unless its command was actually run and its output inspected. A
  slice touching personal data, permissions, grants or storage is not done without security
  evidence (tests that fail closed for unauthenticated and unauthorized callers).
- Update `docs/`, `README.md` or the relevant spec for anything user-visible or
  contract-changing, in the same change.

## Backend conventions (`backend/`)

- Vertical slices across projects: `Domain` → `Application` → `Infrastructure` → `Web`.
  The allowed reference graph is enforced by
  `Tests/UnitTests/Architecture/ProjectDependencyTests.cs`. `Tools/DataMigration`
  (`ktl-migrate`) is operator-only: no production project may reference `Tools/`.
- A feature lives in one file per use case under `Application/Features/<Feature>/`: a
  `sealed record` command/query implementing MediatR `IRequest<T>`, its FluentValidation
  validator, and a `sealed` handler with primary-constructor dependencies. Handlers repeat
  the permission guard (e.g. `CatalogGuards.RequireManage(actor)`).
- HTTP lives in `Web/Features/<Feature>/<Feature>Endpoints.cs` as a static
  `Map<Feature>Endpoints` extension using `MapGroup("/api/<feature>")`, request records
  nested in the class, `.WithName()` and explicit `.Produces*` metadata, and dispatch via
  `ISender`. Register the map call in `Program.cs`.
- Errors are thrown as the exception types in `Application/Common/Errors` and mapped to
  ProblemDetails by `GlobalExceptionHandler`; do not build error responses by hand.
- Updates use optimistic concurrency (`Version` in requests → 409 on mismatch).
  New ids use `Guid.CreateVersion7()`. Timestamps are `DateTimeOffset` UTC.
- Persistence: repository interfaces in `Application/Abstractions`, implementations and
  EF configurations in `Infrastructure/Persistence`. Physical tables use a registered prefix
  (`CND_`, `CAT_`, `OPS_`, `AUD_`, `ADM_`) and quoted identifiers; C# names stay idiomatic
  without prefixes. New prefixes need an architecture decision.
- NuGet versions are pinned centrally in `backend/Directory.Packages.props`; do not put
  versions in `.csproj` files. Nullable is on and warnings are errors.
- Tests: xUnit, `sealed` test classes, sentence-style names
  (`List_excludes_inactive_values_by_default`). Unit tests use hand-written doubles;
  integration tests use the Testcontainers `PostgreSqlFixture` against a real database.

## Frontend conventions (`frontend/`)

Paths in this section are relative to `frontend/`.

Since KTL-3 (Angular → React migration):

- Pages and components are React function components under `src/app/features/**` and
  `src/app/core/**`, written as `.tsx` with co-located plain `.css` files. Do not use CSS
  Modules — several Playwright specs bind to class names such as `a.skip-link` and
  `span.badge`.
- Shared state lives in plain singleton service classes that hold a signal from
  `src/app/core/state/signal.ts`. Components subscribe with `useSignal()`. Pass the signal
  itself, never a derived call such as `service.list()`, or React will loop on
  `getSnapshot`. Derive with `useMemo` instead.
- Services are wired explicitly in `src/app/core/di/services.ts` and reached through
  `useServices()`. Tests swap doubles in with `<ServicesProvider>`. API calls go through
  `src/app/core/http/api-transport.ts`; authorization logic belongs in the API, not
  duplicated in services.
- If a service is READ during render, reach it through its subscribing hook —
  `useCatalogs()`, `useCandidates()`, or `usePermission()` for authorisation checks — not
  through `useServices()`. Those hooks subscribe and return the value together. Taking the
  service from `useServices()` and calling a read method on it renders correctly once and
  then silently stops updating, with no error and no failing test. A new service that is
  read during render should get the same kind of hook.
- Never call `authService.hasPermission()` directly in a component. Use
  `usePermission('...')` at the top of the component. Because it is a hook it cannot be
  called inside a loop or a callback, so hoist the result to a const.
- Report errors with `useErrorToast()` rather than hand-rolling
  `error instanceof Error ? error.message : fallback`.
- Use the `.span-all` utility class instead of inline `style={{ gridColumn: '1 / -1' }}`;
  inline styles bypass the Kepler tokens (`docs/CORPORATE_IDENTITY_Kepler.md`).
- Keep pure helpers and constants in a sibling `.ts` file (e.g. `*.logic.ts`) rather than
  exporting them from a `.tsx` component module, so fast refresh keeps working.
- Shell navigation is data, not markup. Add a section by adding an entry to
  `src/app/core/layout/nav-items.ts` — never by adding another `NavLink` to
  `app-layout.tsx`. Administration sections (Catálogos, Usuarios, Roles, Importación) live
  under the Admin group, whose parent is a disclosure button with no route. If the new
  entry needs a permission the nav does not already consult, add it to `NAV_PERMISSIONS`
  and call `usePermission()` for it at the top of `PrimaryNav` — the hook cannot be called
  while iterating the table.
- The shell has a single breakpoint at 768px, and it lives entirely in `primary-nav.css`.
  Do not branch on `window.innerWidth` or `matchMedia`: one DOM tree serves both widths.
  jsdom has no media queries, so unit specs assert on presence and `aria-expanded`, and
  layout is covered by `tests/e2e/navigation-responsive.spec.ts` instead.
- Route guards are layout-route elements (`RequireAuth`, `RequirePermission`), not
  loaders, so they can subscribe to the auth signal and stay test-swappable.
- Forms are controlled components using `useState`. Validation stays in the service layer;
  do not move it into the components or introduce a schema library. New or changed
  validation throws `TranslatableError(key, values)` (`src/app/core/i18n/`) and components
  render it with `errorText(err, t)`. Existing plain `Error`s with Spanish messages stay
  until their code is touched.
- Do not reintroduce Angular idioms, RxJS, or a state-management library.
- Keep the `name=` attribute on every form control and every `data-testid`. They are
  decorative in React but load-bearing for the Playwright suite.
- Tests live outside `src/`: `tests/unit/*.spec.ts(x)` (jsdom + Testing Library, shared
  doubles in `tests/unit/support/`), `tests/integration/`, `tests/security/`, `tests/e2e/`.
- Every record an e2e spec creates (candidate, position, catalog item, user, role, preset,
  import file) must carry a `Date.now()` value in its name, title, code, e-mail or file
  name. The Playwright global teardown (`scripts/e2e-cleanup.js`) purges test data by that
  13-digit marker, so an unmarked record would stay in the development database. Seeded
  fixtures shared across runs (e.g. `ensureSearchCandidate`) stay unmarked on purpose.
- Formatting is Prettier (single quotes, trailing commas, width 100); the pre-commit hook
  runs it through lint-staged.

## Language

- **UI copy is Spanish**, with correct accents and wording. It lives in
  `frontend/src/assets/i18n/es.json` under flat `feature.section.element` keys and is rendered with
  `t()` from `react-i18next`. Spanish is the only active language; there is no switch.
  - New or changed JSX copy is never hardcoded. `npm run lint` enforces this for every
    `.tsx` outside `LEGACY_HARDCODED_COPY` in `frontend/eslint.config.js`. Attribute copy
    (`placeholder`, `aria-label`, `title`) is not caught by lint but follows the same rule.
  - That list only shrinks. When you change copy in a listed file, move the file's copy to
    keys and remove it from the list in the same change. Never add a file to it.
  - Use whole sentences with interpolation (`t('key', { count })`), never concatenated
    fragments. Format dates and numbers with `formatDate`/`formatNumber`, and catalog
    names with `catalogLabel`, instead of a hardcoded locale or `nameEs`.
  - Adding the English value to `en.json` is welcome but not required.
  - New tests locate elements by role and accessible name, label or `data-testid`. Unit
    tests may assert on Spanish text resolved from `es.json`; new e2e selectors must not
    hardcode Spanish text.
  - Never translate a Spanish literal the app actually renders.
- **Prose is English**: code comments, OpenSpec artifacts, ticket briefs, specs and
  `docs/`.
- **Exception: `README.md` is maintained in Spanish.** Keep commands, paths, flags and
  identifiers untranslated inside it.

## Git

- Branches: `feat/KTL-<n>` for tickets, `hotfix/<slug>` and `chore/<slug>` otherwise.
  Never commit directly to `main`.
- Commit messages: `KTL-<n> - <Summary in imperative mood>`.
- Only commit, push or open a pull request when explicitly asked. PRs use
  `.github/pull_request_template.md`.
- Never skip hooks (`--no-verify`) or rewrite published history.

## Environment gotchas

- Development happens on Windows. **Do not bulk-edit files with Windows PowerShell
  `Get-Content | Set-Content` or `-replace` pipelines**: PowerShell 5.1 reads UTF-8 as
  Windows-1252 and silently corrupts Spanish accents and em dashes. Use the agent's file
  editing tool; if a script is unavoidable, use `[IO.File]::ReadAllText/WriteAllText` with
  explicit UTF-8 (no BOM).
- Ports are split on purpose. **4200** is the Compose nginx serving the bundle built into
  its image, which can be days old; **4300** is the Vite dev server serving the working tree
  and proxying `/api` to 4200 (override with `KTL_API_PROXY_TARGET`). Playwright targets
  4300, so e2e always tests current code. Browsing 4200 shows the image's build, not your
  edits, until `docker compose build nginx`. Both ports are strict, so a clash fails
  instead of drifting.
- `docker compose down --volumes` destroys development data (PostgreSQL, documents, ClamAV
  signatures). Do not run it unless asked.
- ClamAV needs ~3 GiB of RAM; a degraded `/api/health/scanner` alone does not mean the API
  is broken.
