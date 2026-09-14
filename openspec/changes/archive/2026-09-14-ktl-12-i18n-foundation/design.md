## Context

See proposal.md for motivation and `specs/frontend-localization/spec.md` for the behaviour contract.

Current state that shapes the approach:

- `src/main.tsx` renders `RouterProvider` synchronously after `evictSupersededStorage()`. There is no async bootstrap and no `<StrictMode>`.
- `src/assets/i18n/es.json` holds 51 flat keys (`feature.section.element`). Its accents are already correct, contrary to the KTL-3 note. Vite does not emit it because nothing imports it.
- The five profile sections hardcode copy that matches `candidate.profile.*` keys one-to-one. The login page hardcodes copy with **no** matching keys (`Entrar`, `Contraseña`, `Rol local`, the intro line, the sign-in fallback error). The existing `auth.login` ("Iniciar sesión") is not what the page renders.
- `candidate-relations.service.ts` throws `new Error('<Spanish sentence>')` at nine sites. Components render `err.message`.
- Unit tests (`tests/unit/candidate-profile-sections.spec.tsx`) and e2e specs (`candidate-profile.spec.ts`, `candidate-api-cutover.spec.ts`) locate elements by rendered Spanish text. Unit tests share `tests/setup.ts`.
- No component formats dates or numbers with a locale today.
- ESLint 9 flat config in `eslint.config.js`.

## Goals / Non-Goals

**Goals:**

- One import (`useTranslation` / `t`) is the obvious and lint-enforced way to write copy in any new or migrated file.
- Zero change in rendered output and zero change to e2e specs.
- Adding English later only means adding a resource and a language-selection source. No component edits for migrated files.

**Non-Goals:**

- Namespaces, lazy loading, or a runtime backend for translations.
- Typed keys (compile-time key checking). Worth doing once key count grows. Deferred.
- Migrating errors thrown outside the relations service, such as transport, auth, or candidate/catalog services.

## Decisions

### D1. `i18next` + `react-i18next`, bundled resources, synchronous init

Create `src/app/core/i18n/i18n.ts`: `i18next.use(initReactI18next).init({ lng: 'es', fallbackLng: 'es', supportedLngs: ['es'], resources: { es: { translation: es } }, initImmediate: false, keySeparator: false, nsSeparator: false, interpolation: { escapeValue: false } })`. `main.tsx` imports it before the router.

- **Why this library:** it is the de-facto React standard, KTL-3 already named it, and it supports interpolation, plurals and later language detection without custom code. That is the documented reason for a new runtime dependency (principle 2).
- **`keySeparator: false`:** keys stay flat strings, matching the existing file. Dots do not become nesting.
- **Bundled over HTTP backend:** the build is guaranteed to ship the file, there are no loading or suspense states, and translations cannot fail independently of the app. The JSON is a few KB.
- **Fixed `lng`, no detector:** this satisfies "browser language does not matter". The later English ticket adds a detector.
- **Alternatives considered:** `react-intl` (ICU messages, heavier authoring, no existing key scheme to reuse), and a hand-rolled `t()` over the JSON (cheap now, but reimplements plurals and interpolation later).

### D2. Missing keys: throw in tests, warn in development

`saveMissing: true` plus a `missingKeyHandler`. It throws when `import.meta.env.MODE === 'test'` and calls `console.warn` in development. In production i18next falls back to the key, which the tests have already ruled out for shipped code. `tests/setup.ts` imports the same `i18n.ts`, so components render real Spanish in jsdom and existing text-based assertions keep passing.

### D3. Keys for login

Add `auth.login.intro`, `auth.login.email`, `auth.login.password`, `auth.login.role`, `auth.login.submit`, `auth.login.failed`, with values copied verbatim from the page. Leave `auth.login` untouched: it is unused, and repurposing it would collide with the flat `auth.login.*` family only visually, since `keySeparator: false`. `app.title` covers the "Kepler Talento" heading. `DEFAULT_ROLES[].label` is data from a shared model, not JSX text, and is out of scope.

### D4. Keyed errors: `TranslatableError`

Add `src/app/core/i18n/translatable-error.ts`:
`class TranslatableError extends Error { constructor(readonly key: string, readonly values?: Record<string, unknown>) { super(i18n.t(key, values)); } }`.

- `message` is already Spanish, so any caller that still renders `err.message` (and any test asserting it) keeps working. Components use a small `errorText(err, t)` helper: `err instanceof TranslatableError ? t(err.key, err.values) : (err as Error).message`.
- The relations service's nine throws become `new TranslatableError('candidate.profile.languages.duplicate')` and so on. All nine already have keys in `es.json`.
- **Alternative considered:** throwing error codes and mapping them in components. This breaks every existing `toThrow('<Spanish>')` assertion and every caller rendering `message` in one step. Keeping `message` populated makes the migration incremental.

### D5. Helpers for later work

