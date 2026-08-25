import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { PrimaryNav } from '../../src/app/core/layout/primary-nav';
import type { Permission } from '../../src/app/shared/models/auth.models';

/**
 * jsdom has no media queries, so the 768px breakpoint that decides what is
 * visible does not exist here. That is by design (see design.md - D1): this
 * spec asserts structure, permissions, state and focus; the desktop/mobile
 * layout itself is proven in tests/e2e/navigation-responsive.spec.ts.
 */

const ALL: Permission[] = [
  'view_candidates',
  'manage_catalogs',
  'manage_users',
  'manage_roles',
  'import_candidates',
];

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>;
}

function renderNav(permissions: Permission[], initialEntry = '/app') {
  // Must be a stable reference: useSignal reads it as getSnapshot, and a fresh
  // object literal per call makes React loop (see AGENTS.md).
  const profile = { id: 'u-1', permissions };
  const authStub = {
    profile: () => profile,
    hasPermission: (permission: Permission) => permissions.includes(permission),
  };
  Object.assign(authStub.profile, { subscribe: () => () => undefined });

  const value = {
    ...services,
    authService: authStub as unknown as Services['authService'],
  } as Services;

  return render(
    <ServicesProvider value={value}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <PrimaryNav />
        <LocationProbe />
        <Routes>
          <Route path="*" element={null} />
        </Routes>
      </MemoryRouter>
    </ServicesProvider>,
  );
}

const trigger = () => screen.getByTestId('nav-admin-trigger');

describe('PrimaryNav - entries and permissions', () => {
  it('shows three top-level entries plus the Admin group for a full profile', () => {
    renderNav(ALL);

    expect(screen.getByTestId('nav-dashboard')).toBeInTheDocument();
    expect(screen.getByTestId('nav-candidates')).toBeInTheDocument();
    expect(screen.getByTestId('nav-search')).toBeInTheDocument();
    expect(trigger()).toBeInTheDocument();
  });

  it('keeps the administration entries out of the document until the group is opened', () => {
    renderNav(ALL);

    expect(screen.queryByTestId('nav-catalogs')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-users')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-roles')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-import')).not.toBeInTheDocument();
  });

  it('hides candidate entries without view_candidates but keeps Dashboard', () => {
    renderNav(['manage_roles']);

    expect(screen.queryByTestId('nav-candidates')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-search')).not.toBeInTheDocument();
    expect(screen.getByTestId('nav-dashboard')).toBeInTheDocument();
  });

  it('renders the group with a single child when only one admin permission is held', async () => {
    const user = userEvent.setup();
    renderNav(['manage_roles']);

    await user.click(trigger());

    expect(screen.getByTestId('nav-roles')).toBeInTheDocument();
    expect(screen.queryByTestId('nav-catalogs')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-users')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-import')).not.toBeInTheDocument();
  });

  it('omits the Admin group entirely without any administration permission', () => {
    renderNav(['view_candidates']);

    expect(screen.queryByTestId('nav-admin-trigger')).not.toBeInTheDocument();
    expect(screen.getByTestId('nav-candidates')).toBeInTheDocument();
  });

  it('renders every label in Spanish with its accents', async () => {
    const user = userEvent.setup();
    renderNav(ALL);
    await user.click(trigger());

    expect(screen.getByTestId('nav-search')).toHaveTextContent('Búsqueda');
    expect(screen.getByTestId('nav-catalogs')).toHaveTextContent('Catálogos');
    expect(screen.getByTestId('nav-import')).toHaveTextContent('Importación');
  });
});

describe('PrimaryNav - the group parent is not a link', () => {
  it('is a button with no href', () => {
    renderNav(ALL);

    expect(trigger().tagName).toBe('BUTTON');
    expect(trigger()).toHaveAttribute('type', 'button');
    expect(trigger()).not.toHaveAttribute('href');
  });

  it('does not navigate when activated', async () => {
    const user = userEvent.setup();
    renderNav(ALL, '/app/candidates');

    await user.click(trigger());

    expect(screen.getByTestId('location')).toHaveTextContent('/app/candidates');
  });

  it('does not contribute the chevron to the accessible name', () => {
    renderNav(ALL);

    expect(screen.getByRole('button', { name: 'Admin' })).toBe(trigger());
  });
});

