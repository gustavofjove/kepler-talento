## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-12` from an up-to-date `main`

## 1. i18n setup (spec: single active language, resources ship with the build, missing keys detectable)

- [x] 1.1 Add `i18next` and `react-i18next` as dependencies and `eslint-plugin-i18next` as a dev dependency. Commit the lockfile. (Installed and lockfile updated; commit left to the user.)
- [x] 1.2 Create `src/app/core/i18n/i18n.ts` per design D1/D2: bundled `es.json`, fixed `lng: 'es'`, no detector, `keySeparator: false`, synchronous init, missing-key handler that throws in test mode and warns in development
- [x] 1.3 Import `i18n.ts` in `src/main.tsx` before the router is created
- [x] 1.4 Import `i18n.ts` in `tests/setup.ts`
- [x] 1.5 Verify every `es.json` value has correct accents (the brief's 18 missing accents are believed already fixed; confirm, correct any found). Confirmed all correct; no changes needed.

## 2. Helpers (design D4, D5)

- [x] 2.1 Add `src/app/core/i18n/translatable-error.ts` (`TranslatableError` with `key`, `values`, Spanish `message`) and an `errorText(err, t)` helper
- [x] 2.2 Add `src/app/features/catalogs/catalog-label.ts` (`catalogLabel` returning `nameEs`)
- [x] 2.3 Add `src/app/core/i18n/format.ts` (`formatDate`, `formatNumber` using `Intl` with `i18n.language`)
- [x] 2.4 Add `tests/unit/i18n.spec.ts` covering:
  - active language is `es` with an English `navigator.language`
  - missing key throws
  - `TranslatableError` key and message
  - `errorText` for keyed and plain errors
  - `catalogLabel`
  - format helpers

## 3. Migrate components (spec: migrated screens keep their copy, relation errors from resources)

- [x] 3.1 Replace the nine `throw new Error(...)` sites in `candidate-relations.service.ts` with `TranslatableError` using the existing `candidate.profile.*` keys
- [x] 3.2 Migrate `candidate-languages.tsx` and `candidate-programs.tsx` to `t()`, rendering errors through `errorText`
- [x] 3.3 Migrate `candidate-education.tsx`, `candidate-experience.tsx` and `candidate-skills.tsx` to `t()`, rendering errors through `errorText` (added `candidate.profile.yearsCount` and `candidate.profile.experience.currentBadge` for the item-row fragments)
- [x] 3.4 Add the `auth.login.*` keys (design D3) to `es.json`, with values copied verbatim, and add English values to `en.json`
- [x] 3.5 Migrate `login-page.tsx` to `t()`, using `app.title` for the heading
- [x] 3.6 Diff rendered copy against `main` for all six components (read the diff line by line): no text or accent changes

## 4. Lint gate (spec: hardcoded copy rejected outside the legacy list)

- [x] 4.1 Add the `i18next/no-literal-string` block (`jsx-text-only`, error) for `src/**/*.tsx` in `eslint.config.js`
- [x] 4.2 Add a `LEGACY_HARDCODED_COPY` list with every still-unmigrated `.tsx` that currently fails the rule. Set the rule `off` for those files, and add a header comment stating entries may only be removed. (20 files.)
- [x] 4.3 Run `npm run lint`. Confirm it passes and that none of the six migrated files are on the list.
- [x] 4.4 Run the negative check. Create a temporary `src/app/lint-probe.tsx` rendering `<p>Texto fijo</p>`, run `npm run lint`, and confirm it fails on that file. Delete the probe and re-run lint to confirm it passes.

## 5. Rules and documentation

- [x] 5.1 Update `AGENTS.md`:
  - _Frontend_: `TranslatableError` for new or changed validation.
  - _Language_: copy from `es.json` via `t()`, no hardcoded JSX copy, test-selector rule, and required migrate-and-delist when changing a legacy file's copy.
- [x] 5.2 Update the `openspec/config.yaml` context line on user-facing copy (design D7)
- [x] 5.3 Add a short "Textos de interfaz (i18n)" note to `README.md` in Spanish, covering where copy lives, how to add a key, and the legacy lint list
- [x] 5.4 Remove the stale "not wired / not emitted" correction note from active guidance only if referenced outside the archive. Leave the archived KTL-3 design as it is. (No reference outside the archive; nothing to remove.)

## 6. Tests and verification

- [x] 6.1 Review existing unit tests affected by the change (`candidate-profile-sections.spec.tsx`, `candidate-relations.service.spec.ts`, `route-guards.spec.tsx` and any login rendering). Update only if the i18n setup requires it, and keep their Spanish assertions. (Reviewed; none needed changes. Their Spanish `toThrow`/text assertions pass through `TranslatableError.message`.)
- [x] 6.2 Run `npm test` (unit project) and confirm it passes with no missing-key failures (24 files, 172 tests passed)
- [x] 6.3 Run `npm run test:integration` and `npm run test:security`, and confirm no regressions. No PostgreSQL or storage state changes are expected; confirm none occurred. (Integration 15/15, security 8/8 passed.)
- [x] 6.4 Run `npm run build`. Confirm `dist/` contains `Sin idiomas asociados.` and that no `i18n/*.json` request exists in the built app. (Copy found in the JS bundle; no JSON emitted or referenced.)

## 7. End to end

> Environment fix made during this group: e2e used to reuse the Compose nginx on :4200,
> which serves the bundle built into its image (2026-09-11), so earlier runs did not test
> this branch. Vite now runs on :5173 with an `/api` proxy to :4200, and Playwright targets
> :5173 (`vite.config.ts`, `playwright.config.ts`, `package.json`, `tests/e2e/global-setup.ts`,
> `AGENTS.md`). The results below are from runs against the branch.

- [x] 7.1 Start the dev stack and run `npx playwright test tests/e2e/candidate-profile.spec.ts`. Confirm it passes unchanged. (Passes, unchanged spec.)
- [x] 7.2 Run `npx playwright test tests/e2e/candidate-api-cutover.spec.ts`. Confirm it passes unchanged. (5/5 pass, including the document upload, after a one-off re-own of the documents volume to `app`: 1867 root-owned entries, now 0. The permanent fix stays in KTL-13.)
- [ ] 7.3 Run the full `npm run e2e` suite and confirm the login journey passes. (Login journey passes. Full suite: 58 passed, 1 failed; the upload-related failures are gone. `advanced-search.spec.ts` run on its own: 4 passed, 3 failed. The failures are at the assertions on lines 33, 52/58 and 97: they search "Laura" and assume the e2e seed "Laura Garcia" is the only match, but the database has 8 active Lauras, 7 of them KTL-11 synthetic candidates. This is a fixture collision with development data; the search pages are not touched by this change.)
- [x] 7.4 Restore seed data afterwards if the specs left test candidates behind. (29 still-active e2e candidates (`Doc`/`Storage`/`Conf`/`Del`/`Rel`/`Filtro` + epoch) logically deactivated through `PUT /api/candidates/{id}/active`, 29/29 succeeded; none left active. The "Laura Garcia" seed was kept, as the search specs need it. Nothing was physically deleted.)

## 8. Finish

- [x] 8.1 Run `npm run lint` and `npm run format:check`, and fix any findings (lint passes. The change's markdown files were formatted. The remaining `format:check` warnings are CRLF working-copy line endings from `core.autocrlf=true` on 43 files, many of them untouched by this change: `prettier --check . --end-of-line auto` passes repo-wide.)
- [x] 8.2 Update `openspec/KTL-12.md` status and note that the accent item was already resolved
