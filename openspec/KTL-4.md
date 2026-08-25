## [original]

Reorganise navigation

You are an expert front-end developer. I need to refactor my application's navigation bar to make it fully responsive. Currently, all links are exposed. I want to group four specific links ("Catálogos", "Usuarios", "Roles", and "Importación") under a new parent element called "Admin".

Core Constraints

    The "Admin" parent element must not be a clickable link that routes to a page. It only acts as a toggle/trigger for the sub-navigation.

    Maintain the existing dark blue background for the main header and the exact active state styles (orange border/text highlights, like the current "Roles" button).

Desktop Requirements (min-width: [e.g., 768px])

    Display the links horizontally.

    Render "Dashboard", "Candidatos", "Búsqueda" as standard top-level links.

    Next to them, render an "Admin" button with a small downward chevron icon.

    When "Admin" is [hovered OR clicked], open a clean, styled dropdown menu directly below it containing the 4 sub-items.

    If one of the sub-items is the active route, the "Admin" parent in the top bar should ideally show a subtle active state indication.

Mobile Requirements (max-width: [e.g., 767px])

    Hide the horizontal links and display a Hamburger Menu icon on the right side of the header.

    Clicking the hamburger opens a vertical menu [Slide-out drawer OR full-width dropdown].

    "Dashboard", "Candidatos", and "Búsqueda" are standard vertical links.

    "Admin" is an accordion toggle button with a chevron icon.

    Tapping "Admin" expands the accordion, pushing the content below it down, and revealing the 4 sub-items.

    The sub-items should be slightly indented to show hierarchy.

## [enhanced]

# KTL-4 — Responsive primary navigation with an "Admin" group

## Context

