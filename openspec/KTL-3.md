## [original]

Migrate to React

https://claude.ai/code/artifact/d24f3b98-f04d-492d-a05b-d7d3e3b8804c

## [enhanced]

# KTL-3 — Migrate the frontend from Angular 21 to React 19

> Turned into OpenSpec change [`migrate-frontend-to-react`](changes/migrate-frontend-to-react/) on 2026-08-20. That change's `proposal.md`, `design.md` and `tasks.md` are the executable artifacts; this brief is the input that produced them.

## Context

The link in the original brief points to the technical review **"RRHH BBDD — Revisión técnica"** (10 Aug 2026). It is worth being precise about what that review does and does not say:

- **It does not propose migrating to React.** Its roadmap is backend and security work: `C-1` all persistence lives in `localStorage` and Supabase is never queried, `C-2` the role is chosen by the user from a dropdown at login, `C-3` the Edge Functions do not verify the bearer token, `C-4` candidate personal data sits in the browser in clear.
- **Its only stack-related finding is `M-4`**: "Angular 21 used with Angular 15 patterns" — no `ChangeDetectionStrategy.OnPush`, no `computed()`, inline templates over 400 lines, untyped template-driven forms.

The decision to move to React is a product decision, not a review finding. This ticket treats it as such and **deliberately excludes** `C-1`..`C-4` and `A-1`..`A-5`, which remain independent tickets. Mixing the framework change with Supabase wiring would combine two unrelated risks on one branch and double the porting work.

**Product motivation:** align the frontend with the team's skill set and the wider Kepler ecosystem.

**What makes this cheap today:** the app uses no `NgModule`, no RxJS, no HTTP interceptors, no lazy routes and no state library. The 16 services are plain classes holding `signal()` state over `localStorage`, and **13 Playwright specs** already cover the product end to end and can act as an objective regression net.

## User stories

> As a **Kepler Talento user**, I want the application to behave exactly as before after the framework change, so I do not have to relearn anything and do not lose my data or my saved search presets.

> As a **member of the development team**, I want a React frontend with the business logic untouched, so we can keep shipping features (e.g. KTL-4, the "Posiciones" section) without carrying obsolete Angular patterns.

The functional success criterion is, by design, **the absence of visible change**: same screens, same copy, same routes, same permissions, same validation messages, same `localStorage` keys.

## Stack decisions (agreed with product)

| Decision           | Choice                                                       | Rationale                                                                           |
| ------------------ | ------------------------------------------------------------ | ----------------------------------------------------------------------------------- |
| Framework          | **React 19**                                                 | Product decision                                                                    |
| Build / dev server | **Vite**                                                     | Static SPA; preserves the Docker + nginx + `env.js` delivery model                  |
| Routing            | **React Router 7** (`createBrowserRouter`)                   | Data router: allows navigation from outside React (`AuthService.signOut()`)         |
| State              | **`useSyncExternalStore`** over the existing service classes | See below; avoids a new dependency                                                  |
| Forms              | **Controlled components with `useState`**                    | No react-hook-form, no zod                                                          |
| Styling            | **Keep `src/styles.css`; drop Tailwind**                     | Kepler brand tokens transfer 1:1; Tailwind is configured but effectively unused     |
| Unit tests         | **Vitest 3**                                                 | Reads `vite.config.ts` directly: TSX, CSS and `import.meta` with no extra transform |

## Scope

### In

- Port the **23 components** and **16 services** under `src/app/`.
- Replace the build, test and lint configuration.
- **Approved cheap cleanup:** remove dead dependencies, self-host the Google Fonts (`M-3`), associate `for`/`id` on form labels (`M-5`), split components over ~300 lines (partial `M-4`).
- Update the affected documentation.

### Out

