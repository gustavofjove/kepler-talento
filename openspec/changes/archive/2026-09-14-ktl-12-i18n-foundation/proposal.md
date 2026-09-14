## Why

English support is wanted eventually, but the frontend is not translatable today: `src/assets/i18n/{es,en}.json` exist and nothing reads them, no i18n library is installed, and every component hardcodes its Spanish copy. Each new feature adds more hardcoded copy and more tests that match Spanish text, so the eventual migration grows with every merge. This change puts the translation path in place now, with Spanish as the only active language, so new work stops adding to that debt (brief: `openspec/KTL-12.md`).

## What Changes

- Add `i18next` and `react-i18next` (new runtime dependencies: see design.md for the reason), initialized before the app renders with Spanish as the fixed, only active language.
- Bundle `es.json` into the build instead of leaving it as an unemitted asset. `en.json` stays in the tree, unloaded.
- Move the login page and the five candidate profile sections (languages, programs, education, experience, skills) onto translation keys. Rendered Spanish copy stays identical.
- Relation validation errors in the candidate relations service carry a translation key the component resolves, instead of only a Spanish sentence.
- Add a `catalogLabel` helper and a language-aware date/number formatting helper, so later catalog and formatting work does not hardcode `nameEs` or a locale.
- A lint rule rejecting hardcoded JSX text in any frontend file not on an explicit legacy list. The list only shrinks.
- Amend `AGENTS.md` (_Frontend_ and _Language_) and `openspec/config.yaml` so new or changed copy goes through keys and new tests stop asserting on literal Spanish text.

No user-visible change. No language switch.

**Actors:** developers and coding agents writing frontend copy. End users (HR staff) see no difference.

**Assumptions:** Spanish remains the only language shipped until a later ticket adds English and a switch. The `feature.section.element` key scheme already in `es.json` is kept.

**Edge cases:** a key missing from `es.json` must be visible in development and in tests, not silently rendered as the raw key in production copy. Errors thrown outside the relations service (API transport, auth) keep their current Spanish messages.

**Success criteria:**

- `npm run build` output contains the Spanish translations.
- The six migrated components render only through keys, and existing unit and e2e tests pass unchanged, apart from test setup.
- A new `.tsx` file with hardcoded JSX text fails `npm run lint`. Unmigrated legacy files do not.
- `AGENTS.md` and `openspec/config.yaml` state the copy, key and test-selector rules.

## Capabilities

### New Capabilities

- `frontend-localization`: how user-facing frontend copy is sourced. A single active language (Spanish) comes from bundled translation resources, missing keys are handled, errors carry keys, and hardcoded copy in non-legacy files is rejected at lint time.

### Modified Capabilities

None. No existing requirement changes. Rendered copy in `candidate-management` and `primary-navigation` screens stays identical.

## Impact

- **Code:** `src/main.tsx`, new `src/app/core/i18n/` module, `src/app/core/auth/login-page.tsx`, `src/app/features/candidates/components/candidate-{languages,programs,education,experience,skills}.tsx`, `src/app/features/candidates/services/candidate-relations.service.ts`, `src/assets/i18n/es.json` (new login keys), `eslint.config.js`, `tests/setup.ts`.
- **Dependencies:** `i18next`, `react-i18next` (runtime); `eslint-plugin-i18next` (dev).
- **Docs/rules:** `AGENTS.md`, `openspec/config.yaml`.
- **APIs, backend, database, storage:** none.
- **Personal data, RLS, storage access, roles:** not touched. Only UI copy sourcing and the frontend build change. No candidate data is read, stored, logged or exposed differently, so principles 1 and 3 are unaffected.
- **Stale brief item:** the brief asks to fix 18 missing accents in `es.json`. They are already correct in the tree, so this change only verifies them.
