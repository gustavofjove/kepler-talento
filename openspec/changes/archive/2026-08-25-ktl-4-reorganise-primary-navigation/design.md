## Context

See `proposal.md` — Why. This section records only the constraints that shape the
approach.

- The shell is a single component, `src/app/core/layout/app-layout.tsx`. It renders the
  brand block, a flat `<nav aria-label="Navegación principal">` of `NavLink`s, the role
  badge and the sign-out button, then `<Outlet/>`, the toast region and `ConfirmDialog`.
- Styling is plain CSS in a co-located `app-layout.css`, every rule prefixed with `.shell`
  because Angular's component encapsulation is gone (KTL-3) and bare `header`/`nav a`
  selectors would leak. **CSS Modules are forbidden** — Playwright binds `a.skip-link` and
  `span.badge`.
- Permissions are read with `usePermission(...)`, a hook that subscribes to the auth
  signal. `AGENTS.md` forbids calling `authService.hasPermission()` directly in a
  component, and because `usePermission` is a hook it cannot be called in a loop or a
  callback — every result must be hoisted to a `const`.
- Design tokens live in `src/styles.css`: `--fj-navy`, `--fj-navy-90`,
  `--fj-orange-bright`, `--r-md`, `--font-display`. The toast region sits at `z-index: 20`.
- No routing, guard, permission or backend surface is in play. `RequirePermission` and RLS
  keep enforcing access regardless of what the menu shows.
- Playwright runs a single `chromium` project at the Desktop Chrome default viewport, and
  every existing e2e spec navigates with `page.goto(...)` rather than by clicking the nav.

## Goals / Non-Goals

**Goals:**

- One source of truth for navigation entries, so adding a section is a data edit.
- One DOM tree serving both viewport shapes, so behaviour cannot drift between them.
- Panel state that is impossible to leave stale across navigation or a resize.
- Keyboard and assistive-technology parity with pointer use, at both widths.

**Non-Goals:**

- No animation library, no headless-UI/menu library, no state-management library.
- No focus trap and no body scroll-lock — deliberately avoided by choosing a flow panel
  over an overlay drawer.
- No full ARIA `menu`/`menuitem` widget with roving tabindex and arrow-key navigation.
- No change to the brand block, role badge, sign-out button, toast region or skip link
  beyond the header's flex layout.
- No responsive treatment of any page _below_ the header; this change stops at the shell.

## Decisions

### D1 — One DOM tree, breakpoint in CSS only

Render a single navigation tree and let a `@media (min-width: 768px)` /
`(max-width: 767px)` pair decide which parts are shown and how the panel is positioned.

_Why:_ the alternative — branching in JS on `window.innerWidth` or a `matchMedia` hook —
duplicates the markup, doubles the state, and makes first paint depend on a measurement.
It also breaks under Playwright's `test.use({ viewport })`, which changes the viewport
without a resize sequence a naive hook would catch. Keeping the breakpoint in CSS means
the resize edge case in the spec ("crossing 768px with a panel open") is handled by
layout, not by an effect.

_Trade-off:_ in jsdom there are no media queries, so unit tests assert on presence and
`aria-expanded`, never on visibility. Visual/viewport behaviour is proven in Playwright
instead. This split is stated in the test strategy below so it is not mistaken for a gap.

_Alternative rejected:_ two components (`DesktopNav`, `MobileNav`) with a `useMediaQuery`
hook. Cleaner to read, but it duplicates the permission logic and the panel state, and
introduces the exact drift this design is trying to prevent.

### D2 — Entries as data in a sibling `.ts`, not JSX in the layout

`src/app/core/layout/nav-items.ts` exports the entry/group types and the table:

```ts
export interface NavItem {
  label: string; // Spanish, accented
  to: string;
  permission?: Permission; // undefined = always visible (Dashboard)
  testId: string;
}
export interface NavGroup {
  label: string;
  testId: string;
  items: NavItem[];
}
```

_Why:_ `AGENTS.md` requires pure helpers and constants to live in a sibling `.ts` so fast
refresh keeps working for the `.tsx`. It also makes the permission filter a single
`useMemo` over a table rather than seven hand-written conditionals, and gives the unit
test something to assert against directly.

