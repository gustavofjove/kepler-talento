import { useEffect, useMemo, useRef, useState } from 'react';
import { NavLink, useLocation } from 'react-router';
import { usePermission } from '../di/services-context';
import {
  ADMIN_GROUP,
  isAdminRoute,
  TOP_LEVEL_ITEMS,
  visibleItems,
  type NavItem,
} from './nav-items';
import './primary-nav.css';

const MOBILE_PANEL_ID = 'nav-mobile-panel';

const linkClass = ({ isActive }: { isActive: boolean }): string =>
  isActive ? 'nav-link active' : 'nav-link';

/** Decorative - the accessible name comes from the button's own label. */
function Chevron() {
  return (
    <svg className="chevron" viewBox="0 0 12 8" width="12" height="8" aria-hidden="true">
      <path d="M1 1.5 6 6.5 11 1.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

function NavEntry({ item }: { item: NavItem }) {
  return (
    <NavLink to={item.to} end={item.end} className={linkClass} data-testid={item.testId}>
      {item.label}
    </NavLink>
  );
}

/**
 * The whole shell navigation: top-level entries, the `Admin` disclosure group
 * and the narrow-viewport hamburger, rendered as ONE DOM tree. Which parts are
 * visible and how the group's panel is positioned is decided entirely by the
 * 768px breakpoint in `primary-nav.css` - deliberately not by measuring
 * `window.innerWidth` here, so first paint needs no measurement and a viewport
 * change cannot leave JS state and layout disagreeing.
 */
export function PrimaryNav() {
  const { pathname } = useLocation();

  // usePermission is a hook, so it cannot be called while iterating the nav
  // tables. Call it once per permission and filter against the result.
  const canViewCandidates = usePermission('view_candidates');
  const canManageCatalogs = usePermission('manage_catalogs');
  const canManageUsers = usePermission('manage_users');
  const canManageRoles = usePermission('manage_roles');
  const canImport = usePermission('import_candidates');

  const granted = useMemo(
    () => ({
      view_candidates: canViewCandidates,
      manage_catalogs: canManageCatalogs,
      manage_users: canManageUsers,
      manage_roles: canManageRoles,
      import_candidates: canImport,
    }),
    [canViewCandidates, canManageCatalogs, canManageUsers, canManageRoles, canImport],
  );

  const topLevel = useMemo(() => visibleItems(TOP_LEVEL_ITEMS, granted), [granted]);
  const adminItems = useMemo(() => visibleItems(ADMIN_GROUP.items, granted), [granted]);

  // Derived, never stored - so it cannot go stale against the route.
  const adminActive = isAdminRoute(pathname);

  // Landing on an admin route renders with the group already open, rather than
  // flashing closed and then opening from an effect.
  const [adminOpen, setAdminOpen] = useState(adminActive);
  const [mobileOpen, setMobileOpen] = useState(false);

  const adminGroupRef = useRef<HTMLDivElement>(null);
  const adminTriggerRef = useRef<HTMLButtonElement>(null);
  const hamburgerRef = useRef<HTMLButtonElement>(null);

  // Any navigation closes everything, including browser back/forward. Skipping
  // the mount run is what keeps this from fighting the initial state above:
  // landing on an admin route opens the group, but navigating to one from the
  // open group closes it.
  const mounted = useRef(false);
  useEffect(() => {
    if (!mounted.current) {
      mounted.current = true;
      return;
    }
    setAdminOpen(false);
    setMobileOpen(false);
  }, [pathname]);

  // Registered only while the group is open, removed on cleanup. `pointerdown`
  // rather than `click` so the panel is gone before a click on a control
  // underneath it resolves.
  useEffect(() => {
    if (!adminOpen) {
      return;
    }
    const onPointerDown = (event: PointerEvent) => {
      if (!adminGroupRef.current?.contains(event.target as Node)) {
        setAdminOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [adminOpen]);

  // Escape closes the innermost open panel and hands focus back to the control
  // that opened it.
  useEffect(() => {
    if (!adminOpen && !mobileOpen) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') {
        return;
      }
      if (adminOpen) {
        setAdminOpen(false);
        adminTriggerRef.current?.focus();
        return;
      }
      setMobileOpen(false);
      hamburgerRef.current?.focus();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [adminOpen, mobileOpen]);

  const adminTriggerClass = adminActive ? 'nav-link nav-trigger active' : 'nav-link nav-trigger';

  return (
    <>
      <button
        ref={hamburgerRef}
        className="nav-hamburger"
        type="button"
        name="navMenu"
        data-testid="nav-hamburger"
        aria-label={mobileOpen ? 'Cerrar menú' : 'Abrir menú'}
        aria-expanded={mobileOpen}
        aria-controls={MOBILE_PANEL_ID}
        onClick={() => setMobileOpen((open) => !open)}
      >
        <span className="bar" aria-hidden="true" />
        <span className="bar" aria-hidden="true" />
        <span className="bar" aria-hidden="true" />
      </button>
      <nav
        id={MOBILE_PANEL_ID}
        className={mobileOpen ? 'primary-nav open' : 'primary-nav'}
        aria-label="Navegación principal"
        data-testid={MOBILE_PANEL_ID}
      >
        {topLevel.map((item) => (
          <NavEntry key={item.to} item={item} />
        ))}
        {adminItems.length > 0 ? (
          <div className="nav-group" ref={adminGroupRef}>
            <button
              ref={adminTriggerRef}
              className={adminTriggerClass}
              type="button"
              name="adminMenu"
              data-testid={ADMIN_GROUP.testId}
              aria-expanded={adminOpen}
              aria-controls={ADMIN_GROUP.panelId}
              onClick={() => setAdminOpen((open) => !open)}
            >
              {ADMIN_GROUP.label}
              <Chevron />
            </button>
            {adminOpen ? (
              <div id={ADMIN_GROUP.panelId} className="nav-panel" data-testid={ADMIN_GROUP.panelId}>
                {adminItems.map((item) => (
                  <NavEntry key={item.to} item={item} />
                ))}
              </div>
            ) : null}
          </div>
        ) : null}
      </nav>
    </>
  );
}