- **Supabase wiring.** Persistence stays in `localStorage`, with the same 9 keys byte for byte (`C-1`).
- **Authentication and authorization model.** The login role selector stays as-is (`C-2`) — it is the contract `tests/e2e/global-setup.ts` and all 13 specs depend on.
- SQL migrations, RLS policies, Storage and Edge Functions.
- Any functional, copy or visual design change.
- `<StrictMode>`: ported **without** it to guarantee behavioural identity (React 19 double-invokes effects in development, which could double-fire `toastService.show()`). Added later as a follow-up ticket, once the e2e suite is green.

## Data contract

**No change.** This ticket does not touch Supabase:

- No new migration in `supabase/migrations/`.
- No new or modified Edge Function in `supabase/functions/`.
- No RLS policy, view or RPC function affected.
- `@supabase/supabase-js` and the `src/app/core/supabase/supabase-client.service.ts` wrapper are ported **verbatim**; they remain used only for `signInWithPassword`, `signOut` and `getAuthenticatorAssuranceLevel`.

Actual persistence stays in `localStorage`, and the keys **do not change**:

`rrhh-demo-profile` · `rrhh-candidates` · `rrhh-catalogs` · `rrhh-admin-users` · `rrhh-admin-roles` · `rrhh.search.presets.v1` · `rrhh.search.last-filters.v1` · `rrhh.export.batches.v1` · `rrhh.import.batches.v1`

This is a verifiable requirement: an existing session must keep working after deployment, including the legacy preset-shape migration (`languageValues` → `languageCriteria`) implemented in `search-presets.service.ts`.

## Target architecture

### In-place replacement of `src/`

No parallel folder. The 15 unit specs import via `../../src/app/...`, and `openspec/config.yaml`, `specs/001-gestion-cvs-rrhh/plan.md` and `.claude/commands/enrich-us.md` all reference those paths. Preserving the tree keeps those documents correct and allows milestone-by-milestone progress instead of one "big-bang" commit.

| Element      | Convention                                                                                                       |
| ------------ | ---------------------------------------------------------------------------------------------------------------- |
| Services     | **Same filename and class name** (`candidate.service.ts` → `CandidateService`)                                   |
| Components   | `candidate-list-page.component.ts` → `candidate-list-page.tsx`, exporting `CandidateListPage`                    |
| Models       | Unchanged (`*.models.ts`)                                                                                        |
| Import alias | **None.** The ~200 relative imports are already correct; a `@/` alias adds nothing to a behaviour-identical port |

### State: signal shim + `useSyncExternalStore`

All of `src/app` uses only `signal(init)`, `sig()`, `sig.set()` and `sig.update()` — not a single `computed()` or `effect()`. A ~20-line shim in `src/app/core/state/signal.ts` (same API plus `subscribe`) reduces the migration of all 16 services to **deleting the `@Injectable` decorator and changing one import line**:

```diff
-import { Injectable, signal } from '@angular/core';
+import { signal } from '../../../core/state/signal';
@@
-@Injectable({ providedIn: 'root' })
 export class CandidateService {
   readonly candidates = signal<Candidate[]>(this.restore());
```

The whole class body (`list`, `create`, `update`, `deactivateMany`, `persist`, `restore`, the demo seed) stays identical. Zustand or Context+reducer would force rewriting the 11 service specs and, in Zustand's case, justifying an extra dependency under principle 2.

The instance graph is explicit and acyclic in `src/app/core/di/services.ts`. `services-context.tsx` exposes a context **whose default value is the real singleton graph**: production renders no provider, and tests wrap with `<ServicesProvider>` to inject doubles.

**Critical rule:** `getSnapshot` must be the raw signal (`candidateService.candidates`), never `candidateService.list()` — `list()` allocates a fresh array per call and React would loop ("The result of getSnapshot should be cached"). All derivation goes in `useMemo`.

### Guards and routes

`authGuard` and `permissionGuard` become **layout routes**, not loaders: auth state is synchronous (localStorage), and `router.createUrlTree([...])` maps 1:1 onto `<Navigate replace>`.

