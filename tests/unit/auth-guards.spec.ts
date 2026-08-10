import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from '../../src/app/core/auth/auth.service';
import { authGuard, permissionGuard } from '../../src/app/core/guards/auth.guard';

describe('Auth guards', () => {
  const createUrlTree = jest.fn((commands: string[]) => ({ commands }));

  const setup = (profile: any, url = '/app/candidates') => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthService,
          useValue: {
            profile: () => profile,
            isAuthenticated: !!profile,
            hasPermission: (permission: string) =>
              !!profile?.isActive && profile.permissions?.includes(permission),
          },
        },
        {
          provide: Router,
          useValue: {
            url,
            createUrlTree,
          },
        },
      ],
    });
  };

  beforeEach(() => {
    createUrlTree.mockClear();
  });

  it('redirects to login when there is no profile', () => {
    setup(null);

    const result = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));

    expect(result).toEqual({ commands: ['/login'] });
  });

  it('redirects to mfa when profile requires MFA', () => {
    setup({
      id: 'u-1',
      isActive: true,
      mfaRequired: true,
      permissions: ['view_candidates'],
    });

    const result = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));

    expect(result).toEqual({ commands: ['/mfa'] });
  });

  it('denies readonly profile on create route and redirects to app shell', () => {
    setup({
      id: 'u-2',
      isActive: true,
      mfaRequired: false,
      permissions: ['view_candidates'],
    });

    const result = TestBed.runInInjectionContext(() =>
      permissionGuard('create_candidates')({} as any, {} as any),
    );

    expect(result).toEqual({ commands: ['/app'] });
  });

  it('allows permission guard when permission exists', () => {
    setup({
      id: 'u-3',
      isActive: true,
      mfaRequired: false,
      permissions: ['view_candidates', 'create_candidates'],
    });

    const result = TestBed.runInInjectionContext(() =>
      permissionGuard('create_candidates')({} as any, {} as any),
    );

    expect(result).toBe(true);
  });

  it('denies permission guard for inactive profile even if permission is listed', () => {
    setup({
      id: 'u-4',
      isActive: false,
      mfaRequired: false,
      permissions: ['create_candidates'],
    });

    const result = TestBed.runInInjectionContext(() =>
      permissionGuard('create_candidates')({} as any, {} as any),
    );

    expect(result).toEqual({ commands: ['/app'] });
  });

  it('allows authenticated profile when MFA is not required', () => {
    setup({
      id: 'u-5',
      isActive: true,
      mfaRequired: false,
      permissions: ['view_candidates'],
    });

    const result = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));

    expect(result).toBe(true);
  });
});
