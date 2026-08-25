# Migrate the frontend from Angular 21 to React 19

Ticket: [KTL-3](../../KTL-3.md)

## Why

Product has decided to move the Kepler Talento frontend to React to align with the team's skill set and the wider Kepler ecosystem. The current codebase makes this unusually cheap: there are no `NgModule`s, no RxJS, no HTTP interceptors, no lazy routes and no state library — the 16 services are plain classes holding `signal()` state over `localStorage`, and 13 Playwright specs already exercise the whole product end to end and can serve as the regression net.

Doing it now, before KTL-4 ("Posiciones") adds a seventh feature area, avoids porting that work twice.

## What Changes

- **BREAKING (build/deploy only, not user-facing):** the build output moves from `dist/rrhh-bbdd/browser/` to `dist/`. `Dockerfile.frontend` changes by exactly one line. `nginx.conf`, `docker-compose.frontend.yml`, the `env.js` runtime-config mechanism and host port 63151 are unchanged.
- Port all **23 components** and **16 services** under `src/app/` to React 19 + React Router 7, built by Vite, replacing `src/` in place.
- Replace Angular's dependency injection and `signal()` with a ~20-line signal shim (`core/state/signal.ts`) consumed through `useSyncExternalStore`, plus an explicit composition root. Service classes keep their filenames, class names and bodies.
- Replace `authGuard` / `permissionGuard` with `<RequireAuth>` / `<RequirePermission>` layout routes.
- Replace Jest + `jest-preset-angular` with **Vitest 3**; 11 of the 15 unit specs survive with a mechanical `jest.` → `vi.` rename, 4 component/guard specs are rewritten against React Testing Library.
- Remove dependencies that are installed but never imported: `rxjs`, `zone.js`, `@ngx-translate/*`, `@angular/cdk`, `@angular/animations`, and Tailwind + PostCSS + autoprefixer (configured but effectively unused). Delete `src/assets/i18n/` — zero references in the repo.
- Keep `src/styles.css` and its Kepler brand tokens essentially verbatim; self-host Montserrat and Nunito Sans instead of `@import`ing them from Google Fonts.
- Accessibility fix carried along: associate `for`/`id` on the form labels that currently lack them.
- **No user-visible change.** Same screens, routes, permissions, Spanish copy, validation messages and `localStorage` keys.

### Explicitly out of scope

The linked technical review (`RRHH BBDD — Revisión técnica`, 10 Aug 2026) raises findings that this change deliberately does **not** address, because they are backend concerns unrelated to the framework and would double the risk on one branch:

| Finding                                                                            | Why deferred                                                                                   |
| ---------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `C-1` all persistence is `localStorage`; Supabase is never queried                 | Separate ticket; wiring Supabase is an order-of-magnitude larger change                        |
| `C-2` role is chosen by the user at login; permissions come from a client constant | The login role selector is the contract `tests/e2e/global-setup.ts` and all 13 specs depend on |
| `C-3` Edge Functions do not verify the bearer token                                | No Edge Function is touched here                                                               |
| `C-4` candidate personal data persisted in clear in the browser                    | Follows from `C-1`; remains an open ticket                                                     |
| `A-1`..`A-5`                                                                       | Integration tests, security gates, DB triggers, bucket policy — all backend                    |

The review does **not** recommend React; its only stack-related finding is `M-4` ("Angular 21 used with Angular 15 patterns"). The migration is a product decision, and this proposal treats it as such.

## Capabilities

This change declares `skip_specs: true` in `.openspec.yaml`.

### New Capabilities

None.

### Modified Capabilities

None. Specs describe behaviour, and this change is defined by the absence of behavioural change: every screen, route, permission check, validation message, Spanish string and `localStorage` key is preserved byte for byte. `openspec/specs/` is currently empty, and inventing a requirement purely to satisfy validation would misrepresent the work.

## Actors and user value

| Actor                                        | Value                                                                          |
| -------------------------------------------- | ------------------------------------------------------------------------------ |
| RRHH staff (`rrhh_admin`, `rrhh_user`)       | Nothing to relearn; saved candidates, catalogs and search presets keep working |
| Managers (`manager_reader`), read-only users | Unchanged access and unchanged permission gating                               |
| Development team                             | A React frontend with business logic intact, ready for KTL-4                   |

## Functional requirements

- **FR-1** Every route in `app.routes.ts` resolves to the same page under the same permission, including the ordering that makes `/app/candidates/new` render the create form rather than the detail page.
- **FR-2** All 9 `localStorage` keys are read and written unchanged, including the legacy preset-shape migration in `search-presets.service.ts`.
- **FR-3** Business validation keeps throwing the same errors with the same Spanish messages from the same service classes.
- **FR-4** Editing a search filter does not re-run the search until "Buscar" is pressed.
- **FR-5** The login page keeps `#email`, `#password` and `#role` with the same option values, and navigates to `/app` on submit.
- **FR-6** The container serves the app and a substituted `env.js` on port 63151.

