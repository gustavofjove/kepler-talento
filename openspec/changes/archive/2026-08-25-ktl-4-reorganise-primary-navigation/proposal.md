## Why

The application shell renders up to seven permission-gated links as flat siblings in a
single `<nav>` (`src/app/core/layout/app-layout.tsx`). The only concession to narrow
viewports is `flex-wrap: wrap`, so on a phone the links wrap into a second row that
collides with the brand block and the `Salir` button, and on a desktop an `rrhh_admin`
sees seven equally-weighted options with no sense of which are day-to-day work and which
are administration. Kepler Talento is used from tablets and phones during recruitment
sessions, so an unusable header on small screens is a daily friction, not a cosmetic one.

## What Changes

- Group the four administration links — `Catálogos`, `Usuarios`, `Roles`,
  `Importación` — under a new parent element labelled `Admin`.
- `Admin` is a `<button type="button">`, not a link. It has no route and never changes
  the URL; it only opens and closes the group.
- The `Admin` group renders only when the signed-in profile holds at least one of
  `manage_catalogs`, `manage_users`, `manage_roles`, `import_candidates`. Each child keeps
  its own individual permission check, so the group can legitimately contain a single item.
- Introduce a 768px breakpoint:
  - **≥768px**: horizontal bar with `Dashboard`, `Candidatos`, `Búsqueda` plus an `Admin`
    trigger carrying a chevron; clicking it opens a dropdown panel directly beneath it.
  - **≤767px**: the horizontal list is hidden and a hamburger button appears at the right
    of the header; pressing it opens a full-width vertical panel below the header, in which
    `Admin` behaves as an accordion whose children are indented.
- The dropdown and the mobile panel close on `Escape`, on outside click, and on navigation.
  `Escape` returns focus to the trigger that opened the panel.
- When the active route is one of the four administration routes, the `Admin` trigger
  carries the same active treatment as any active link today (orange text, orange left
  border), and the desktop group is open on first render of that route.
- Move the declaration of navigation entries out of the layout component into a
  data table (`nav-items.ts`), so a future section is added by editing data rather than
  by adding another `NavLink` to the shell.
- **No BREAKING changes.** Routes, URLs, guards, permission names and the `Permission`
  model are untouched. Every existing link remains reachable; four of them now live one
  interaction deeper.

## Capabilities

### New Capabilities

- `primary-navigation`: how the application shell exposes the available sections to the
  signed-in user — which entries are visible for which permissions, how administration
  entries are grouped, and how the navigation behaves and stays operable across viewport
  widths and input methods (pointer, keyboard, touch).

### Modified Capabilities

<!-- None. openspec/specs/ holds no capability specs yet; this change introduces the
     first one rather than modifying an existing one. -->

## Impact

**Actors**: every authenticated user of Kepler Talento. The visible effect scales with
permissions — a user with no administration permission sees no `Admin` group at all.

**Personal data, RLS, storage, roles** — this change touches **none** of them:

- No candidate personal data is read or rendered by the navigation. The only profile data
  it consults is the role/permission set already exposed through `authService.profile`
  (principle 1 unaffected).
- No table, view, RPC, RLS policy, storage policy, Edge Function, migration or seed
  changes. There is no new call to the Supabase client or to any service under
  `src/app/core/services/`.
- Menu visibility is a convenience, **not** an authorization control. The four
  administration routes stay protected by the `RequirePermission` layout-route guard and
  by RLS in the database. Hiding a link neither adds nor removes a security boundary, and
  the security posture after this change is byte-for-byte the one before it
  (principle 3 upheld by leaving the enforcing layers alone).

**Code**

- `src/app/core/layout/app-layout.tsx` — delegates the nav to a new component.
- `src/app/core/layout/app-layout.css` — header layout and the 768px breakpoint.
- `src/app/core/layout/primary-nav.tsx`, `primary-nav.css`, `nav-items.ts` — new.

**Dependencies**: none added. React 19, React Router 7 and the existing token set in
`src/styles.css` cover everything required.

**Tests**: new Vitest + React Testing Library spec for the navigation component, new
Playwright spec covering desktop and a 390×844 mobile viewport, and a re-run of
`tests/e2e/ux-accessibility.spec.ts`, which asserts on the shell's skip-link. No change
under `tests/security/`.

**Documentation**: `AGENTS.md` (how a new section is registered) and
`specs/001-gestion-cvs-rrhh/ux-audit.md` (the responsive header and its breakpoint).

## Assumptions

- `Admin` stays in that form as the label — it is the wording the business asked for, and
  it is understood in Spanish. Every other label keeps its current Spanish spelling and
  accents.
- The header keeps the brand block on the left and the role badge plus `Salir` on the
  right at every width; only the navigation between them is restructured.
- Desktop opens the group on click, not on hover, so pointer, keyboard and touch share one
  behaviour.

## Edge cases

- A user holding exactly one administration permission: the group renders with one child.
- A user holding none: no `Admin` trigger in the document at all.
- A user without `view_candidates`: `Candidatos` and `Búsqueda` stay hidden as today, and
  the bar may contain only `Dashboard` plus possibly `Admin`.
- Landing directly on `/app/admin/roles` via URL or refresh: the trigger must already show
  the active treatment before any interaction.
- The viewport crossing 768px while a panel is open (rotation, window resize): the layout
  must not strand an open panel in an unreachable state.
- `prefers-reduced-motion: reduce`: no chevron rotation, no accordion transition.

## Success criteria

- At 390px wide, the header occupies a single row with no wrapping and no overlap between
  the brand, the navigation control and the `Salir` button — verified in Playwright at
  390, 768, 1024 and 1440px.
- The number of top-level navigation controls visible to an `rrhh_admin` on desktop drops
  from 7 to 4.
- Every one of the seven destinations remains reachable, in at most two interactions.
- The full keyboard path (Tab to the trigger, Enter to open, Tab to a child, Enter to
  navigate, Escape to close and restore focus) works at both widths.
- `npm test`, `npm run lint`, `npm run format:check` and the two Playwright specs pass;
  `npm run security:rls` and `npm run security:storage` still pass unchanged.