```tsx
export function RequireAuth() {
  const profile = useSignal(useServices().authService.profile);
  const { pathname } = useLocation();
  if (!profile) return <Navigate to="/login" replace />;
  if (profile.mfaRequired && pathname !== '/mfa') return <Navigate to="/mfa" replace />;
  return <Outlet />;
}
```

`withComponentInputBinding()` + `@Input('id')` → `const { id = '' } = useParams()`. The `''` default reproduces the behaviour on `/app/candidates/new` exactly, which the "Alta de candidato" vs "Editar candidato" heading depends on.

`AuthService` currently depends on `Router`, and `tests/unit/auth.service.spec.ts` asserts `navigateByUrl('/login')`. Replacing it with a local `AppNavigator { navigateByUrl(url) }` interface backed by the data router keeps that spec **untouched**.

## Affected files

### New

| File                                                                              | Purpose                                                                                |
| --------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `vite.config.ts`                                                                  | Build, dev server on 4200, and the Vitest `test` block                                 |
| `index.html` (root)                                                               | Vite entry point; keeps `lang="es"` and `<script src="/env.js">` **before** the module |
| `src/main.tsx`                                                                    | Bootstrap + `appNavigator.attach(router)`                                              |
| `src/app/app.tsx`                                                                 | Route table (replaces `app.component.ts` + `app.config.ts` + `app.routes.ts`)          |
| `src/app/core/state/signal.ts`, `use-signal.ts`                                   | Signal shim and subscription hook                                                      |
| `src/app/core/di/services.ts`, `services-context.tsx`                             | Composition root and injection context                                                 |
| `src/app/core/routing/require-auth.tsx`, `require-permission.tsx`, `navigator.ts` | Guards and navigator                                                                   |
| `tests/setup.ts`                                                                  | Replaces `setup-jest.ts`                                                               |

### Modified

- **The 16 services** in `core/` and `features/*/services/`: decorator + import line.
- **The 23 components** in `features/{admin,candidates,catalogs,dashboard,documents,search}`, `core/{auth,layout}` and `shared/components`.
- `src/styles.css`: remove the Google Fonts `@import` and the three `@tailwind` lines. **The Kepler brand tokens stay intact.**
- `package.json`, `tsconfig.json`, `eslint.config.js`, `.prettierignore`, `lint-staged`.
- `scripts/release-gate.js` line 6: `'npm test -- --runInBand'` → `'npm test'` (Vitest rejects the flag and the gate would fail hard).
- `Dockerfile.frontend`, **exactly one line**: `COPY --from=build /app/dist/rrhh-bbdd/browser/ ./` → `COPY --from=build /app/dist/ ./`.

### Deleted

`angular.json` · `setup-jest.ts` · `jest.config.js` · `tailwind.config.js` · `postcss.config.js` · `tsconfig.app.json` · `tsconfig.spec.json` · `src/index.html` · `src/main.ts` · `src/app/app.{component,config,routes}.ts` · `src/app/core/guards/auth.guard.ts` · `src/assets/` (the i18n `es.json`/`en.json` have **zero** references anywhere in the repo).

From `package.json`: all `@angular/*`, `@ngx-translate/*`, `rxjs`, `zone.js`, `tslib`, `jest*`, `tailwindcss`, `@tailwindcss/postcss`, `postcss`, `autoprefixer`, and **the entire `overrides` block** (its 3 entries target Angular build-chain transitives that cease to exist).

**Unchanged:** `nginx.conf`, `docker-compose.frontend.yml`, `docker-entrypoint.d/40-envsubst.sh`, `public/env.js`, `public/env.template.js`, `playwright.config.ts`, `scripts/check-rls.js`, `scripts/check-storage-policies.js`, `.github/**`.

## Milestone delivery

Each milestone leaves the app runnable and ends with a concrete test command.

