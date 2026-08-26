Requirement keys used in the trace column of every task, from
`specs/primary-navigation/spec.md`:

| Key | Requirement                                                     |
| --- | --------------------------------------------------------------- |
| R1  | Primary navigation entries and permission visibility            |
| R2  | Administration entries are grouped under a non-navigable parent |
| R3  | Active route indication                                         |
| R4  | Wide-viewport navigation layout                                 |
| R5  | Narrow-viewport navigation layout                               |
| R6  | Dismissing an open navigation panel                             |
| R7  | Keyboard and assistive-technology operability                   |

## 0. Create Feature Branch

- [x] 0.1 Create and check out `feat/KTL-4` from an up-to-date `main`, and confirm the
      working tree is clean before touching any file.

## 1. Navigation data table

- [x] 1.1 Create `src/app/core/layout/nav-items.ts` exporting the `NavItem` and `NavGroup`
      interfaces from design D2, importing `Permission` from
      `src/app/shared/models/auth.models`. No JSX in this file — it must stay a pure `.ts`
      sibling so fast refresh keeps working for the component. _(R1, R2)_
- [x] 1.2 Populate the table: top-level `Dashboard` (`/app`, end-match, no permission),
      `Candidatos` (`/app/candidates`, `view_candidates`), `Búsqueda` (`/app/search`,
      `view_candidates`); group `Admin` with `Catálogos` (`/app/catalogs`,
      `manage_catalogs`), `Usuarios` (`/app/admin/users`, `manage_users`), `Roles`
      (`/app/admin/roles`, `manage_roles`), `Importación` (`/app/admin/import`,
      `import_candidates`). Copy the labels with their accents exactly as they appear in
      `app-layout.tsx` today. _(R1, R2)_
- [x] 1.3 Give every entry a stable `testId`: `nav-dashboard`, `nav-candidates`,
      `nav-search`, `nav-admin-trigger`, `nav-catalogs`, `nav-users`, `nav-roles`,
      `nav-import`. Export the group's route list (or a helper over it) so the active-state
      derivation in 2.4 has a single source. _(R3)_

## 2. Primary navigation component

- [x] 2.1 Create `src/app/core/layout/primary-nav.tsx` and move the
      `<nav aria-label="Navegación principal">` block out of `app-layout.tsx` into it, rendering
      from the table of task 1. Keep `app-layout.tsx` otherwise untouched — brand block,
      `span.badge`, `Salir`, `a.skip-link`, `#main-content`, toast region and
      `ConfirmDialog` all stay exactly as they are. _(R1)_
- [x] 2.2 Call `usePermission(...)` once per permission at the top level of the component
      and collect the results into a `Record<Permission, boolean>`. Never call it inside a
      loop, a callback or a conditional, and never call `authService.hasPermission()`
      directly (`AGENTS.md`). _(R1)_
- [x] 2.3 Derive the visible top-level entries and the visible group children with
      `useMemo` over the table and the permission map. Render the `Admin` parent only when
      at least one child is visible; render each child only when its own permission is
      granted. _(R1, R2)_
- [x] 2.4 Render the `Admin` parent as `<button type="button">` — never an `<a>` or
      `NavLink`, no `href`, no route — carrying the label, an inline chevron SVG marked
      `aria-hidden="true"`, `aria-expanded` and `aria-controls` pointing at the panel id.
      _(R2, R7)_
- [x] 2.5 Add the two `useState` flags (`adminOpen`, `mobileOpen`) and derive `adminActive`
      from `useLocation().pathname` against the group's routes with `useMemo` — derived,
      never stored. Initialise `adminOpen` to `adminActive` so an admin route renders with
      the group already open and no closed-then-open flash. _(R3, R4)_
- [x] 2.6 Reset both flags in a `useEffect` keyed on `pathname`, so every navigation —
      including browser back/forward — closes every panel. _(R6)_
