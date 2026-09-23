import { ProfileService, type AdminUser } from '../../src/app/features/admin/users/profile.service';

const user = (overrides: Partial<AdminUser> = {}): AdminUser => ({
  id: 'u-1',
  displayName: 'Ana',
  email: 'ana@example.com',
  roleName: 'readonly',
  isActive: true,
  lastSignInAt: null,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
  version: 3,
  ...overrides,
});

describe('ProfileService', () => {
  it('loads users from the API into its signal', async () => {
    const api = {
      request: vi
        .fn()
        .mockResolvedValue({ items: [user()], page: 1, pageSize: 100, totalCount: 1 }),
    };
    const service = new ProfileService(api as never);
    await service.load();
    expect(api.request).toHaveBeenCalledWith(
      '/admin/users?includeInactive=true&page=1&pageSize=100',
    );
    expect(service.users()).toEqual([user()]);
  });

  it('creates through the API without browser persistence', async () => {
    const created = user();
    const api = { request: vi.fn().mockResolvedValue(created) };
    const service = new ProfileService(api as never);
    await service.create({ displayName: 'Ana', email: 'ana@example.com', roleName: 'readonly' });
    expect(api.request).toHaveBeenCalledWith(
      '/admin/users',
      expect.objectContaining({ method: 'POST' }),
    );
    expect(service.users()).toEqual([created]);
  });

  it('sends the current version when changing role and activation', async () => {
    const current = user();
    const api = { request: vi.fn().mockResolvedValue(user({ version: 4 })) };
    const service = new ProfileService(api as never);
    service.users.set([current]);
    await service.setRole(current, 'rrhh_user');
    await service.setActive(current, false);
    expect(api.request.mock.calls[0][1].body).toContain('"version":3');
    expect(api.request.mock.calls[1][1].body).toContain('"version":3');
  });
});
