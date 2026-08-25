# Design — Angular 21 → React 19 migration

See [proposal.md](./proposal.md) for motivation and scope.

## Context

Facts about the current codebase that shape every decision below:

- **No Angular framework surface to unwind.** Zero `NgModule`, zero pipes, zero directives, zero HTTP interceptors, zero resolvers, zero lazy routes. `HttpClient` is registered in `app.config.ts` but never injected. `rxjs` has zero imports in `src/`.
- **Services are framework-agnostic already.** All 16 are `@Injectable({providedIn:'root'})` classes whose only Angular dependency is the `signal` import. Across all of `src/app`, only four signal APIs are used: `signal(init)`, call-as-getter `sig()`, `sig.set()`, `sig.update()`. No `computed`, no `effect`, no `asReadonly`.
- **All 23 components are standalone with inline `template:` and inline `styles: []`.** There are no `.html` or per-component `.css` files in `src/`.
- **Forms are 100% template-driven `[(ngModel)]`.** `ReactiveFormsModule`, `FormBuilder` and `Validators` appear nowhere. Real validation lives in the service layer as thrown `Error`s with exact Spanish messages, asserted verbatim by both unit and e2e specs.
- **All 15 unit specs live under `tests/`, none colocated in `src/`,** and import via `../../src/app/...`. Eleven construct services directly (`new CandidateService()`) and assert on their public surface.
- **13 Playwright specs are the only end-to-end oracle,** and no CI exists to run them — verification is local.

Two structural hazards, identified during exploration:

1. `search-filters.component.ts` takes `@Input({required:true}) filters` and **mutates it in place**, relying on the parent sharing the object reference.
2. Pages recompute derived data through **getters** (`get filteredCandidates()`, the six dashboard KPI getters) that Angular re-evaluated on every Zone tick, and `candidate-list-page` mutates a `Set` in place for row selection.

## Goals / Non-Goals

**Goals:**

- Preserve the public surface of the service classes so the majority of the unit suite proves the business logic was not modified.
- Keep the Playwright suite runnable as the acceptance gate, changing as little of it as possible.
- Keep the added dependency surface minimal — principle 2 makes every new runtime dependency a separate thing to justify.
- Sequence the work so the app is runnable and testable at every milestone.

**Non-Goals:**

- Improving the state architecture. A better React design might lift `localStorage` access behind a repository interface in anticipation of Supabase (`C-1`); doing so here would decouple the unit specs from the thing they currently verify and blur what the migration changed.
- Introducing `computed`-style derivation primitives. Angular's own `computed()` is unused today; adding an equivalent would be new behaviour, not a port.
- Type-safety improvements to forms. Tempting (`M-4` calls out untyped template-driven forms) but it changes validation behaviour, which is precisely what must stay fixed.

## Decisions

### D1 — Replace `src/` in place rather than building a parallel app

**Chosen:** port into the existing tree on branch `feat/KTL-3`.

The 15 unit specs import through `../../src/app/...`; `openspec/config.yaml`, `specs/001-gestion-cvs-rrhh/plan.md` and `.claude/commands/enrich-us.md` all reference `src/app/features/...`. Preserving the directory structure keeps those specs and documents correct, and lets git's rename detection produce a readable per-file history.

**Alternative considered:** build `src-react/` alongside and swap at the end. Rejected — it means two working apps, two build configs, and a single enormous swap commit, which defeats the milestone sequencing in `tasks.md` and makes review impractical.

**Naming:** services keep filenames _and_ class names verbatim (this is what keeps 11 specs importing an unchanged path). Components become `.tsx` and drop the `Component` suffix: `candidate-list-page.component.ts` → `candidate-list-page.tsx` exporting `CandidateListPage`. Models are untouched.

**No `@/` path alias.** Every import in `src/app` is already relative and correct; an alias would touch ~200 import lines plus Vite, tsconfig and eslint resolver config for zero behavioural gain.

### D2 — State: a signal shim consumed through `useSyncExternalStore`

**Chosen:** a ~20-line `src/app/core/state/signal.ts` reproducing the four signal APIs actually in use, plus `subscribe`, and a `useSignal(sig)` hook wrapping `useSyncExternalStore`.