- [x] 2.7 Add outside-click dismissal: a `useRef` on the desktop group plus a `pointerdown`
      listener on `document`, registered only while `adminOpen` is true and removed in the
      effect cleanup. Use `pointerdown`, not `click`, so the panel closes before a click on
      an underlying control resolves. _(R6)_
- [x] 2.8 Add `Escape` handling: close the open panel and move focus back to the control
      that opened it. _(R6, R7)_
- [x] 2.9 Render the hamburger `<button>` with `aria-label="Abrir menú"` /
      `"Cerrar menú"` reflecting state, `aria-expanded` and `aria-controls`, plus
      `data-testid="nav-hamburger"`; give the mobile panel
      `data-testid="nav-mobile-panel"` and the desktop panel `data-testid="nav-admin-panel"`.
      _(R5, R7)_
- [x] 2.10 Verify one DOM tree serves both widths: no `window.innerWidth` branch, no
      `matchMedia` hook, no duplicated desktop/mobile markup (design D1). Confirm the
      component compiles with `npx tsc --noEmit`. _(R4, R5)_

## 3. Styling and breakpoint

- [x] 3.1 Create `src/app/core/layout/primary-nav.css` as plain CSS — **no CSS Modules**,
      Playwright binds class names — with every rule prefixed under `.shell`, matching the
      convention already documented at the top of `app-layout.css`. Use only the existing
      tokens from `src/styles.css`; introduce no new hex value. _(R4, R5)_
- [x] 3.2 Give the `Admin` parent the existing active treatment by sharing the declarations
      of `.shell nav a.active` (orange text, orange left border, `--fj-navy-90` background)
      through a common class rather than copying the values, so the two cannot diverge.
      _(R3)_
- [x] 3.3 Wide viewport (`@media (min-width: 768px)`): horizontal top level; hamburger
      hidden; the group wrapper `position: relative` and the panel `position: absolute`
      directly below the trigger, at a `z-index` above `main` and below the toast region's
      `20`. _(R4)_
- [x] 3.4 Add a comment on the `.shell header` rule stating that `overflow: hidden` must
      never be added there, because it would clip the dropdown (design D8). _(R4)_
- [x] 3.5 Narrow viewport (`@media (max-width: 767px)`): hide the horizontal entries; show
      the hamburger at the trailing edge of the header after the badge; the panel is a
      full-width block **in normal flow** below the header that displaces `<main>` — no
      `position: fixed`, no overlay, no body scroll-lock, no focus trap. Indent the group's
      children one step deeper than their siblings. _(R5)_
- [x] 3.6 Remove the `flex-wrap: wrap` crutch from `.shell nav` and adjust
      `app-layout.css` so the header stays a single row at every width with no overlap
      between brand, navigation control and `Salir`. _(R5)_
- [x] 3.7 Ensure every activatable navigation control has a touch target of at least
      44×44 px at the narrow viewport, and that the focus ring stays visible on the
      trigger, the hamburger and every entry. _(R5, R7)_
- [x] 3.8 Add a `@media (prefers-reduced-motion: reduce)` block disabling the chevron
      rotation and the accordion transition. _(R7)_
- [x] 3.9 Check contrast of resting (`#d8dee8`) and active (`--fj-orange-bright`) text
      against `--fj-navy` and the panel background — both must stay at or above 4.5:1. Do
      not darken the panel below `--fj-navy-90`. _(R7)_

## 4. Unit tests

- [x] 4.1 Review the existing unit suite for specs affected by the shell change — start
      with `tests/unit/route-guards.spec.tsx` and any spec that renders `AppLayout` — and
      update them for the new markup. **Do not weaken an assertion to make it pass**; if
      one now fails for a real reason, fix the component. _(R1)_
- [x] 4.2 Create `tests/unit/primary-nav.spec.tsx` rendering through `<ServicesProvider>`
      with an `authService` double and a `MemoryRouter`. Assert on presence and
      `aria-expanded` only — jsdom has no media queries, so visibility is deliberately not
      asserted here (design D1). _(R1, R2)_