| #         | Scope                                                                                                                         | Exit criterion                                                                                 |
| --------- | ----------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **M0**    | Configs, `index.html`, `main.tsx`, signal shim, composition root, mechanical port of the 16 services, `release-gate.js` patch | `tsc --noEmit` clean; 11 pure specs + 5 integration specs green; `npm run build` emits `dist/` |
| **M1**    | Routes, `RequireAuth`, `RequirePermission`, `LoginPage`, `MfaPage`, `AppLayout`, `ConfirmDialog`                              | `secure-access.spec.ts` green; `global-setup.ts` writes all 4 `.auth/*.json`                   |
| **M2** ⚠️ | `DashboardPage`, `CandidateListPage` (538 lines); getters → `useMemo`; extract `candidate-list.logic.ts`                      | `candidate-list-operations.spec.ts` green                                                      |
| **M3**    | `CandidateForm`, `CandidateEditPage`, `CandidateDetailPage`                                                                   | `candidate-crud.spec.ts` green                                                                 |
| **M4**    | The 5 relation panels + `CandidateDocuments`                                                                                  | `candidate-profile.spec.ts`, `candidate-documents.spec.ts` green                               |
| **M5** ⚠️ | `SearchFilters` (473 lines), `SearchResults`, `AdvancedSearchPage` (334)                                                      | `advanced-search*.spec.ts`, `export-results.spec.ts` green                                     |
| **M6**    | `CatalogManagementPage`, `AdminUsersPage`, `AdminRolesPage`, `ImportPage`                                                     | `catalogs-crud.spec.ts`, `import-*.spec.ts`, `security-ops.spec.ts` green                      |
| **M7**    | Final cleanup, self-hosted fonts, `for`/`id`, Dockerfile, docs, **Docker image test**                                         | Full verification sequence                                                                     |

### The two hazards ⚠️

**M2 — in-place mutation.** `candidate-list-page` does `this.selectedIds.add(id)` on a `Set`; Angular re-rendered anyway, React will not. It must become `setSelectedIds(prev => new Set(prev).add(id))`. Grep `.push(`, `.splice(`, `.sort(`, `.add(`, `.delete(` across `src/app` and convert each case. Typing the state as `ReadonlySet`/`readonly` turns regressions into compile errors.

**M5 — `search-filters.component.ts` mutates its `@Input` in place** (`this.filters.statusValues = ...`, `this.filters[\`${kind}Criteria\`] = ...`), relying on sharing the object reference with `advanced-search-page`. It becomes a controlled component (`filters`+`onFiltersChange`).

> **Behavioural trap:** today, editing a filter mutates the parent's object but does **not** re-run the search until "Buscar" is pressed. When `filters` is lifted into the parent's `useState`, `results` must be a **separate** `useState`, updated only in `run()`/`clear()`/`loadPreset()`. Deriving it with `useMemo` would make the results table update on every keystroke — a user-visible change and a functional regression.

## Acceptance criteria

**AC-1 — Functional parity.** Given the React build served on `:4200`, when `npm run e2e` runs, then **all 13 Playwright specs pass** with a single documented change: the 4 locators in `tests/e2e/candidate-profile.spec.ts` (lines 23/34/45/60).

**AC-2 — Business logic intact.** Given `npm test`, then the **15 unit specs and 5 integration specs pass**, and at least 11 of the unit specs do so with no change beyond `jest.` → `vi.`.

**AC-3 — Data continuity.** Given a session with `rrhh-candidates`, `rrhh-catalogs` and `rrhh.search.presets.v1` populated by the Angular build, when the React build is loaded on the same origin, then the same candidates, catalogs and presets render, including the legacy preset-shape migration.

**AC-4 — Non-reactive search filters.** Given the advanced search page with results on screen, when text is typed into the "Texto" field without pressing "Buscar", then the results table **does not change**.

**AC-5 — Bulk selection.** Given the candidate list, when 2 rows are checked and "Baja lógica" is pressed and confirmed, then the toast `Baja lógica aplicada a 2 candidato(s).` appears and both rows become inactive.

**AC-6 — Route ordering.** Given `/app/candidates/new`, then the create form renders with the heading "Alta de candidato", **not** the detail page (Angular is first-match-wins; React Router 7 ranks).