The entire diff to each of the 16 services becomes: delete the `@Injectable` line, change one import line. Class bodies — including all the business logic and the `localStorage` read/write paths — are untouched, which is the strongest available evidence that behaviour did not change.

**Alternatives considered:**

| Option                                          | Why rejected                                                                                                                                                                                      |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Zustand                                         | Rewrites all 11 service specs, and adds a runtime dependency needing its own principle-2 justification                                                                                            |
| React Context + reducers                        | Requires reshaping `candidate-relations.service.ts`'s thrown-`Error` validation and `catalog.service.ts`'s 301 lines into reducer form — the largest possible diff for a behaviour-identical port |
| Keep classes, subscribe via polling/`useEffect` | Tearing, and no synchronous consistency guarantee                                                                                                                                                 |

**Constraint this imposes:** the `getSnapshot` handed to `useSyncExternalStore` must be the raw signal (`candidateService.candidates`), never a derived call like `candidateService.list()` — `list()` allocates a fresh array per call and React would loop with "The result of getSnapshot should be cached". The `useSignal(sig)` helper takes a signal rather than a selector specifically so the mistake is hard to express. All derivation happens in `useMemo`.

**Dependency injection:** an explicit, acyclic composition root in `core/di/services.ts` (`CandidateService` is the root of the feature graph; `ProfileService` takes `RoleService`; `AuthService`/`MfaService` take `SupabaseClientService`). `services-context.tsx` exposes a context **whose default value is the real singleton graph**, so production renders no provider and tests wrap with `<ServicesProvider>` to inject doubles — the seam that replaces Angular's `TestBed` providers.

### D3 — Guards become layout routes, not loaders

**Chosen:** `<RequireAuth>` and `<RequirePermission permission="...">` as elements on layout routes, rendering `<Navigate replace>` or `<Outlet />`.

Auth state is synchronous (`localStorage`-backed), so a loader has no async work to do. Loaders also run outside React, so they could not use `useSignal` and would have to reach into the singleton directly, losing the test seam from D2. `router.createUrlTree([...])` maps 1:1 onto `<Navigate replace>`, whereas `throw redirect(...)` changes the observable navigation semantics.

`createBrowserRouter` (data router) is required regardless, because `AuthService.signOut()` navigates from outside React.

**Fidelity detail:** today's `authGuard` checks `router.url !== '/mfa'`. `RequireAuth` wraps both `/mfa` and `/app`, so the equivalent is `useLocation().pathname !== '/mfa'` — same semantics, no redirect loop.

### D4 — Keep `AuthService`'s navigation dependency as a local interface

`AuthService` injects Angular's `Router` and `tests/unit/auth.service.spec.ts` asserts `navigateByUrl('/login')`. Introducing a local `AppNavigator { navigateByUrl(url: string): void }` interface, implemented by a thin adapter over the data router and attached in `main.tsx`, keeps that spec passing **unmodified** — the stub `{ navigateByUrl: vi.fn() }` still satisfies it.

### D5 — Forms stay plain controlled components

**Chosen:** `useState` per form, keeping the manual `submit()` guard and the local `error` string.

There is no validation _schema_ to express. Validation is (a) HTML `required`/`type` attributes, (b) three or four manual `if (!x.trim())` checks, and (c) — for everything substantive — thrown `Error`s from the service layer. Introducing zod means either duplicating those Spanish messages (drift risk) or leaving zod with nothing to do. react-hook-form's value proposition is uncontrolled inputs on large forms; the largest form here has 12 fields.

Two new runtime dependencies would also be two more principle-2 departures on a change whose brief is a behaviour-identical port.

**Derived state:** `@Input set candidate(...)` becomes a pure `toDraft(candidate?)` helper plus `useState(() => toDraft(candidate))`, with `key={candidateId || 'new'}` on the form so navigating between `/new` and `/:id/edit` resets state exactly as the Angular setter did. Explicitly **not** a `useEffect` that syncs derived state.

**Fidelity detail:** Angular's template-driven form does not block submit on `required` — `candidate-crud.spec.ts` expects the custom Spanish message, not the browser's validation bubble. The `<form onSubmit>` must `preventDefault()` and run the manual check; add `noValidate` if native validation starts intercepting.

### D6 — Vitest over Jest