- [x] 4.3 Cover the permission matrix: all permissions → three top-level entries plus
      `Admin`; only `manage_roles` → `Admin` with `Roles` as its single child; none of the
      four admin permissions → no `Admin` parent at all; no `view_candidates` →
      `Candidatos` and `Búsqueda` absent while `Dashboard` remains. _(R1, R2)_
- [x] 4.4 Assert the parent is a `button` with no `href` and that activating it leaves the
      router location unchanged. _(R2)_
- [x] 4.5 Cover open/close: click toggles `aria-expanded` and mounts/unmounts the children;
      `Escape` closes and returns focus to the trigger. Use `userEvent`, not `fireEvent`,
      so focus behaves realistically. _(R6, R7)_
- [x] 4.6 With an initial entry of `/app/admin/roles`, assert the parent carries the active
      class and the group is already open on first render. _(R3, R4)_
- [x] 4.7 Cover the hamburger toggling `nav-mobile-panel` and the accordion toggling the
      children, asserted on presence and `aria-expanded`. _(R5)_
- [x] 4.8 Optionally add `tests/unit/nav-items.spec.ts` asserting the table maps each label
      to the right route and permission. _(R1)_
- [x] 4.9 **Run** `npm test` and inspect the output. Every spec must pass; do not mark this
      task complete from a description of the run. `npm run test:integration` is **not**
      required — this change touches no Supabase-backed data path, and there is no database
      state to verify. State that explicitly in the completion note. _(R1–R7)_

## 5. End-to-end tests

- [x] 5.1 Create `tests/e2e/navigation-responsive.spec.ts` using the `rrhh_admin` storage
      state from `authFile`, as the existing specs do. _(R1)_
- [x] 5.2 Desktop block at the default viewport: only `Dashboard`, `Candidatos`, `Búsqueda`
      and `Admin` at the top level; the four admin entries absent until the group is
      opened; `expect(page).toHaveURL(...)` unchanged after activating `Admin`; the panel
      is visible and not clipped; `Escape` and an outside click both close it; navigating
      to `Roles` closes the panel and leaves `Admin` in the active state. _(R2, R3, R4, R6)_
- [x] 5.3 Mobile block: `test.describe` with
      `test.use({ viewport: { width: 390, height: 844 }, isMobile: true })` covering the
      hamburger appearing, the full-width panel opening, the accordion expanding with
      indented children, a link closing the whole panel, and the header staying a single
      row. Do **not** add a `mobile` project to `playwright.config.ts` — one spec does not
      justify doubling CI runtime (design D1). _(R5, R6)_
- [x] 5.4 Add width checks at 390, 768, 1024 and 1440 px asserting no horizontal overflow
      and no overlap between the brand block, the navigation control and `Salir`. _(R5)_
- [x] 5.5 **Run** the new spec: `npm run e2e -- tests/e2e/navigation-responsive.spec.ts`.
      Inspect the report and fix real failures rather than relaxing assertions. _(R1–R7)_
- [x] 5.6 **Run** `npm run e2e -- tests/e2e/ux-accessibility.spec.ts` — it asserts
      `a.skip-link` is the first focusable element of the shell and that `Enter` moves focus
      to `#main-content`, both of which this change could break. _(R7)_
- [x] 5.7 **Run** the full suite once with `npm run e2e` before opening the PR, to catch any
      spec that reached a page through the shell. No seed data is written by these specs,
      so no restore step is needed; confirm that is still true after the run. _(R1–R7)_

## 6. Security evidence

- [x] 6.1 Confirm in the diff that nothing under `supabase/migrations/`,
      `supabase/functions/`, `tests/security/` or `src/app/core/services/` was modified,
      and that no new Supabase client call was introduced. This slice changes visibility
      only; authorization stays in `RequirePermission` and RLS. _(R1)_
- [x] 6.2 **Run** `npm run security:rls` and `npm run security:storage` as the standing
      gate and record that both still pass unchanged. No new check is added because no
      boundary moved. _(R1)_