**AC-7 — Delivery.** Given `docker compose -f docker-compose.frontend.yml up --build`, then the container serves the app on `:63151`, `/env.js` contains `__APP_CONFIG__` with substituted values, and a deep link such as `/app/candidates` resolves through nginx's `try_files`.

**AC-8 — Bundle budget.** The initial bundle stays under the 700 kB warning threshold `angular.json` used to set.

**AC-9 — Copy.** No UI string changes. Accents and spelling are preserved (`Certificación`, `Añadir idioma`, `Baja lógica`, `España`).

## Test coverage

### Unit and integration — Jest → **Vitest 3**

`tests/setup.ts` drops `setupZoneTestEnv()` but **keeps both polyfills**: `structuredClone` (used by `candidate-form` and `cloneSearchFilters`) and `URL.createObjectURL`/`revokeObjectURL` (used by `document.service.createSecureUrl()` and `import-page.downloadErrorsCsv()`). The 5 specs under `tests/integration/` import only `node:fs`/`node:path`, so they need a **node** environment, not jsdom (`environmentMatchGlobs`).

| Specs                                                                                                                                                                                                                                  | Treatment                                                                                                                                 |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `candidate.service`, `catalog.service`, `import.service`, `candidate-search.service`, `candidate-relations.service`, `document.service`, `export.service`, `profile.service`, `role.service`, `search-presets.service`, `auth.service` | **11 specs, no logical change** — `jest.` → `vi.` only                                                                                    |
| `auth-guards.spec.ts`                                                                                                                                                                                                                  | Rewrite with RTL: `<MemoryRouter>` + sentinel elements for `/login`, `/mfa`, `/app`. All 6 cases map 1:1                                  |
| `candidate-form.spec.ts`                                                                                                                                                                                                               | The 2 draft cases move to the pure `toDraft()` helper; the 2 submit cases become RTL                                                      |
| `candidate-list-page.spec.ts`                                                                                                                                                                                                          | Filtering and chip cases move to `candidate-list.logic.spec.ts`; bulk cases become RTL with `confirmDialogService`/`toastService` doubles |
| `candidate-profile-sections.spec.ts`                                                                                                                                                                                                   | RTL per section, with `candidateRelationsService` doubled via `ServicesProvider`                                                          |

### E2E — frozen contract

`playwright.config.ts` **does not change**. For the 13 specs to keep passing, the React app must preserve:

| Category           | Must stay identical                                                                                                                                                                                                                                                                                                 |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Server             | Port **4200** on `127.0.0.1`, with SPA fallback for deep links                                                                                                                                                                                                                                                      |
| Login              | Ids `#email`, `#password`, `#role`; option values `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly`; `button[type="submit"]`; post-login navigation matching `**/app**`                                                                                                                                       |
| `data-testid`      | All 8: `confirm-dialog`, `confirm-accept`, `confirm-overlay`, `open-export-history`, `export-history-modal`, `close-export-history`, `toggle-filters`, `filters-summary`                                                                                                                                            |
| `name=` attributes | All ~20: `firstName`, `lastName`, `text`, `presetName`, `newCode`, `newNameEs`, `editNameEs`, `company`, `position`, `startDate`, `endDate`, `family`, `hasCv`, `status`, `selectedPreset`, `educationType`, `language`, `level`, `sector`, `skill`                                                                 |
| DOM hooks          | `a.skip-link`, `#main-content` with `tabIndex={-1}`, `span.badge`, `tbody tr`, `p:has-text("Activo:")` rendering `Si`/`No`                                                                                                                                                                                          |
| Copy               | `Nombre y apellidos son obligatorios.`, `Baja lógica aplicada a 2 candidato(s).`, `CV subido correctamente.`, `Filas cargadas: 1`, `Modo: Dry run`, `Sin resultados.`, and the buttons `Abrir seguro`, `Activar`, `Desactivar`, `Bajar`, `Guardar`, `Exportar CSV`, `Añadir idioma/habilidad/formación/experiencia` |