Vitest reads `vite.config.ts` directly, so TSX, CSS imports and `import.meta` work with no extra transform config. Staying on Jest would mean adding ts-jest or babel-jest plus a `moduleNameMapper` for CSS and ESM interop for `@supabase/supabase-js` — more configuration to maintain, for a tool the project would then be running against a Vite build it does not understand.

`tests/setup.ts` drops `setupZoneTestEnv()` but **keeps both polyfills**: `structuredClone` (used by `candidate-form` and `cloneSearchFilters`) and `URL.createObjectURL`/`revokeObjectURL` (used by `document.service.createSecureUrl()` and `import-page.downloadErrorsCsv()`). The five integration specs import only `node:fs`/`node:path` and need a **node** environment, not jsdom.

`scripts/release-gate.js:6` currently runs `npm test -- --runInBand`, which Vitest rejects. It must be patched in the first milestone, not the last, so the gate is known-good before it is needed.

### D7 — Global co-located CSS, not CSS Modules

Each inline `styles: []` block moves to a co-located plain `.css` file imported by its module. **Not** CSS Modules: `ux-accessibility.spec.ts` binds `a.skip-link` and several specs bind `span.badge`, so hashed class names would break the suite.

Losing Angular's emulated encapsulation means previously-scoped generic selectors leak. `header`, `nav a` and `main` in the layout must be prefixed (`.shell header`). `.overlay`/`.modal`/`.dialog` appear in both `advanced-search-page` and `confirm-dialog` with functionally identical rules — consolidate into one `shared/components/modal.css` rather than duplicating.

Tailwind is removed rather than adopted: it is configured but templates use the hand-written class system (`class="grid two"` resolves to `.grid.two`, not Tailwind utilities), so only the preflight reset is in play. `src/styles.css` and its Kepler brand tokens transfer 1:1.

### D8 — The e2e suite is the acceptance gate, with exactly one permitted edit

`playwright.config.ts` does not change. The React app must preserve port 4200, the login ids `#email`/`#password`/`#role` with the same option values, the 8 `data-testid` hooks, the ~20 `name=` attributes, the DOM hooks (`a.skip-link`, `#main-content[tabindex=-1]`, `span.badge`), and every asserted Spanish string.

The `name=` attributes deserve specific attention: in Angular they exist only because `ngModel` requires them. In React they are decorative, and a developer porting "cleanly" would drop them — silently breaking 20 locators late in the project. `tasks.md` carries this as a per-milestone checklist item.

**The one permitted edit:** `tests/e2e/candidate-profile.spec.ts` lines 23/34/45/60 locate `rrhh-candidate-languages`, `-skills`, `-experience`, `-education` — Angular custom-element tags that do not exist in React. They become `getByTestId('candidate-languages')` etc., with five matching `data-testid` attributes added. Rendering literal `<rrhh-*>` elements in React would work (React 19 passes unknown tags through) but would embed Angular naming in a React codebase permanently.

### D9 — Ship without `<StrictMode>`

React 19's StrictMode double-invokes effects in development, which can double-fire `toastService.show()` and double-register the `ConfirmDialog` escape listener. Porting without it guarantees behavioural identity; enabling it becomes a follow-up ticket once the e2e suite is green, so whatever it surfaces is diagnosed against a known-good baseline rather than during the migration.

## Stack impact

| Layer      | Before                                                   | After                                                          |
| ---------- | -------------------------------------------------------- | -------------------------------------------------------------- |
| Framework  | Angular 21.2.17                                          | React 19                                                       |
| Build      | `@angular/build:application` → `dist/rrhh-bbdd/browser/` | Vite → `dist/`                                                 |
| Router     | `@angular/router` + `CanActivateFn`                      | React Router 7 `createBrowserRouter` + layout routes           |
| State      | `@Injectable` + `signal()`                               | Singleton classes + local signal shim + `useSyncExternalStore` |
| Forms      | Template-driven `ngModel`                                | Controlled `useState`                                          |
| Styles     | `styles.css` + Tailwind (unused) + inline `styles: []`   | `styles.css` + co-located global CSS                           |
| Unit tests | Jest 30 + `jest-preset-angular`                          | Vitest 3 + React Testing Library                               |
| E2E        | Playwright 1.58                                          | **unchanged**                                                  |
| Delivery   | Docker + nginx-unprivileged + `env.js`                   | **unchanged** except one `COPY` line                           |