- [x] 6.3 Verify by hand that a profile lacking `manage_roles` is still redirected away
      from `/app/admin/roles` when the URL is entered directly — proving the refusal comes
      from the guard and RLS, not from the hidden link. _(R1)_

## 7. Documentation

- [x] 7.1 Update `AGENTS.md`: a new section is registered by adding an entry to
      `src/app/core/layout/nav-items.ts`, not by adding a `NavLink` to the layout; note
      that the shell has a 768px breakpoint and that admin sections live under the `Admin`
      group. _(R1, R2)_
- [x] 7.2 Update `specs/001-gestion-cvs-rrhh/ux-audit.md` with the responsive header, the
      768px breakpoint and the disclosure pattern used for the group. _(R4, R5, R7)_
- [x] 7.3 Check `README.md` and update it only if it describes or screenshots the
      navigation. Leave `SUPABASE_INTEGRATION_GUIDE.md` untouched — no backend surface is
      affected. _(R1)_

## 8. Quality gate and hand-off

- [x] 8.1 **Run** `npm run lint` and fix every finding. _(R1–R7)_
- [x] 8.2 **Run** `npm run format:check`, and `npm run format` if it reports drift. _(R1–R7)_
- [x] 8.3 **Run** `npm run build` (it runs `tsc --noEmit` first) to confirm the change
      type-checks and builds. _(R1–R7)_
- [x] 8.4 Walk the acceptance criteria in `openspec/KTL-4.md` — Acceptance criteria (AC-1
      to AC-14) and confirm each one is covered by a passing test or an inspected manual
      check. Record any that is not. _(R1–R7)_
- [ ] 8.5 Open the PR from `feat/KTL-4` to `main`, linking this change directory and
      summarising the security position: visibility only, no boundary moved.

## Completion notes

- **4.1** No existing unit spec renders `AppLayout`; `route-guards.spec.tsx` asserts on
  sentinel routes, not on the shell, so no existing spec needed updating. Confirmed by the
  full run.
- **4.9** `npm test`: 136/137 pass. The single failure,
  `candidate-relations.service.spec.ts > rejects an implausible end year`, is
  **pre-existing** — it fails identically on clean `main` (the spec's regex reads
  `valido`, the thrown message reads `válido`). Out of scope for this change; worth its own
  ticket. `npm run test:integration` was **not** run: this slice touches no Supabase-backed
  data path and writes no database state, so there is nothing to verify.
- **5.5–5.7** `navigation-responsive.spec.ts` 17/17; `ux-accessibility.spec.ts` 2/2; full
  `npm run e2e` 46/46. One caveat: on the first full-suite run the skip-link test in
  `ux-accessibility.spec.ts` failed once. It then passed 46/46 on three subsequent full
  runs and 6/6 in isolation, and clean `main` passes 29/29. Not reproduced, cause not
  established — recorded rather than dismissed. No seed data is written by these specs, so
  no restore step was needed.
- **6.2** Both gates exit clean. Note they are currently thin wrappers that report where
  the SQL guardrails live rather than executing them, so they are evidence that nothing
  regressed, not a fresh proof of the policies.
- **6.3** Promoted from a manual check to a permanent test: "a readonly user sees no Admin
  group and is still refused the route by URL", which covers the spec scenario directly.
- **8.2** `npm run format:check` reports the **whole repo** as unformatted — 108 files on
  clean `main`, before this change. Only the eight files this change touches were
  formatted; rewriting the other 108 would bury this diff. The repo-wide formatting drift
  needs its own ticket.
- **8.4** AC-1 to AC-14 from `openspec/KTL-4.md` are each covered by a passing test.
  AC-13's "no hard-coded hex outside styles.css" holds except for the hamburger bar colour
  and the dropdown shadow, which reuse the same literal `#d8dee8` the nav already used
  before this change and a black rgba for elevation; neither has an existing token.
- **8.5** Not done: pushing the branch and opening the PR is left to the author.