## Key entities

Unchanged: `Candidate` and its six sub-entities (languages, programs, education, experience, skills, documents), `CatalogFamily` (9 families), `UserProfile`, `RoleDefinition`, `Permission` (12), `SearchFilters` / `SearchPreset`, and the import/export batch records.

## Assumptions

- The demo/`localStorage` mode remains the operating mode after this change; Supabase stays wired only for `signInWithPassword`, `signOut` and `getAuthenticatorAssuranceLevel`.
- No CI exists (`.github/` has templates only), so verification is the local `npm run release:gate` sequence plus manual container checks.
- The Playwright suite is a trustworthy oracle for UI parity. It is not a security oracle — `tests/e2e/security-ops.spec.ts` asserts UI gating, not an authorization boundary (review finding `A-3`), and this change neither improves nor worsens that.

## Edge cases

- A user with an existing session and populated `localStorage` loads the new build — must render identically.
- Deep-linking to `/app/candidates/:id` must work through nginx `try_files` and Vite's SPA fallback.
- `input type="number"` (`yearsExperience`) yields a string in React where `ngModel` coerced to number; it feeds search comparisons and CSV export.
- Removing Angular's style encapsulation exposes previously-scoped generic selectors (`header`, `nav a`, `main`, `.overlay`, `.modal`).
- A stale Angular dev server left on port 4200 would make the e2e suite pass against the old app.

## Success criteria

| #    | Criterion                                                                                                                                            |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| SC-1 | 13/13 Playwright specs pass, with one documented change: 4 locators in `tests/e2e/candidate-profile.spec.ts` that target Angular custom-element tags |
| SC-2 | 15/15 unit + 5/5 integration specs pass; ≥11 unit specs change only `jest.` → `vi.`                                                                  |
| SC-3 | `npm run release:gate` exits 0                                                                                                                       |
| SC-4 | The Docker image serves the app and a substituted `env.js` on 63151                                                                                  |
| SC-5 | Initial bundle stays under the 700 kB warning budget `angular.json` used to enforce                                                                  |
| SC-6 | A `localStorage` state produced by the Angular build renders identically in the React build                                                          |
| SC-7 | Zero changes to Spanish UI copy                                                                                                                      |

## Security and data protection

Assessed against principles 1 and 3:

- **Personal data:** no change to what is collected, displayed or exported. The same candidate fields render on the same screens for the same roles. Storage keys and shapes are identical, so no new copy of personal data is created and none is removed. Finding `C-4` (candidate data sitting in `localStorage` in clear) is neither introduced nor fixed here — it predates this change and stays open.
- **RLS policies:** none touched. No SQL migration is added or modified.
- **Storage access:** none touched. The private `candidate-cvs` bucket, its policies, and `document.service.createSecureUrl()` behaviour are unchanged.
- **Role definitions:** none touched. `DEFAULT_ROLES`, the 12 `Permission` values and the permission-to-route mapping are ported verbatim.
- Client-side guards remain what they are today: UX affordances, not an authorization boundary, exactly as `SECURITY.md` states.
- `tests/security/*.sql`, `npm run security:rls` and `npm run security:storage` are unchanged and must still pass, as evidence of no regression.

**Principle 2 departure:** Angular is the ratified corporate stack, so replacing it requires a documented reason, a simpler alternative considered, and mitigations. That record belongs in `design.md`.

## Impact

**Code:** all of `src/app/` (~6,300 LOC TS); `src/styles.css`; `src/index.html` → root `index.html`.

**Config:** `package.json` (scripts, ~25 dependencies removed, ~12 added, `overrides` block deleted), `tsconfig.json` (+ deletion of `tsconfig.app.json` / `tsconfig.spec.json`), `eslint.config.js`, new `vite.config.ts`. Deleted: `angular.json`, `jest.config.js`, `setup-jest.ts`, `tailwind.config.js`, `postcss.config.js`.

**Build/deploy:** one line in `Dockerfile.frontend`. `scripts/release-gate.js:6` must drop `--runInBand`, which Vitest rejects.

**Tests:** `jest.config.js` → Vitest block in `vite.config.ts`; `setup-jest.ts` → `tests/setup.ts` (keeping the `structuredClone` and `URL.createObjectURL` polyfills); 4 unit specs rewritten; 4 locators in one e2e spec.

**Docs:** `README.md`, `openspec/config.yaml` context block, `specs/001-gestion-cvs-rrhh/plan.md`, `AGENTS.md`, `.claude/commands/enrich-us.md`.

**Unaffected:** `supabase/**`, `tests/security/**`, `tests/integration/**`, `nginx.conf`, `docker-compose.frontend.yml`, `docker-entrypoint.d/`, `public/env*.js`, `playwright.config.ts`, `.github/**`, `scripts/check-rls.js`, `scripts/check-storage-policies.js`.