**Data model:** no change. Same entities, same TypeScript models, same 9 `localStorage` keys, same serialized shapes.

**Authorization model:** no change. `DEFAULT_ROLES`, the 12 `Permission` values and the permission-to-route mapping are ported verbatim. Client-side guards remain UX affordances; per `SECURITY.md`, RLS is the real boundary — and this change touches neither RLS nor the Supabase client beyond porting it verbatim.

**Storage model:** no change. No SQL migration is added or modified, so the idempotency rule for migrations does not apply to this change. The private `candidate-cvs` bucket and its policies are untouched.

**Backend access centralization:** preserved. `src/app/core/supabase/supabase-client.service.ts` remains the single place a Supabase client is constructed, and `auth.service.ts` / `mfa.service.ts` remain its only consumers. No component gains direct backend access.

## Test strategy

| Layer                                | Approach                                                                                                                                       |
| ------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| Service logic (11 specs)             | Port unchanged; `jest.` → `vi.` only. Their passing **is** the evidence that business logic was not modified                                   |
| `auth-guards.spec.ts`                | Rewrite with RTL: `<MemoryRouter>` around a small route tree with sentinel elements for `/login`, `/mfa`, `/app`. All 6 cases map 1:1          |
| `candidate-form.spec.ts`             | Draft cases move to the pure `toDraft()` helper; submit cases become RTL                                                                       |
| `candidate-list-page.spec.ts`        | Filtering/chips cases move to an extracted `candidate-list.logic.ts`; bulk cases become RTL with stubbed `confirmDialogService`/`toastService` |
| `candidate-profile-sections.spec.ts` | RTL per section, with `candidateRelationsService` doubled via `ServicesProvider`                                                               |
| Integration (5 specs)                | Unchanged, node environment                                                                                                                    |
| E2E (13 specs)                       | Unchanged but for D8's four locators. Run the relevant spec at the end of **every** milestone, not only at the end                             |
| Security                             | `tests/security/*.sql`, `security:rls`, `security:storage` unchanged; run as evidence of no regression                                         |

## Risks / Trade-offs

| Risk                                                                                                                                                                                                               | Mitigation                                                                                                                                                                                                              |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `search-filters` mutates its `@Input` in place; naively lifting `filters` to parent state and deriving `results` with `useMemo` would make the results table update on every keystroke — a user-visible regression | Controlled component with `onFiltersChange`; parent keeps `results` in a **separate** `useState` updated only in `run()`/`clear()`/`loadPreset()`. Assert explicitly that typing does not change results until "Buscar" |
| Angular re-rendered on mutation, React does not — `selectedIds.add(id)` and any array mutation become silent no-render bugs                                                                                        | Grep `.push(`, `.splice(`, `.sort(`, `.add(`, `.delete(` across `src/app`; convert to immutable updates; type state as `readonly`/`ReadonlySet` so regressions become compile errors                                    |
| Getters ran every tick; a `useMemo` with an incomplete dependency list yields a stale table with no error                                                                                                          | `react-hooks/exhaustive-deps` configured as **error**, not warning. For cheap derivations (dashboard KPIs over a handful of candidates) prefer plain in-render computation — correctness over memoization               |
| `name=` attributes are decorative in React and easy to drop, breaking 20 e2e locators late                                                                                                                         | Explicit per-milestone checklist item in `tasks.md`; run the milestone's e2e spec before closing it                                                                                                                     |
| Playwright's `reuseExistingServer` attaches to a leftover Angular dev server on 4200 → false greens                                                                                                                | `--strictPort` on Vite, plus an explicit port-kill as step 0 of the verification sequence                                                                                                                               |
| The `dist/` path change breaks the container, and nothing in the unit or e2e suite covers it                                                                                                                       | Change `Dockerfile.frontend` and run the `docker compose up --build` + `curl` checks in the **same** milestone task                                                                                                     |
| `input type="number"`: `ngModel` coerced to number, React yields a string; `yearsExperience` feeds search comparisons and CSV export                                                                               | `Number(e.target.value)` at every numeric binding; grep `type="number"` during the relation-panel and admin milestones                                                                                                  |
| React Router 7 ranks routes rather than first-match-wins; `/app/candidates/new` could resolve to the detail page                                                                                                   | Explicit assertion during the candidate-form milestone; `candidate-crud.spec.ts` already covers it                                                                                                                      |
| Removing style encapsulation leaks `header`, `nav a`, `main`, `.overlay`, `.modal`                                                                                                                                 | Prefix with the component's root class; consolidate duplicated modal rules; manual visual diff                                                                                                                          |
| `release-gate.js`'s `--runInBand` fails under Vitest, blocking the gate at the last minute                                                                                                                         | Patch it in the first milestone and run the gate once to confirm                                                                                                                                                        |
| Self-hosting Montserrat/Nunito Sans risks visual drift and an OFL licence omission                                                                                                                                 | Use `@fontsource/*` packages (self-hosted, licence bundled) rather than hand-vendoring WOFF2s; verify weights against before/after screenshots                                                                          |