describe('PrimaryNav - opening and dismissing', () => {
  it('toggles aria-expanded and the children on click', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    expect(trigger()).toHaveAttribute('aria-expanded', 'false');

    await user.click(trigger());
    expect(trigger()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByTestId('nav-admin-panel')).toBeInTheDocument();

    await user.click(trigger());
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByTestId('nav-admin-panel')).not.toBeInTheDocument();
  });

  it('points aria-controls at the panel it opens', async () => {
    const user = userEvent.setup();
    renderNav(ALL);
    await user.click(trigger());

    expect(trigger().getAttribute('aria-controls')).toBe(screen.getByTestId('nav-admin-panel').id);
  });

  it('closes on Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    await user.click(trigger());
    await user.keyboard('{Escape}');

    expect(screen.queryByTestId('nav-admin-panel')).not.toBeInTheDocument();
    expect(trigger()).toHaveFocus();
  });

  it('closes when a pointer goes down outside the group', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    await user.click(trigger());
    await user.click(screen.getByTestId('location'));

    expect(screen.queryByTestId('nav-admin-panel')).not.toBeInTheDocument();
  });

  it('navigates and closes the group when a child is activated', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    await user.click(trigger());
    await user.click(screen.getByTestId('nav-users'));

    expect(screen.getByTestId('location')).toHaveTextContent('/app/admin/users');
    expect(screen.queryByTestId('nav-catalogs')).not.toBeInTheDocument();
  });
});

describe('PrimaryNav - active route indication', () => {
  it('marks the parent active and opens the group when landing on an admin route', () => {
    renderNav(ALL, '/app/admin/roles');

    expect(trigger().className).toContain('active');
    expect(trigger()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByTestId('nav-roles')).toBeInTheDocument();
  });

  it('leaves the parent inactive on a non-admin route', () => {
    renderNav(ALL, '/app/candidates');

    expect(trigger().className).not.toContain('active');
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
  });

  it('marks the active child as the current page', () => {
    renderNav(ALL, '/app/admin/roles');

    expect(screen.getByTestId('nav-roles')).toHaveAttribute('aria-current', 'page');
  });
});

describe('PrimaryNav - narrow-viewport controls', () => {
  it('exposes the hamburger with Spanish labels reflecting its state', async () => {
    const user = userEvent.setup();
    renderNav(ALL);
    const hamburger = screen.getByTestId('nav-hamburger');

    expect(hamburger).toHaveAttribute('aria-label', 'Abrir menú');
    expect(hamburger).toHaveAttribute('aria-expanded', 'false');

    await user.click(hamburger);

    expect(hamburger).toHaveAttribute('aria-label', 'Cerrar menú');
    expect(hamburger).toHaveAttribute('aria-expanded', 'true');
  });

  it('marks the panel it controls as open', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    const hamburger = screen.getByTestId('nav-hamburger');
    expect(hamburger.getAttribute('aria-controls')).toBe(screen.getByTestId('nav-mobile-panel').id);
    expect(screen.getByTestId('nav-mobile-panel').className).not.toContain('open');

    await user.click(hamburger);
    expect(screen.getByTestId('nav-mobile-panel').className).toContain('open');
  });

  it('closes the mobile panel on Escape and returns focus to the hamburger', async () => {
    const user = userEvent.setup();
    renderNav(ALL);
    const hamburger = screen.getByTestId('nav-hamburger');

    await user.click(hamburger);
    await user.keyboard('{Escape}');

    expect(hamburger).toHaveAttribute('aria-expanded', 'false');
    expect(hamburger).toHaveFocus();
  });

  it('closes the mobile panel when a child is activated', async () => {
    const user = userEvent.setup();
    renderNav(ALL);

    await user.click(screen.getByTestId('nav-hamburger'));
    await user.click(screen.getByTestId('nav-candidates'));

    expect(screen.getByTestId('nav-mobile-panel').className).not.toContain('open');
  });
});