> ⚠️ **The `name=` attributes are the silent risk.** In Angular they exist only because `ngModel` requires them; in React they are decorative and very easy to lose while porting "cleanly". They would break 20 locators at the end of the project. They belong on an explicit checklist item in every milestone.

**The one permitted e2e edit:** `tests/e2e/candidate-profile.spec.ts` lines 23/34/45/60 use `page.locator('rrhh-candidate-languages' | '-skills' | '-experience' | '-education')` — Angular custom-element tags that do not exist in React. They become `getByTestId('candidate-languages')` etc., with those 5 `data-testid` attributes added. Rendering `<rrhh-*>` tags in React purely to satisfy the selector would work, but would embed Angular naming permanently.

### Security

`tests/security/*.sql`, `npm run security:rls` and `npm run security:storage` **do not change and must still pass**: this ticket touches no permissions, RLS or Storage. They run anyway as part of `npm run release:gate`, as evidence of no regression.

## Documentation to update

| File                                 | Change                                                                                                                                                                                                                         |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `README.md`                          | Stack section: Angular 21 / RxJS / Angular CDK / Tailwind / `@ngx-translate` / Jest → React 19 / Vite / React Router 7 / own CSS / Vitest. Also fix the stale "Estado" section that still says "Todavia no hay implementacion" |
| `openspec/config.yaml`               | `context:` block — stack line, and "Jest" → "Vitest". **Do not touch the numbered principles**                                                                                                                                 |
| `specs/001-gestion-cvs-rrhh/plan.md` | 6 Angular mentions (lines 13, 25, 33, 63, 140, 168). Line 63's "Stack boundary: PASS … Angular 21" → React 19 + Vite, with a dated note pointing at the change's `design.md`                                                   |
| `AGENTS.md`                          | New paragraph: React function components; state lives in singleton classes using the `core/state/signal.ts` shim, subscribed via `useSignal`; do not reintroduce Angular idioms, RxJS or a state library                       |
| `.claude/commands/enrich-us.md`      | Its step 5 currently assumes "standalone components, signals" and "Jest" — update to the React equivalent. The `src/app/**` paths remain valid                                                                                 |
| `SUPABASE_INTEGRATION_GUIDE.md`      | Note marking its Angular snippets as historical reference (optional, low priority)                                                                                                                                             |

`.github/**` needs no change: verified, the issue and PR templates do not mention the stack.

## Non-functional requirements

- **Security.** No change to the exposure surface: same `localStorage` keys, same auth model, same client-side guards. Review finding `C-4` (personal data in clear in the browser) is **not resolved here** and must stay on the books as an open ticket.
- **Performance.** Derivations that today are getters re-evaluated on every change-detection cycle become `useMemo`, reducing per-render work. `react-hooks/exhaustive-deps` is configured as an **error**, not a warning: it is the defence against a `useMemo` with an incomplete dependency list, which would produce a stale table with no visible error. Bundle budget: 700 kB (AC-8).
- **Accessibility.** Fixes review finding `M-5`: `htmlFor`/`id` on every label (`candidate-form.tsx` has 11 bare `<label>` elements). Preserves `a.skip-link`, focusable `#main-content`, `aria-modal`, and the confirm dialog's `Escape` close. `tests/e2e/ux-accessibility.spec.ts` must stay green.
- **Responsive.** No regression. Losing Angular's style encapsulation means the generic selectors must be prefixed (`header`, `nav a`, `main` → `.shell header`, …) and the duplicated `.overlay`/`.modal` rules consolidated into `shared/components/modal.css`. Verified by visual diff (below).
- **Language.** All UI copy in Spanish, with correct accents and spelling. None of it changes (AC-9).
- **Fonts.** Montserrat and Nunito Sans get self-hosted (`M-3`): today an `@import` to `fonts.googleapis.com` blocks render, fails on an isolated network, and sends every employee's IP to a third party.

