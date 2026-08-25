import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { services, type Services } from '../../src/app/core/di/services';
import { RequireAuth } from '../../src/app/core/routing/require-auth';
import { RequirePermission } from '../../src/app/core/routing/require-permission';
import type { UserProfile } from '../../src/app/shared/models/auth.models';

/**
 * Replaces the Angular auth-guards spec. The guards are now layout-route
 * elements, so each case renders a small route tree with sentinel elements and
 * asserts which one won.
 */
function renderGuards(profile: Partial<UserProfile> | null, initialEntry: string) {
  const authStub = {
    profile: () => profile,
    // useSignal only needs a subscribe that never fires for a static stub.
    hasPermission: (permission: string) =>
      Boolean(profile?.isActive && profile.permissions?.includes(permission as never)),
  };
  Object.assign(authStub.profile, { subscribe: () => () => undefined });

  const value = {
    ...services,
    authService: authStub as unknown as Services['authService'],
  } as Services;

  return render(
    <ServicesProvider value={value}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/login" element={<p>SENTINEL login</p>} />
          <Route element={<RequireAuth />}>
            <Route path="/mfa" element={<p>SENTINEL mfa</p>} />
            <Route path="/app" element={<p>SENTINEL app</p>} />
            <Route element={<RequirePermission permission="create_candidates" />}>
              <Route path="/app/candidates/new" element={<p>SENTINEL create</p>} />
            </Route>
          </Route>
        </Routes>
      </MemoryRouter>
    </ServicesProvider>,
  );
}

describe('Route guards', () => {
  it('redirects to login when there is no profile', () => {
    renderGuards(null, '/app/candidates/new');
    expect(screen.getByText('SENTINEL login')).toBeInTheDocument();
  });

  it('redirects to mfa when profile requires MFA', () => {
    renderGuards(
      { id: 'u-1', isActive: true, mfaRequired: true, permissions: ['view_candidates'] },
      '/app/candidates/new',
    );
    expect(screen.getByText('SENTINEL mfa')).toBeInTheDocument();
  });

  it('denies readonly profile on create route and redirects to app shell', () => {
    renderGuards(
      { id: 'u-2', isActive: true, mfaRequired: false, permissions: ['view_candidates'] },
      '/app/candidates/new',
    );
    expect(screen.getByText('SENTINEL app')).toBeInTheDocument();
  });

  it('allows permission guard when permission exists', () => {
    renderGuards(
      {
        id: 'u-3',
        isActive: true,
        mfaRequired: false,
        permissions: ['view_candidates', 'create_candidates'],
      },
      '/app/candidates/new',
    );
    expect(screen.getByText('SENTINEL create')).toBeInTheDocument();
  });

  it('denies permission guard for inactive profile even if permission is listed', () => {
    renderGuards(
      { id: 'u-4', isActive: false, mfaRequired: false, permissions: ['create_candidates'] },
      '/app/candidates/new',
    );
    expect(screen.getByText('SENTINEL app')).toBeInTheDocument();
  });

  it('allows authenticated profile when MFA is not required', () => {
    renderGuards(
      { id: 'u-5', isActive: true, mfaRequired: false, permissions: ['view_candidates'] },
      '/app',
    );
    expect(screen.getByText('SENTINEL app')).toBeInTheDocument();
  });
});
