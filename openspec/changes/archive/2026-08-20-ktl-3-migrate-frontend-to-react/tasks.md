# Tasks — Angular 21 → React 19 migration

Requirements: [proposal.md](./proposal.md) (FR-1..FR-6, SC-1..SC-7) · Approach: [design.md](./design.md) (D1..D9)

**Standing checklist for every UI task (D8).** Before ticking any task that ports a component, confirm it preserved: the `name=` attribute on every form control, every `data-testid`, every element id, and every Spanish string verbatim. These are decorative in React and silently break Playwright locators if dropped.

## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-3` from `chore/adopt-openspec-drop-speckit` (not `main` — that branch is one commit ahead with the OpenSpec adoption `d9b8941` this workflow depends on)

## 1. Toolchain scaffolding (M0)

- [x] 1.1 Add React runtime deps (`react`, `react-dom`, `react-router`) and dev deps (`vite`, `@vitejs/plugin-react`, `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `@types/react`, `@types/react-dom`, `eslint-plugin-react-hooks`, `eslint-plugin-react-refresh`, `@fontsource/montserrat`, `@fontsource/nunito-sans`)
- [x] 1.2 Remove Angular-era deps: all `@angular/*`, `@ngx-translate/*`, `rxjs`, `zone.js`, `tslib`, `jest`, `jest-environment-jsdom`, `jest-preset-angular`, `@types/jest`, `tailwindcss`, `@tailwindcss/postcss`, `postcss`, `autoprefixer`, and the entire `overrides` block; run `npm audit` to confirm nothing regressed
- [x] 1.3 Rewrite `package.json` scripts: `start` → `vite --host 0.0.0.0 --port 4200 --strictPort` (D8 — `--strictPort` prevents false-green e2e runs), `build` → `tsc -b && vite build`, `test` → `vitest run`, `test:watch`, `test:integration` → `vitest run tests/integration`; delete `ng` and `watch`; add `tsx` to the `lint-staged` glob
- [x] 1.4 Write `vite.config.ts`: react plugin, `server.port 4200` + `strictPort`, `build.outDir 'dist'`, and the `test` block (`globals`, `setupFiles: ['./tests/setup.ts']`, `include tests/**`, `exclude tests/e2e/**`, `environment: 'jsdom'`, `environmentMatchGlobs` mapping `tests/integration/**` to `node`)
- [x] 1.5 Rewrite `tsconfig.json` as a single file: add `jsx: "react-jsx"`, `noEmit`, `isolatedModules`, `types`; remove `experimentalDecorators`, `useDefineForClassFields: false`, `importHelpers`, `outDir` and `angularCompilerOptions`; keep `strict` and `noPropertyAccessFromIndexSignature` (needed by `search-filters`'s computed keys). Add `tsconfig.node.json`; delete `tsconfig.app.json` and `tsconfig.spec.json`
- [x] 1.6 Update `eslint.config.js`: widen source globs to `{ts,tsx}`, add `react-hooks` with **`exhaustive-deps` as `error`** (design risk mitigation) and `react-refresh`; drop the deleted config files from the node-globals block; leave the `public/*.js` and `supabase/functions/**` Deno blocks untouched; swap `.angular/**` for `.vite/**` in `ignores`
- [x] 1.7 Move `src/index.html` to repo root: keep `lang="es"` and the `<script src="/env.js">` **before** the module script, drop `<base href>`, replace `<rrhh-root>` with `<div id="root"></div>` + `<script type="module" src="/src/main.tsx">`
- [x] 1.8 Patch `scripts/release-gate.js:6` from `'npm test -- --runInBand'` to `'npm test'` (Vitest rejects the flag), then run `npm run release:gate` once to confirm the script shape before it is depended on

## 2. State layer and service port (M0)

- [x] 2.1 Write `src/app/core/state/signal.ts` — `signal<T>(initial)` returning a callable with `set`, `update` and `subscribe`, using `Object.is` to skip no-op notifications
- [x] 2.2 Write `src/app/core/state/use-signal.ts` — `useSignal(sig)` over `useSyncExternalStore`, taking a signal (not a selector) so the "getSnapshot should be cached" trap is hard to express (D2)
- [x] 2.3 Write `src/app/core/routing/navigator.ts` — `AppNavigator` interface plus a data-router-backed adapter with `attach(router)` (D4)
- [x] 2.4 Write `src/app/core/di/services.ts` — explicit acyclic singleton graph for all 16 services — and `services-context.tsx` whose context default is that graph
- [x] 2.5 Port the 8 `core/` and `shared/` services (auth, mfa, supabase-client, toast, observability, confirm-dialog, plus the models modules): delete `@Injectable`, swap the `signal` import, swap `Router` for `AppNavigator` in `auth.service.ts`. No logic changes
- [x] 2.6 Port the 8 feature services (`candidate`, `candidate-relations`, `catalog`, `document`, `candidate-search`, `search-presets`, `export`, `import`, `role`, `profile`): decorator + import line only. Verify `localStorage` keys are untouched (FR-2)
- [x] 2.7 Write `src/main.tsx` — create the router, `appNavigator.attach(router)`, render without `<StrictMode>` (D9)
- [x] 2.8 Clean `src/styles.css`: remove the Google Fonts `@import` and the three `@tailwind` lines; import `@fontsource/montserrat` and `@fontsource/nunito-sans` from `main.tsx`. Brand tokens unchanged. Delete `tailwind.config.js` and `postcss.config.js`
- [x] 2.9 Migrate `setup-jest.ts` to `tests/setup.ts`: drop `setupZoneTestEnv()`, **keep** the `structuredClone` and `URL.createObjectURL`/`revokeObjectURL` polyfills, add `@testing-library/jest-dom/vitest` and `afterEach(cleanup)`. Delete `jest.config.js` and `setup-jest.ts`
- [x] 2.10 **Update affected unit tests:** rename `jest.` → `vi.` and `jest.Mocked` → `MockedObject` across the 11 service specs. No assertion changes
- [x] 2.11 **Run** `npx tsc --noEmit`, `npm test` and `npm run test:integration`; confirm the 11 service specs and 5 integration specs pass (SC-2) and that `npm run build` emits `dist/index.html` + `dist/env.js`

## 3. Shell, routing and auth (M1)

- [x] 3.1 Write `core/routing/require-auth.tsx` and `require-permission.tsx` as layout routes returning `<Navigate replace>` or `<Outlet />`; add the `usePermission` hook that subscribes before calling `hasPermission` (D3)
- [x] 3.2 Write `src/app/app.tsx` with the full route table, preserving the `candidates/new` before `candidates/:id` resolution (FR-1) and the per-route permission mapping; delete `app.component.ts`, `app.config.ts`, `app.routes.ts` and `core/guards/auth.guard.ts`
- [x] 3.3 Port `LoginPage` — **keep ids `#email`, `#password`, `#role`**, the same option values (`rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly`), `button[type="submit"]`, and navigation to `/app` (FR-5) — and `MfaPage`
- [x] 3.4 Port `AppLayout`: permission-gated nav via `<NavLink>`, role badge, sign-out, toast stack, and the plain `<a class="skip-link">` with `<main id="main-content" tabIndex={-1}>`. Prefix the leaked generic selectors (`header`, `nav a`, `main` → `.shell …`) in a co-located `app-layout.css` (D7)
- [x] 3.5 Port `ConfirmDialog` + service: keep `data-testid` `confirm-dialog`/`confirm-accept`/`confirm-overlay`, `aria-modal`, and convert `@HostListener('document:keydown.escape')` to a `useEffect` listener that unmounts the dialog. Extract the shared `.overlay`/`.modal` rules into `shared/components/modal.css`
- [x] 3.6 **Update affected unit tests:** rewrite `tests/unit/auth-guards.spec.ts` as `route-guards.spec.tsx` using `<MemoryRouter>` + sentinel routes + `<ServicesProvider>`; all 6 cases map 1:1
- [x] 3.7 **Run** `npm test` then **execute** `npx playwright test tests/e2e/secure-access.spec.ts`; confirm `global-setup.ts` writes all four `tests/e2e/.auth/*.json` fixtures. Delete stale `.auth/` state first so the fixtures are genuinely regenerated

## 4. Dashboard and candidate list (M2 — mutation hazard)

- [x] 4.1 Extract `candidate-list.logic.ts` with pure `filterCandidates`, `sortCandidates`, `buildFilterChips` and `paginate`
- [x] 4.2 Port `CandidateListPage`, splitting into page + `candidate-filters-bar` + `candidate-table` + `pagination`; convert every getter to `useMemo`; convert row selection to immutable `Set` updates and type it `ReadonlySet<string>`
- [x] 4.3 Grep `.push(`, `.splice(`, `.sort(`, `.add(`, `.delete(` across `src/app` and convert each in-place mutation found; record what was changed
- [x] 4.4 Port `DashboardPage` — the six KPI getters become plain in-render computation (cheap, and avoids a stale-`useMemo` class of bug)
- [x] 4.5 **Update affected unit tests:** move the filtering/`includeInactive`/chip cases of `candidate-list-page.spec.ts` to `candidate-list.logic.spec.ts`; rewrite the bulk activate/deactivate cases as RTL with stubbed `confirmDialogService`/`toastService`, asserting the same Spanish toasts
- [x] 4.6 **Run** `npm test` and **execute** `npx playwright test tests/e2e/candidate-list-operations.spec.ts`

## 5. Candidate form, edit and detail (M3)

- [x] 5.1 Extract the pure `toDraft(candidate?): CandidateDraft` helper; port `CandidateForm` as a controlled form with `useState`, keeping `preventDefault()` + the manual check that yields `'Nombre y apellidos son obligatorios.'` (D5), and adding `htmlFor`/`id` to all 11 bare labels
- [x] 5.2 Port `CandidateEditPage` using `useParams` with an `''` default, and `key={candidateId || 'new'}` on the form so navigation resets state
- [x] 5.3 Port `CandidateDetailPage` with the audit panel, keeping `p:has-text("Activo:")` rendering `Si`/`No`, and logical delete/restore
- [x] 5.4 Verify `/app/candidates/new` renders the create form and not the detail page (React Router ranking vs Angular first-match-wins, FR-1)
- [x] 5.5 **Update affected unit tests:** split `candidate-form.spec.ts` — draft cases against `toDraft()`, submit cases as RTL asserting `onSave` is not called and the Spanish message renders
- [x] 5.6 **Run** `npm test` and **execute** `npx playwright test tests/e2e/candidate-crud.spec.ts`

## 6. Relation panels and documents (M4)

- [x] 6.1 Port `CandidateLanguages`, `CandidatePrograms` and `CandidateSkills`, keeping the per-section `select[name="language"|"level"|"skill"]` controls and the `try/catch` → local error string pattern
- [x] 6.2 Port `CandidateEducation` and `CandidateExperience`, keeping `select[name="educationType"|"sector"]` and `input[name="company"|"position"|"startDate"|"endDate"]`
- [x] 6.3 Add `data-testid="candidate-languages|programs|education|experience|skills"` to each section wrapper, and update the four locators in `tests/e2e/candidate-profile.spec.ts` lines 23/34/45/60 (D8 — the single permitted e2e edit)
- [x] 6.4 Port `CandidateDocuments`: PDF-only + 10 MB validation, set-primary, delete, `Abrir seguro`; file input stays uncontrolled
- [x] 6.5 Wrap every `input type="number"` binding (`yearsExperience`, `endYear`) with `Number(e.target.value)` — `ngModel` coerced, React does not, and these feed search and CSV export
- [x] 6.6 **Update affected unit tests:** rewrite `candidate-profile-sections.spec.ts` as RTL per section with `candidateRelationsService` doubled via `ServicesProvider`; same 5 cases and same Spanish messages
- [x] 6.7 **Run** `npm test` and **execute** `npx playwright test tests/e2e/candidate-profile.spec.ts tests/e2e/candidate-documents.spec.ts`

## 7. Advanced search (M5 — in-place `@Input` mutation hazard)

- [x] 7.1 Port `SearchFilters` as a **controlled** component (`filters` + `onFiltersChange`, `collapsed` + `onCollapsedChange`), converting every in-place mutation to an immutable update; split out `criteria-group.tsx` and `filters-summary.tsx`
- [x] 7.2 Port `AdvancedSearchPage` keeping `filters` and `results` in **separate** `useState` — `results` updates only in `run()`/`clear()`/`loadPreset()`, never via `useMemo` (FR-4); preserve `filtersCollapsed = results.length > 0` after each search
- [x] 7.3 Port `SearchResults` and the export-history modal, keeping `data-testid` `toggle-filters`, `filters-summary`, `open-export-history`, `export-history-modal`, `close-export-history`, and `input[name="text"|"presetName"]`, `select[name="selectedPreset"|"hasCv"]`. Replace the `| slice: 0 : 19` pipe with `.slice(0, 19)`
- [x] 7.4 Verify manually that typing in the "Texto" field with results on screen does **not** change the results table until "Buscar" is pressed (FR-4 — the regression a naive port introduces)
- [x] 7.5 **Run** `npm test` and **execute** `npx playwright test tests/e2e/advanced-search.spec.ts tests/e2e/advanced-search-presets.spec.ts tests/e2e/export-results.spec.ts`

## 8. Catalogs and admin (M6)

- [x] 8.1 Port `CatalogManagementPage`: family tabs, inline create/edit/reorder/toggle/delete, keeping `select[name="family"]`, `input[name="newCode"|"newNameEs"|"editNameEs"]` and the `Activar`/`Desactivar`/`Bajar`/`Eliminar`/`Guardar` labels
- [x] 8.2 Port `AdminUsersPage` and `AdminRolesPage` including the permission matrix, preserving the guardrails surfaced from `profile.service` and `role.service`
- [x] 8.3 Port `ImportPage`: file picker, dry-run then commit, error table, `downloadErrorsCsv()`; keep `getByRole('button', { name: 'Confirmar commit', exact: true })` and the verbatim `Modo: Dry run` / `Modo: Carga` / `Filas cargadas: N` / `Filas con error: N` strings
- [x] 8.4 **Run** `npm test` and **execute** `npx playwright test tests/e2e/catalogs-crud.spec.ts tests/e2e/import-access-csv.spec.ts tests/e2e/import-export-ops.spec.ts tests/e2e/security-ops.spec.ts`

## 9. Cleanup and delivery (M7)

- [x] 9.1 Delete the remaining Angular artefacts: `angular.json`, `src/main.ts`, `src/assets/` (unreferenced i18n), and any leftover `.component.ts` files; remove dead `.angular/` entries from `.gitignore`, `.prettierignore` and `.dockerignore`
- [x] 9.2 Change `Dockerfile.frontend`'s `COPY --from=build /app/dist/rrhh-bbdd/browser/ ./` to `COPY --from=build /app/dist/ ./`
- [x] 9.3 **Execute** `docker compose -f docker-compose.frontend.yml up -d --build`, then `curl` `/`, `/env.js` (must contain `__APP_CONFIG__`) and a deep link `/app/candidates` on port 63151; tear down afterwards (SC-4, FR-6)
- [x] 9.4 Verify the built bundle stays under the 700 kB initial budget the old `angular.json` enforced (SC-5)

## 10. Verification gate

- [x] 10.1 **Run** `npm run lint` and `npm run format:check`; fix all findings
- [x] 10.2 **Run** `npx kill-port 4200`, then `rm -rf node_modules dist .angular && npm ci` for a clean-slate check (prevents a stale Angular dev server producing false greens)
- [x] 10.3 **Run** `npm test` and `npm run test:integration` — 15/15 unit and 5/5 integration must pass (SC-2)
- [x] 10.4 **Execute the full e2e suite:** `npm run e2e`. 13/13 must pass with only the four documented locator changes from task 6.3 (SC-1)
- [x] 10.5 **Run** `npm run security:rls` and `npm run security:storage`, and the SQL checks under `tests/security/`, as evidence of no regression — this change touches no RLS, storage or role definitions, and these must still fail closed for unauthenticated and unauthorized users
- [x] 10.6 **Run** `npm run release:gate` and confirm exit 0 (SC-3)
- [x] 10.7 Manual data-continuity check: populate `localStorage` from the Angular build, dump it, load it into the React build on the same origin, and confirm identical rendering including the legacy preset-shape migration (SC-6)
- [ ] 10.8 Manual visual diff: side-by-side screenshots of dashboard, candidate list, candidate detail, advanced search (collapsed and expanded), catalogs and import — checking for leaked `.overlay`/`.modal`/`header`/`nav a` rules and correct self-hosted font weights (SC-7)

## 11. Documentation

- [x] 11.1 Update `README.md`: stack section (Angular 21 / RxJS / CDK / Tailwind / ngx-translate / Jest → React 19 / Vite / React Router 7 / own CSS / Vitest) and fix the stale "Estado" section that still claims there is no implementation
- [x] 11.2 Update the `context:` block in `openspec/config.yaml` (stack line, and Jest → Vitest). Do **not** touch the numbered principles
- [x] 11.3 Update `specs/001-gestion-cvs-rrhh/plan.md` — the six Angular mentions including line 63's "Stack boundary: PASS … Angular 21" — and add a dated stack-update note pointing at this change's `design.md` departure record
- [x] 11.4 Add a paragraph to `AGENTS.md`: React function components, state in singleton service classes via the `core/state/signal.ts` shim and `useSignal`, and do not reintroduce Angular idioms, RxJS or a state library
- [x] 11.5 Update `.claude/commands/enrich-us.md` step 5 (its "standalone components, signals" and "Jest" assumptions); the `src/app/**` paths remain valid
- [x] 11.6 Add a note to `SUPABASE_INTEGRATION_GUIDE.md` marking its Angular snippets as historical reference (optional, low priority)
