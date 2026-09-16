import { RoleService, type AdminRole } from '../../src/app/features/admin/roles/role.service';

const role = (overrides: Partial<AdminRole> = {}): AdminRole => ({
  id: 'r-1',
  name: 'readonly',
  label: 'Solo lectura',
  isSystem: true,
  permissions: ['candidates.read'],
  isActive: true,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
  version: 2,
  ...overrides,
});

describe('RoleService', () => {
  it('loads roles from the API', async () => {
    const api = { request: vi.fn().mockResolvedValue([role()]) };
    const service = new RoleService(api as never);
    await service.load();
    expect(service.roles()).toEqual([role()]);
  });

  it('creates a role through the API', async () => {
    const created = role({ id: 'r-2', name: 'custom', isSystem: false });
    const api = { request: vi.fn().mockResolvedValue(created) };
    const service = new RoleService(api as never);
    await service.create('custom', 'Custom');
    expect(api.request).toHaveBeenCalledWith(
      '/admin/roles',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('sends the current version with permission and activation writes', async () => {
    const current = role();
    const api = { request: vi.fn().mockResolvedValue(role({ version: 3 })) };
    const service = new RoleService(api as never);
    service.roles.set([current]);
    await service.setPermissions(current, ['candidates.read', 'catalogs.read']);
    await service.setActive(current, false);
    expect(api.request.mock.calls[0][1].body).toContain('"version":2');
    expect(api.request.mock.calls[1][1].body).toContain('"version":2');
  });
});
