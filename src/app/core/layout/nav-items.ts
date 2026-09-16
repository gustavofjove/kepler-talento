import type { Permission } from '../../shared/models/auth.models';

/**
 * The shell's navigation declared as data rather than as JSX in the layout.
 *
 * Adding a section means adding an entry here - not another `NavLink` in
 * `app-layout.tsx`. Keeping this a plain `.ts` sibling (no JSX) is what lets
 * fast refresh keep working for `primary-nav.tsx`.
 */
export interface NavItem {
  /** Spanish label, accents included. Rendered verbatim. */
  label: string;
  to: string;
  /** `undefined` means always visible to an authenticated user. */
  permission?: Permission;
  /** Passed to `NavLink end`, so `/app` does not match every child route. */
  end?: boolean;
  testId: string;
}

export interface NavGroup {
  label: string;
  testId: string;
  panelId: string;
  items: NavItem[];
}

/** Top-level entries, shown horizontally on wide viewports. */
export const TOP_LEVEL_ITEMS: NavItem[] = [
  { label: 'Dashboard', to: '/app', end: true, testId: 'nav-dashboard' },
  {
    label: 'Candidatos',
    to: '/app/candidates',
    permission: 'candidates.read',
    testId: 'nav-candidates',
  },
  { label: 'Búsqueda', to: '/app/search', permission: 'candidates.read', testId: 'nav-search' },
];

/**
 * The administration group. Its parent is a disclosure button, never a link -
 * it has no `to` because it has no destination.
 */
export const ADMIN_GROUP: NavGroup = {
  label: 'Admin',
  testId: 'nav-admin-trigger',
  panelId: 'nav-admin-panel',
  items: [
    {
      label: 'Catálogos',
      to: '/app/catalogs',
      permission: 'catalogs.manage',
      testId: 'nav-catalogs',
    },
    {
      label: 'Presets',
      to: '/app/admin/presets',
      permission: 'presets.manage',
      testId: 'nav-presets',
    },
    {
      label: 'Usuarios',
      to: '/app/admin/users',
      permission: 'users.manage',
      testId: 'nav-users',
    },
    { label: 'Roles', to: '/app/admin/roles', permission: 'roles.manage', testId: 'nav-roles' },
    {
      label: 'Importación',
      to: '/app/admin/import',
      permission: 'candidates.import',
      testId: 'nav-import',
    },
  ],
};

/**
 * Every permission the navigation consults. `usePermission` is a hook, so the
 * component cannot call it while iterating the tables above - it calls it once
 * per entry here and filters against the resulting map.
 */
export const NAV_PERMISSIONS: Permission[] = [
  'candidates.read',
  'catalogs.manage',
  'presets.manage',
  'users.manage',
  'roles.manage',
  'candidates.import',
];

/** Single source for the active-state derivation of the group's parent. */
export const ADMIN_ROUTES: string[] = ADMIN_GROUP.items.map((item) => item.to);

/** True when `pathname` is one of the group's routes, or nested under one. */
export function isAdminRoute(pathname: string): boolean {
  return ADMIN_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`));
}

/** Filters a table down to the entries the given permission map allows. */
export function visibleItems(items: NavItem[], granted: Record<string, boolean>): NavItem[] {
  return items.filter((item) => !item.permission || granted[item.permission]);
}