- `catalogLabel(item: { nameEs: string; nameEn?: string }): string` in `src/app/features/catalogs/catalog-label.ts` returns `nameEs`. The existing `catalogs.activeNames()` returns plain strings and is left alone. The helper exists for new code that works with items.
- `formatDate(value, options?)` and `formatNumber(value, options?)` in `src/app/core/i18n/format.ts` use `Intl` with `i18n.language`. There are no current callers. Adding them now gives the rule in `AGENTS.md` something concrete to point at.

### D6. Lint: `eslint-plugin-i18next`, error by default, explicit legacy list

In `eslint.config.js`, add a block for `src/**/*.tsx` with `i18next/no-literal-string: ['error', { mode: 'jsx-text-only' }]`. Follow it with a block whose `files` is a `LEGACY_HARDCODED_COPY` array of the currently unmigrated `.tsx` paths, turning the rule `off`.

- **`jsx-text-only`:** it catches the actual debt (text between tags) without flagging class names, ids, `name=`, test ids or route strings. Hardcoded attribute copy (`placeholder`, `aria-label`, `title`) is not caught. That gap is accepted and covered by the written rule and review.
- **Explicit list over "new files only":** ESLint cannot see git history. A warning-count ratchet needs a stored baseline and CI scripting. A list in the config is visible, reviewable in diffs, and trivially shrinks. A header comment says entries may only be removed.
- **Scope:** `.tsx` only. Service-layer strings in `.ts` (thrown messages) are governed by the written rule, not lint.
- **Alternative considered:** warn everywhere. Warnings are ignored in practice, and the spec requires a failure for new files.

### D7. Rules text

- **`AGENTS.md` _Frontend_:** replace "thrown `Error`s with Spanish messages" with "thrown `TranslatableError(key, values)` for new or changed validation. Existing plain `Error`s stay until touched."
- **`AGENTS.md` _Language_:**
  - UI copy is Spanish and lives in `src/assets/i18n/es.json`, read through `t()`.
  - New or changed JSX copy must not be hardcoded (lint-enforced outside the legacy list).
  - Adding `en.json` values is welcome but not required.
  - New tests locate elements by role and accessible name, label, or `data-testid`. Asserting on Spanish text resolved through `t()` in a unit test is acceptable. Hardcoding it in new e2e selectors is not.
  - When you change a legacy file's copy, migrate that file and remove it from the legacy list. This is required, not only encouraged, because it is the only mechanism that shrinks the debt.
  - Keep "never translate a rendered Spanish literal" as it is.
- **`openspec/config.yaml` `context`:** extend the "User-facing copy is Spanish" line with "sourced from `src/assets/i18n/es.json` through i18next; no hardcoded JSX copy outside the lint legacy list."

### Stack, data, authorization, storage

- **Stack:** two runtime dependencies and one dev dependency, frontend only.
- **Data model, migrations, authorization, storage:** none. No endpoint, grant, permission or stored field changes. No departure from the standing principles.

### Test strategy

- **Unit:** `tests/setup.ts` initializes i18n. The existing `candidate-profile-sections.spec.tsx` and `route-guards`/login-related specs run unchanged. Add `tests/unit/i18n.spec.ts`, covering:
  - The active language is `es` regardless of `navigator.language`.
  - A missing key throws under test.
  - `TranslatableError` exposes key and Spanish message.
  - `catalogLabel` and the format helpers.
- **Lint:** a fixture check. Temporarily add a `.tsx` with JSX text outside the legacy list, run `npm run lint`, and confirm it fails. This is manual, recorded in tasks, and not committed.
- **Build:** after `npm run build`, grep `dist/` for a Spanish value only present in `es.json` usage (e.g. `Sin idiomas asociados.`).
- **E2E:** run `candidate-profile.spec.ts` and `candidate-api-cutover.spec.ts` unchanged, plus the login journey.
- **Security:** no boundary changes, so no new `tests/security` checks. The existing suite runs as regression.

## Risks / Trade-offs

- [Missing-key throw breaks unrelated unit tests that render migrated components with partial mocks] → They use the same real resources via `tests/setup.ts`, so only genuinely missing keys throw.
- [`jsx-text-only` misses attribute copy (`placeholder`, `aria-label`)] → Written rule plus review. The rule's `jsx-attributes` include list can be tightened later without changing the spec.
- [Legacy list entries get re-added to silence lint] → The header comment and `AGENTS.md` rule forbid it, and the diff is visible in review.
- [`TranslatableError` calls `i18n.t` at construction, coupling services to i18n init order] → `i18n.ts` initializes synchronously at import, and `main.tsx` and `tests/setup.ts` both import it first. A service constructed before init would get the raw key as `message`, but components resolve by `key` anyway.
- [Bundle grows by i18next (~15 KB gz)] → Accepted for an internal intranet app.

## Migration Plan

This is a frontend-only deploy. Rollback is a revert of the merge commit. There is no data or contract to unwind.
