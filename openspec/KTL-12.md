# KTL-12 — i18n foundation: stop new copy from adding translation debt

**Status:** Implemented in change `ktl-12-i18n-foundation` (pending archive)
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-3 (React frontend)

> Implementation note: the "fix 18 missing accents in `es.json`" item was already
> resolved in the tree before this change; it was verified, not edited.

## Summary

Add the translation setup to the React frontend with Spanish as the only active language,
and make it the required path for new and changed user-facing copy. Users see no change.
The goal is that every later ticket adds translation keys instead of hardcoded strings, so
a future English ticket is mostly translation work, not a sweep through every component.

## Why

The product owner wants English support eventually, and the repository looks further
along than it is. `src/assets/i18n/{es,en}.json` exist (about 51 keys each), but nothing
reads them. No i18n library is installed: `@ngx-translate` left with the Angular frontend
in KTL-3. Vite does not emit the files, and all UI copy is hardcoded Spanish, in 21 of the
33 `.tsx` files. Catalog items carry an optional `nameEn` that the UI never uses.

Adding a language switch today would change about 51 strings and leave the rest of the
screen in Spanish. Moving every string to keys in one go is a mid-sized ticket of its own.
Meanwhile each new feature adds more hardcoded copy, and tests that match Spanish text,
so the eventual migration grows with every merge. This ticket stops that growth cheaply
and leaves the backlog for later.

The KTL-3 design notes (post-archive correction) record two things to settle before the
files are wired: 18 Spanish values are missing accents, and the files are not in the Vite
build.

## In scope

- Add `i18next` and `react-i18next`, and initialize them before the app renders, with
  `lng: 'es'` and `fallbackLng: 'es'`.
- Bundle the translations (import them and pass them as `resources`) rather than fetching
  them at runtime, so the files are guaranteed to ship and nothing loads asynchronously.
- Fix the 18 missing accents in `es.json`, using the forms the live templates already
  have (commit `2fdf86f`).
- Keep `en.json` in the tree. It is not loaded as an active language and gets no switch.
- A key convention matching the existing scheme: `feature.section.element`, whole
  sentences with interpolation (`t('x.count', { count })`), plurals through i18next, and no
  string concatenation.
- A `catalogLabel(item)` helper that returns `nameEs` today, used by new catalog displays,
  so switching to `nameEn ?? nameEs` later is a one-line change.
- A shared date/number formatting helper that takes the current language instead of a
  hardcoded locale.
- Written rules in `openspec/config.yaml` and in `AGENTS.md` (the shared agent rules file
  that `CLAUDE.md` imports, so Claude Code and Codex both pick them up):
  - New or changed user-facing copy goes through `t()`, with its key in `es.json`.
  - Adding the English value to `en.json` is encouraged but not required.
  - New tests find elements by role, accessible name derived from keys, or `data-testid`,
    not by literal Spanish text.
  - The existing rule that Spanish copy keeps correct accents still applies to `es.json`.
- Amend the existing `AGENTS.md` rules that currently steer toward hardcoded Spanish:
  - _Language_ ("UI copy is Spanish … in components, service validation messages, `es.json`
    and test assertions on rendered text"): Spanish stays the only active language, but the
    copy lives in `es.json` and components read it through `t()`. Hardcoded literals are
    no longer accepted in new or changed code, and new tests do not assert on rendered
    Spanish text. "Never translate a Spanish literal the app renders" still holds for
    existing literals until they are moved to keys.
  - _Frontend_ ("validation stays in the service layer as thrown `Error`s with Spanish
    messages"): validation stays in the service layer, but new errors carry a translation
    key (and interpolation values) that the component resolves with `t()`, instead of a
    Spanish sentence. Existing thrown messages stay until touched.
  - The existing rule to keep every `data-testid` fits the new selector rule and stays.
- Linting: `eslint-plugin-i18next` (`no-literal-string`) set to error for new files and
  warning for existing ones, or an equivalent that fails CI when hardcoded strings
  increase. The exact mechanism is decided in the design.
- Migrate the components that already have keys (login and candidate profile sections), so
  the setup is used end to end and proven by tests.

## Out of scope

- A language switch, remembering the chosen language, or storing it on the user profile.
- Loading English as an active language, or completing `en.json`.
- Moving the remaining hardcoded copy to keys. It moves when a ticket touches the
  component (fix-as-you-touch), not in a dedicated sweep.
- Showing `nameEn` for catalogs, or translating export column headers.
- Translating backend error messages. New backend errors should prefer stable codes the
  frontend can map to keys, but existing messages stay as they are.
- Rewriting existing tests that match Spanish text.

## Personal-data impact

None. Only UI copy and the frontend build change. No candidate data is read, stored or
exposed differently.

## Acceptance criteria

- `npm run build` output contains the Spanish translations. The app renders identically to
  before, apart from the corrected accents.
- The migrated components render their copy through `t()`, and their unit tests pass.
- `es.json` has no values with missing accents.
- A new `.tsx` file with a hardcoded user-facing string fails lint. Existing files do not
  fail the build.
- `openspec/config.yaml` and `AGENTS.md` state the i18n rules for copy, keys and test
  selectors.
- The e2e suite passes unchanged.

## Open questions

- Lint mechanism: error scoped to new files by glob or per-file override, or a
  warning-count ratchet in CI.
- Whether fix-as-you-touch is required in review or only encouraged.
- Namespaces: a single flat file, or per-feature files once key count grows.