_Constraint this creates:_ `usePermission` is a hook and cannot be called while iterating
the table. So the component calls it once per permission at the top level, collects the
results into a `Record<Permission, boolean>` map, and the `useMemo` filters the table
against that map. The permission list is therefore fixed at authoring time — which is
correct, since routes and permissions are both static.

### D3 — Click to open, never hover

Confirmed with product (see `openspec/KTL-4.md` — Decisions). The group opens on click,
`Escape`, outside click and navigation close it.

_Why:_ hover-open needs a close delay, a mouse-leave grace area, and a duplicated
keyboard path; touch devices then fire a synthetic hover that opens the panel on the tap
that was meant to activate it. Click-only is one code path for pointer, keyboard and
touch, which is what makes the accessibility requirement cheap rather than a retrofit.

### D4 — Disclosure semantics, not a `menu` widget

The `Admin` parent is `<button type="button" aria-expanded aria-controls>`, and the panel
is a plain container of `NavLink`s. No `role="menu"`, no `role="menuitem"`, no roving
tabindex, no arrow-key handling.

_Why:_ ARIA menus are for application command menus and impose a full keyboard contract
(arrows move, Tab exits, Home/End) that users do not expect from site navigation, and
that we would have to implement and test correctly at both widths. A disclosure button
revealing links is the pattern navigation actually is, and it gets the correct
announcement from `aria-expanded` alone. The same markup serves the desktop dropdown and
the mobile accordion — only CSS positioning differs, which is what makes D1 possible.

_Consequence:_ no focus trap is needed; DOM order is the focus order. `Escape` still
closes and restores focus to the trigger because the spec requires it, not because the
disclosure pattern demands it.

### D5 — Panel state lives in the component, derived state does not

Two `useState` flags: `adminOpen`, `mobileOpen`.

- `adminActive` is **derived** from `useLocation().pathname` against the group's routes
  with `useMemo` — never stored, so it cannot go stale.
- Both flags are reset in a `useEffect` keyed on `pathname`, which satisfies "navigating
  closes every panel" for every navigation source, including browser back/forward. The
  effect **skips its mount run** (a `useRef` latch): landing on an admin route must leave
  the group open, while navigating to one from the open group must close it. These are two
  different moments and an effect that recomputes `adminOpen` from the route on every run
  conflates them — it re-opens the dropdown on the click that was meant to dismiss it,
  which contradicts the spec's "navigating closes every panel" and the ticket's AC-4.
  Found while running the unit spec; recorded here so the latch is not mistaken for
  incidental complexity and removed.
- The initial value of `adminOpen` is `adminActive`, so landing on an admin route renders
  with the group already open (spec: "Group opens on an administration route") without an
  effect that would flash closed-then-open.
- Outside-click uses a `useRef` on the desktop group plus a `pointerdown` listener on
  `document`, registered in an effect **only while `adminOpen` is true** and removed on
  cleanup. `pointerdown` rather than `click` so the panel closes before a click on an
  underlying control resolves.

_Alternative rejected:_ the native `<details>`/`<summary>` element. It gives free
disclosure state and keyboard support, but its open state cannot be driven from the route
without fighting the element, `Escape` is not handled, and styling the marker
cross-browser costs more than the 15 lines of state it saves.

### D6 — Active treatment reuses the existing rule

`.shell nav a.active` (orange text, orange left border, `--fj-navy-90` background) is the
established treatment. The `Admin` trigger gets the same declarations via a shared class
rather than a copy of the values, so the two can never diverge.

_Note:_ the border is on the **left** edge, a carry-over from the Angular sidebar era. It
looks unusual on a horizontal bar, but the brief asks explicitly to keep "the exact active
state styles, like the current Roles button". Keeping it is the instruction; changing it
would be a separate design decision.

### D7 — Mobile panel in flow, not an overlay

The narrow-viewport panel is a normal block below the header that displaces `<main>`.

_Why:_ the brief asks for the accordion to push content down, and product chose the
full-width dropdown over a drawer. In flow, there is no overlay, no `position: fixed`, no
body scroll-lock, and no focus trap — three sources of accessibility bugs removed by a
layout choice rather than mitigated by code.

_Trade-off:_ opening the menu shifts the page content down and can move the user's scroll
position. Accepted, and asserted as intended behaviour in the spec rather than treated as
a defect.

### D8 — Stacking