## Departure from principle 2

> "Supabase and the existing corporate stack are the boundary… New infrastructure choices need an explicit, documented reason."

Angular 21 is the ratified stack (`specs/001-gestion-cvs-rrhh/plan.md` records "Stack boundary: PASS"). This change departs from it.

**Reason.** The frontend framework is replaced (Angular 21 → React 19 + Vite + React Router 7) at product's direction, to align with the team's skill set and the wider Kepler ecosystem. The **backend boundary is unchanged**: Supabase remains the sole backend for auth, data, storage and functions; `@supabase/supabase-js` and the `core/supabase/` wrapper are ported verbatim; no new backend, API layer or persistence mechanism is introduced; the `localStorage` keys are identical byte for byte.

**Simpler alternative considered.** Stay on Angular and apply only the quality cleanup — remove the unused dependencies, self-host the fonts, associate the form labels, split the oversized inline templates. That resolves review findings `M-3`, `M-4` and `M-5` at a fraction of the cost and with no stack departure. Rejected by product in favour of the React alignment.

**Mitigations.**

1. Behaviour-identical port — no change to features, copy, storage keys, routes, permissions or validation messages.
2. The 13 existing Playwright specs are the acceptance gate, passing with a single documented four-locator change.
3. All business logic stays in the existing framework-agnostic service classes; 11 of 15 unit specs pass with a mechanical rename, demonstrating the logic is unmodified.
4. No new state-management or form library — React's built-in `useSyncExternalStore` and `useState`. Added runtime dependency surface is React, React Router and Vite.
5. Delivery is unchanged: same Dockerfile (one line), same nginx config, same runtime `env.js` mechanism, same host port 63151.
6. Security posture is unchanged and re-verified: `security:rls`, `security:storage` and `tests/security/*.sql` run untouched.

## Migration plan

Eight milestones, each leaving a runnable app and closing with a named test command. Ordering is dependency-driven: configuration and services first (no UI to break), then shell and auth (unblocks the e2e login fixture used by every other spec), then features in order of coupling. The two hazards are isolated in their own milestones. Full breakdown in [tasks.md](./tasks.md).

**Deployment.** One change: `Dockerfile.frontend`'s `COPY --from=build /app/dist/rrhh-bbdd/browser/ ./` becomes `COPY --from=build /app/dist/ ./`. Everything downstream — nginx SPA fallback, `40-envsubst.sh` writing `env.js` at container start, the `63151:8080` port mapping — is unaffected.

**Rollback.** The work lands on `feat/KTL-3` and is squashed to `main` only after the full gate passes, so rollback is `git revert` of one merge plus a rebuild. The previous container image remains deployable independently — no database migration, no schema change, no storage change and no `localStorage` shape change means there is **no forward-only state**: an Angular build and a React build can be swapped in either direction against the same browser state. Follow `docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md` for the image-level procedure.

## Open questions

None that block implementation. Two deferred items are already tracked as follow-up tickets rather than questions: enabling `<StrictMode>` (D9), and the review's `C-1`..`C-4` backend findings.

## Post-port hardening (added during apply)

Three items surfaced while reviewing the finished port and were fixed in this
change rather than deferred, because "Posiciones" (KTL-4) will be written by
copying these patterns:

