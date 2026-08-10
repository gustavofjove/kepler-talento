import { RoleService } from '../../src/app/features/admin/roles/role.service';

describe('RoleService', () => {
  let service: RoleService;

  beforeEach(() => {
    localStorage.clear();
    service = new RoleService();
  });

  it('creates a custom role', () => {
    const role = service.create('recruiter_junior', 'Recruiter Junior');

    expect(role.name).toBe('recruiter_junior');
    expect(service.roles().some((item) => item.name === 'recruiter_junior')).toBe(true);
  });

  it('prevents editing labels of system roles', () => {
    expect(() => service.updateLabel('rrhh_admin', 'Nuevo nombre')).toThrow(/rol de sistema/i);
  });

  it('prevents changing permissions of system roles', () => {
    expect(() => service.setPermission('rrhh_admin', 'manage_users', false)).toThrow(
      /rol de sistema/i,
    );
  });

  it('updates custom role permissions', () => {
    service.create('ops_custom', 'Ops Custom');

    service.setPermission('ops_custom', 'manage_users', true);

    const role = service.roles().find((item) => item.name === 'ops_custom');
    expect(role?.permissions.includes('manage_users')).toBe(true);
  });
});