The desktop panel is `position: absolute` under a `position: relative` group wrapper, at a
`z-index` above `main` and below the toast region's `20`. The header must not gain
`overflow: hidden` — a note to that effect goes in the CSS, since adding it would clip the
panel and the failure would look like a positioning bug.

## Authorization model

Unchanged, and deliberately so.

- The navigation reads permissions only to decide **visibility**. It performs no
  authorization.
- Enforcement stays where it already is: `RequirePermission` as a layout-route element for
  each admin route, and RLS in Postgres for the data behind them (principle 3).
- A user who types an admin URL directly is refused by the guard and by RLS, exactly as
  before this change. The spec carries a scenario asserting that refusal does not depend
  on the link being hidden, so no future reader can mistake the menu for a control.
- No new permission, role, policy or service-role path is introduced. Nothing under
  `tests/security/` changes.

## Data model / storage impact

None. No table, view, RPC, RLS policy, storage policy, Edge Function, migration or seed is
touched, and no new Supabase client call is added. No candidate personal data is read or
rendered by the navigation (principle 1); the only profile data consulted is the
permission set already held in the auth signal.

## Test strategy

Split along D1's seam — logic and semantics in Vitest, viewport and layout in Playwright.

**Vitest + React Testing Library** — `tests/unit/primary-nav.spec.tsx`, rendered inside
`<ServicesProvider>` with an `authService` double and a `MemoryRouter`:

- entry set per permission profile, including the one-child and zero-child group cases;
- the parent is a `button` with no `href`, and activating it leaves the location unchanged;
- `aria-expanded` toggling and children mounting/unmounting;
- `Escape` closes and returns focus to the trigger (`userEvent`, not `fireEvent`);
- initial entry `/app/admin/roles` → parent already active and group already open;
- hamburger and accordion toggling asserted on presence and `aria-expanded` **only** —
  jsdom has no media queries, so visibility is not assertable here by design.

Optionally `tests/unit/nav-items.spec.ts` for the table → route/permission mapping.

**Playwright** — `tests/e2e/navigation-responsive.spec.ts`:

- at the default desktop viewport: grouping, opening, dismissal, `toHaveURL` unchanged
  after activating the parent, active treatment after navigating to a child;
- a `test.describe` with `test.use({ viewport: { width: 390, height: 844 }, isMobile: true })`
  for the hamburger, the vertical panel, the accordion and the single-row header. A
  `mobile` project is **not** added to `playwright.config.ts` — one spec does not justify
  doubling CI runtime; add the project when a second mobile spec appears.
- re-run `tests/e2e/ux-accessibility.spec.ts`, which asserts the skip link is the first
  focusable element of the shell.

**Security** — `npm run security:rls` and `npm run security:storage` run as the standing
gate and must stay green; no new check is added, because no boundary moved.

## Risks / Trade-offs

- **A future e2e spec clicks a link now hidden behind `Admin`** → every `data-testid` is
  kept stable and the grouping is documented in `AGENTS.md`; existing specs navigate with
  `page.goto` and are unaffected.
- **`overflow: hidden` added to the header later clips the dropdown** → a comment in
  `app-layout.css` at the header rule states the constraint, and the Playwright desktop
  spec asserts the panel is visible, so the regression fails a test rather than shipping.
- **jsdom cannot see the breakpoint** → accepted and compensated by the Playwright mobile
  block; the split is recorded here so the unit suite's silence about layout is understood
  as scope, not oversight.
- **Opening the mobile menu shifts scroll position** → intended (D7), asserted as such.
- **Hiding links reads as a security improvement** → stated explicitly in the
  authorization model above and in a spec scenario.
- **Four admin sections now cost one extra interaction** → accepted; they are
  configuration surfaces used occasionally, and the daily-use entries stay top level.

## Migration Plan

Not applicable. Pure frontend change, no data migration, no feature flag. Rollback is
reverting the commit; no state is written anywhere that a revert would strand.

## Open Questions

None. The three placeholders in the brief (trigger, mobile shape, breakpoint) were
resolved with product and are recorded in `openspec/KTL-4.md` — Decisions.

## Departure record

No departure from the standing principles. The change adds no dependency (principle 2),
moves no authorization out of the database (principle 3), reads no candidate personal data
(principle 1), and ships with user-journey and security evidence (principle 4).