- **`useCatalogs()` / `useCandidates()`** (`features/catalogs/use-catalogs.ts`,
  `features/candidates/use-candidates.ts`). D2 left the subscription and the read
  as two separate statements — `useSignal(catalogService.catalogs)` followed by
  `catalogService.activeNames(...)`. The subscription line looks unused; deleting
  it produces a component that renders correctly once and then silently stops
  updating, with no error and no failing test. This hazard did not exist in
  Angular, where getters were re-evaluated on every tick, so it was a
  maintainability regression introduced by the port. The hooks bundle the two
  operations so the mistake cannot be expressed. Ten call sites migrated.
- **`CandidateListPage` split** into the page plus `candidate-filters-bar.tsx`,
  `candidate-table.tsx` and the shared `pagination.tsx`: 410 → 219 lines.
- **`SearchFilters` split** into the panel plus `criteria-group.tsx` and
  `filters-summary.tsx`: 382 → 198 lines.

The two splits are what tasks 4.2 and 7.1 called for; the first pass extracted
only the pure logic and left the components at their original size, which left
review finding `M-4` (oversized components) substantially unaddressed.

A second review pass found the same read-without-subscribe hazard in the auth
service and fixed it the same way:

- **`usePermission()` applied to all six components** that call
  `authService.hasPermission()` during render. Five of them never subscribed to
  `authService.profile`, so they were latently stale — harmless today only
  because the profile changes on sign-in and sign-out and both unmount the view.
  The hook already existed; it was only used by `RequirePermission`.
- **`useErrorToast()`** replaces the `error instanceof Error ? ... : fallback`
  toast helper that had been copy-pasted into six files.
- **`.span-all` / `.field--wide`** replace the four inline `style={{}}` objects,
  which bypassed the Kepler tokens entirely.
- **Pure exports moved out of `.tsx` modules** (`criteria-group.model.ts`,
  `filters-summary.logic.ts`, `candidate-form.logic.ts`, and
  `services-context.tsx` → `.ts`, since it contained no JSX). `npm run lint` is
  now **0 errors and 0 warnings**.

One test double had to grow: `candidate-list-page.spec.tsx` stubbed
`authService` as `{ hasPermission }` only, which no longer satisfies a component
that subscribes to `profile` first. That is the `as unknown as Services` cast in
the test doubles doing exactly what was predicted — it let a too-thin double
through until runtime. Typing that seam properly remains an open improvement.

### Correction (2026-08-20, post-archive)

`proposal.md` and task 9.1 state that `src/assets/i18n/{es,en}.json` were deleted
as unreferenced dead assets. **They were restored at the product owner's request**
— i18n is intended to be wired soon, so the existing key scheme is worth keeping
in the tree rather than only in git history.

The rationale for deleting was factually correct (zero references anywhere in
`src/` or `tests/`, and `angular.json` shipped them into `dist/assets/` on every
build), but it weighed "dead weight today" over "scaffolding for imminent work",
and that trade was the product owner's to make.

The `@ngx-translate/*` packages stay removed regardless: they are Angular-only.
A React implementation would use `i18next` + `react-i18next`.

Two things must be settled before these files are wired — see the handover notes
on KTL-3 or whichever ticket picks up i18n:

1. **18 of the 51 Spanish values are missing their accents** (`Iniciar sesion`,
   `Busqueda avanzada`, `Certificacion`, `Anadir idioma`, `Anos de experiencia`,
   `Formacion`, …). The live templates already carry the correct forms — commit
   `2fdf86f` was an explicit tilde-correction pass that these files missed.
   Wiring them as-is would regress copy the team has already paid to fix, and
   would violate the standing rule in `openspec/config.yaml` that user-facing
   Spanish keeps correct accents.
2. **File placement under Vite.** Angular's `angular.json` copied `src/assets`
   into the build output automatically; Vite does not. Files under `src/` are
   only emitted if a module imports them. If the translations are to be fetched
   at runtime (i18next http backend) they must move to `public/i18n/`; if they
   are to be bundled (`resources` passed directly to `i18next.init`) they can
   stay where they are and be imported. They are currently in `src/assets/i18n/`
   and are **not** emitted by `npm run build`.