The shell header in [app-layout.tsx](src/app/core/layout/app-layout.tsx) renders up to
seven sibling `NavLink`s in a single flat `<nav>`. Each one is gated by its own
permission (`view_candidates`, `manage_catalogs`, `manage_users`, `manage_roles`,
`import_candidates`), so an `rrhh_admin` sees all seven. The nav is styled in
[app-layout.css:47-70](src/app/core/layout/app-layout.css#L47-L70) with
`flex-wrap: wrap` — the only concession to narrow viewports today, and it wraps the
links into a second row that collides with the brand block and the `Salir` button.

There is no breakpoint, no menu, and no `.tsx` test covering the shell.

This change is **frontend-only**. It touches no Supabase table, view, RPC, RLS policy,
Edge Function or migration, and it introduces no new runtime dependency. Permission
gating stays exactly where it is: `usePermission()` in the component, RLS and the
`RequirePermission` route guard in the layers below.

## Decisions (agreed with product)

| Question                 | Decision                                                                                                                                                                             |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Desktop dropdown trigger | **Click only.** Toggles on click, closes on `Escape`, on outside click, and on navigation. No hover-open — it is the only behaviour that is identical for mouse, keyboard and touch. |
| Mobile menu shape        | **Full-width dropdown** below the header. No slide-out drawer, so no body scroll-lock, no focus trap, no animation library.                                                          |
| Breakpoint               | **768px.** `max-width: 767px` is mobile, `min-width: 768px` is desktop.                                                                                                              |

## User story

> Como persona usuaria de Kepler Talento, quiero una cabecera que agrupe las opciones
> de administración y que funcione en el móvil, para poder llegar a cualquier sección
> sin que los enlaces se amontonen ni se solapen con mi perfil.

## Scope

**In scope**

- Group `Catálogos`, `Usuarios`, `Roles` and `Importación` under a non-navigable
  `Admin` parent.
- Desktop (≥768px): horizontal bar with `Dashboard`, `Candidatos`, `Búsqueda` and the
  `Admin` dropdown trigger.
- Mobile (≤767px): hamburger button on the right of the header; full-width vertical
  menu; `Admin` as an accordion with indented children.
- Permission-aware rendering of the group itself.
- Keyboard and screen-reader support for both shapes.

**Out of scope**

- Route definitions, URLs and guards — unchanged.
- Permission names and the `Permission` model — unchanged.
- The brand block, the role `badge` and the `Salir` button — they keep their position
  and markup (`span.badge` is bound by Playwright).
- Any backend, database or storage change.

## Functional requirements

### Common

1. `Admin` is a `<button type="button">`, never an `<a>` or `NavLink`. It has no route
   and never changes the URL.
2. The `Admin` group renders **only if at least one** of `manage_catalogs`,
   `manage_users`, `manage_roles`, `import_candidates` is granted. Each sub-item keeps
   its own individual permission check, so a user with only `manage_roles` sees an
   `Admin` group containing a single `Roles` entry.
3. When the active route is one of the four admin routes, the `Admin` trigger shows the
   active treatment (orange text + orange left border, same tokens as the current
   `nav a.active`) and, on desktop, the group opens by default on first render of that
   route.
4. The header keeps `background: var(--fj-navy)`. No new colour values — everything
   comes from the tokens already in [styles.css](src/styles.css)
   (`--fj-navy`, `--fj-navy-90`, `--fj-orange-bright`, `--r-md`, `--font-display`).
5. Every menu closes on route change.

### Desktop (min-width: 768px)

6. `Dashboard`, `Candidatos`, `Búsqueda` render as top-level `NavLink`s, exactly as
   today.
7. The `Admin` trigger renders the label plus a chevron glyph (inline SVG,
   `aria-hidden="true"`), rotated 180° while open.
8. Clicking the trigger opens a dropdown panel positioned directly below it
   (`position: absolute`, `z-index` above `main` and below `.toasts`), containing the
   permitted sub-items as vertical `NavLink`s.
9. The panel closes on: second click on the trigger, `Escape` (focus returns to the
   trigger), a click outside the panel, or a navigation.
10. The hamburger button is not rendered at this width.

### Mobile (max-width: 767px)

11. The horizontal list is hidden; a hamburger `<button>` sits at the right of the
    header, after the role badge, with `aria-label="Abrir menú"` /
    `"Cerrar menú"` reflecting state.
12. Pressing it toggles a full-width vertical panel below the header that pushes the
    page content down (it participates in normal flow, not overlay).
13. `Dashboard`, `Candidatos`, `Búsqueda` are full-width vertical links.
14. `Admin` is an accordion trigger with a chevron; expanding it pushes the following
    items down and reveals the permitted sub-items indented (`padding-left` one step
    deeper than their siblings).
15. Tap targets are at least 44×44 px.

## Accessibility requirements

- `Admin` trigger: `aria-expanded={open}` and `aria-controls` pointing at the panel id.
- Hamburger: same pair, plus a Spanish `aria-label`.
- The `<nav aria-label="Navegación principal">` wrapper stays.
- The active route keeps `aria-current="page"` — React Router's `NavLink` already emits
  it; do not remove it when restyling.
- Focus order is DOM order: trigger → panel items. `Escape` closes and restores focus to
  the trigger. No focus trap is required for the full-width dropdown.
- The chevron is decorative (`aria-hidden`), so the accessible name is just the label.
- `prefers-reduced-motion: reduce` disables the chevron rotation and the accordion
  transition.
- Contrast of `#d8dee8` and `--fj-orange-bright` on `--fj-navy` must stay ≥ 4.5:1 (it
  already is; do not darken the panel background below `--fj-navy-90`).

## Target implementation

Follow the conventions in [AGENTS.md](AGENTS.md): React function components in `.tsx`,
plain co-located `.css` (**no CSS Modules** — Playwright binds `a.skip-link` and
`span.badge`), permissions through `usePermission()` hoisted to a `const` at the top of
the component (never inside a loop or callback), pure constants in a sibling `.ts` so
fast refresh keeps working.

```
src/app/core/layout/
  app-layout.tsx        modified — renders <PrimaryNav/> instead of the flat list
  app-layout.css        modified — header layout + the 768px breakpoint
  primary-nav.tsx       new — the whole nav: desktop bar, dropdown, hamburger, accordion
  primary-nav.css       new — dropdown, accordion, hamburger, indentation
  nav-items.ts          new — NavItem / NavGroup types + the declarative item table
```

`nav-items.ts` holds the shape and the data, no JSX:

```ts
export interface NavItem {
  label: string; // Spanish, with accents
  to: string;
  permission?: Permission; // undefined = always visible (Dashboard)
  testId: string;
}

export interface NavGroup {
  label: 'Admin';
  testId: 'nav-admin-trigger';
  items: NavItem[];
}
```

`primary-nav.tsx`:

- Calls `usePermission()` once per permission at the top level and builds the visible
  item lists with `useMemo`.
- Holds two pieces of `useState`: `adminOpen` and `mobileOpen`.
- Uses `useLocation()` to (a) derive `adminActive` from the four admin paths and
  (b) close both menus in a `useEffect` keyed on `pathname`.
- Uses a `useRef` on the desktop group plus a `pointerdown` listener on `document` for
  outside-click, registered only while `adminOpen` is true.
- Renders **one** DOM tree, not two. The desktop bar and the mobile panel are the same
  markup with different CSS; only the hamburger and the panel wrapper differ. Do not
  branch on `window.innerWidth` in JS — the breakpoint lives in CSS so SSR/first paint
  and Playwright viewport changes behave.

Every interactive element keeps a `data-testid` and, where it is a form control, its
`name=` attribute — decorative in React, load-bearing for Playwright.

Suggested test ids: `nav-dashboard`, `nav-candidates`, `nav-search`,
`nav-admin-trigger`, `nav-admin-panel`, `nav-catalogs`, `nav-users`, `nav-roles`,
`nav-import`, `nav-hamburger`, `nav-mobile-panel`.

## Data contract

**None.** No table, view, RPC, RLS policy, Edge Function, migration or seed changes.
No new call to the Supabase client in [core/supabase/](src/app/core/supabase/) or to any
service in [core/services/](src/app/core/services/). The only data read is the auth
profile already exposed through `authService.profile`.

## Acceptance criteria

| #     | Scenario                                                                   | Expected                                                                                                                                                                 |
| ----- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| AC-1  | An `rrhh_admin` opens the app at ≥768px                                    | The bar shows exactly `Dashboard`, `Candidatos`, `Búsqueda`, `Admin`. `Catálogos`, `Usuarios`, `Roles`, `Importación` are not in the document until the group is opened. |
| AC-2  | Clicks `Admin`                                                             | A panel opens below the trigger with the four sub-items; `aria-expanded="true"`.                                                                                         |
| AC-3  | Clicks `Admin` again, or presses `Escape`, or clicks elsewhere in the page | The panel closes, `aria-expanded="false"`; after `Escape`, focus is back on the trigger.                                                                                 |
| AC-4  | Clicks `Roles` in the panel                                                | Navigates to `/app/admin/roles`, the panel closes, and the `Admin` trigger shows the active treatment (orange text + orange left border).                                |
| AC-5  | `Admin` is inspected in the DOM                                            | It is a `<button type="button">` with no `href`; clicking it never changes `location.pathname`.                                                                          |
| AC-6  | A user with only `manage_roles` (no catalogs/users/import) signs in        | `Admin` is shown and contains only `Roles`.                                                                                                                              |
| AC-7  | A user with none of the four admin permissions signs in                    | No `Admin` trigger is rendered at all.                                                                                                                                   |
| AC-8  | Viewport is 390×844                                                        | The horizontal links are hidden, a hamburger button is visible at the right of the header, and the header stays a single row with no wrapping or overlap.                |
| AC-9  | Taps the hamburger                                                         | A full-width vertical panel opens below the header, containing `Dashboard`, `Candidatos`, `Búsqueda` and the `Admin` accordion trigger.                                  |
| AC-10 | Taps `Admin` in the mobile panel                                           | The accordion expands, the four sub-items appear indented below it, and the items that follow are pushed down (nothing is overlaid).                                     |
| AC-11 | Taps a link in the mobile panel                                            | Navigates and the whole panel closes.                                                                                                                                    |
| AC-12 | Tabs from the top of the page at any width                                 | The first stop is still `a.skip-link`, and `Enter` still moves focus to `#main-content`.                                                                                 |
| AC-13 | The header is inspected at any width                                       | Background is `var(--fj-navy)`; active links keep the current orange border/text rule; no hard-coded hex outside `styles.css`.                                           |
| AC-14 | `prefers-reduced-motion: reduce` is set                                    | No chevron rotation or accordion transition animates.                                                                                                                    |

## Test coverage

Existing e2e specs navigate with `page.goto(...)`, not by clicking the nav, so this
change should not break them — but the a11y spec asserts on the shell and must be
re-run.

**Unit / integration — Vitest + React Testing Library**

`tests/unit/primary-nav.spec.tsx` (new), rendering through `<ServicesProvider>` with an
`authService` double and a `MemoryRouter`:

- renders the three top-level links and the `Admin` trigger for a full-permission
  profile (AC-1);
- the `Admin` trigger is a `button` and has no `href` (AC-5);
- click toggles `aria-expanded` and mounts/unmounts the sub-items (AC-2, AC-3);
- `Escape` closes and returns focus to the trigger (AC-3) — use `userEvent`;
- with an initial entry of `/app/admin/roles`, the trigger carries the active class
  (AC-4);
- permission matrix: only `manage_roles` → one child (AC-6); none of the four → no
  trigger (AC-7);
- hamburger toggles `nav-mobile-panel` and the accordion toggles the sub-items
  (AC-9, AC-10) — jsdom has no media queries, so assert on presence/`aria-expanded`,
  not on visibility.

`tests/unit/nav-items.spec.ts` (new, optional): pure check that the item table maps each
label to the right route and permission.

**e2e — Playwright** (`tests/e2e/navigation-responsive.spec.ts`, new)

- Desktop project default viewport: AC-1 → AC-5, plus `expect(page).toHaveURL` unchanged
  after clicking `Admin`.
- A `test.describe` with `test.use({ viewport: { width: 390, height: 844 }, isMobile: true })`
  for AC-8 → AC-11. Prefer this over adding a project to
  [playwright.config.ts](playwright.config.ts) so CI cost stays flat; add a `mobile`
  project only if more mobile specs follow.
- Re-run [ux-accessibility.spec.ts](tests/e2e/ux-accessibility.spec.ts) for AC-12.

**Security** — nothing under [tests/security/](tests/security/) changes: no RLS policy,
no storage policy, no permission definition is touched. `npm run security:rls` and
`npm run security:storage` still run as the standing gate, but no new check is added.
Note for the reviewer: hiding a link is _not_ an authorization control — the four admin
routes stay protected by `RequirePermission` and by RLS, and that must be stated in the
change's `design.md` under principle 3.

**Commands to actually run**

```bash
npm test
npm run lint
npm run format:check
npm run e2e -- tests/e2e/navigation-responsive.spec.ts tests/e2e/ux-accessibility.spec.ts
```

## Documentation to update

- [AGENTS.md](AGENTS.md) — add the shell-navigation convention: new sections are
  declared in `core/layout/nav-items.ts`, not by adding a `NavLink` to the layout.
- [specs/001-gestion-cvs-rrhh/ux-audit.md](specs/001-gestion-cvs-rrhh/ux-audit.md) —
  record the responsive header and the 768px breakpoint.
- [README.md](README.md) — only if it shows a screenshot or describes the nav.
- No change to [SUPABASE_INTEGRATION_GUIDE.md](SUPABASE_INTEGRATION_GUIDE.md) — no
  backend surface is affected.

## Non-functional requirements

- **Security / privacy.** No candidate personal data is read or rendered by the nav.
  Menu visibility is a convenience only; authorization stays in `RequirePermission` and
  RLS (principles 1 and 3).
- **Performance.** No new dependency, no new network call. The outside-click listener is
  attached only while the dropdown is open and removed on cleanup. No layout thrash: the
  breakpoint is pure CSS.
- **Accessibility.** WCAG 2.1 AA: keyboard operable, `aria-expanded`/`aria-controls`,
  visible focus ring on trigger and items, contrast ≥ 4.5:1, reduced-motion respected,
  44 px touch targets.
- **Responsive.** Verified at 390, 768, 1024 and 1440 px wide. The header must never
  wrap into a second row and must never overlap the brand or the `Salir` button.
- **Copy.** All labels in Spanish with correct accents: `Catálogos`, `Búsqueda`,
  `Importación`, `Abrir menú`, `Cerrar menú`. `Admin` stays as-is (agreed in the brief).

## Risks

| Risk                                                                      | Mitigation                                                                                                                    |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| A future e2e spec clicks a nav link that is now hidden behind `Admin`     | Keep every `data-testid` stable and document the group in AGENTS.md.                                                          |
| The dropdown is clipped by `overflow` on the header                       | The header is `display: flex` with no `overflow` today; keep it that way and give the panel a `z-index` below `.toasts` (20). |
| The full-width mobile panel pushes `main` down and shifts scroll position | It is intended (AC-10); assert no overlay/scroll-lock is introduced.                                                          |
| Someone reads hidden links as an authorization improvement                | Stated explicitly in the change's `design.md` and in this ticket.                                                             |

## Next step

```
/opsx:new
```

with `openspec/KTL-4.md` as the change description.