## Verification

Per milestone, the command in the table. Final gate, in order:

```bash
# 0. A live Angular dev server on 4200 would produce false greens in e2e
npx kill-port 4200
rm -rf node_modules dist .angular && npm ci

npm run lint && npm run format:check && npx tsc --noEmit
npm test && npm run test:integration          # 15 + 5 specs

npm run build
ls dist/index.html dist/env.js dist/assets    # and dist/rrhh-bbdd/ must NOT exist

npm start &                                    # must bind port 4200
curl -sf http://127.0.0.1:4200/env.js
curl -sf http://127.0.0.1:4200/app/candidates/new | grep -q 'id="root"'

npm run e2e                                    # THE BAR: 13/13

docker compose -f docker-compose.frontend.yml up -d --build
curl -sf http://localhost:63151/ | grep -q 'id="root"'
curl -sf http://localhost:63151/env.js | grep -q '__APP_CONFIG__'
docker compose -f docker-compose.frontend.yml down

npm run security:rls && npm run security:storage && npm run release:gate
```

Two manual checks automation does not cover: **data continuity** (AC-3) and a **visual diff** side by side across dashboard, list, detail, advanced search (collapsed and expanded), catalogs and import.

## Risks

| Risk                                                                                                                     | Mitigation                                                                                   |
| ------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| In-place mutation stops re-rendering in React                                                                            | Grep for mutators in M2/M5; `readonly`/`ReadonlySet` types                                   |
| `useMemo` with incomplete deps → stale table with no error                                                               | `react-hooks/exhaustive-deps` as an **error**                                                |
| Losing `name=` attributes → 20 broken locators at the end                                                                | Explicit per-milestone checklist; run the relevant e2e spec when closing **every** milestone |
| Playwright's `reuseExistingServer` attaches to a stale Angular server on 4200 → false greens                             | `--strictPort` on Vite + the kill-port step 0                                                |
| The `dist/` path change breaks the container and nothing in the suite covers it                                          | Test the Docker image in **M7**, not at the very end                                         |
| `input type="number"`: `ngModel` coerced to number, React yields `string`; `yearsExperience` feeds search and CSV export | `Number(e.target.value)` at every numeric binding (M4, M6)                                   |
| `scripts/release-gate.js`'s `--runInBand` fails under Vitest and blocks the final gate                                   | Patch it in **M0** and run `npm run release:gate` once to confirm                            |
| Style leakage after losing encapsulation                                                                                 | Prefix generic selectors; visual diff                                                        |

## Departure record (for `design.md`)

`openspec/config.yaml`, principle 2: _"Supabase and the existing corporate stack are the boundary… New infrastructure choices need an explicit, documented reason"_. Angular is the ratified stack, so this change requires a departure record:

- **Reason.** The frontend framework is replaced (Angular 21 → React 19 + Vite + React Router 7) by product decision, to align with the team's skill set and the Kepler ecosystem. **The backend boundary is unchanged**: Supabase remains the sole backend; `@supabase/supabase-js` and `core/supabase/` are ported verbatim; no new backend, API layer or persistence mechanism is introduced, and the `localStorage` keys are identical byte for byte.
- **Simpler alternative considered.** Stay on Angular and apply only the quality cleanup (dead dependencies, self-hosted fonts, `for`/`id`, splitting large templates) — i.e. resolve `M-3`, `M-4` and `M-5` without changing framework. Rejected by product.
- **Mitigations.** (1) Port with no change to behaviour, copy, routes, permissions or validation messages. (2) The 13 Playwright specs are the acceptance gate, with a single documented four-locator change. (3) Business logic stays in the existing framework-agnostic service classes — 11 of 15 unit specs pass with a mechanical rename, demonstrating it was not modified. (4) No state-management or form library is introduced: only React, React Router and Vite. (5) Delivery is identical: same Dockerfile, same nginx, same `env.js` mechanism, same port 63151.
