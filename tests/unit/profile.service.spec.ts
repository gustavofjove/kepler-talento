import { ProfileService } from '../../src/app/features/admin/users/profile.service';
import { RoleService } from '../../src/app/features/admin/roles/role.service';

describe('ProfileService', () => {
  let roles: RoleService;
  let service: ProfileService;

  beforeEach(() => {
    localStorage.clear();
    roles = new RoleService();
    service = new ProfileService(roles);
  });

  it('prevents self role changes', () => {
    const admin = service.users()[0];

    expect(() => service.updateRole(admin.id, 'readonly', admin.email)).toThrow(/propio rol/i);
  });

  it('prevents self deactivation', () => {
    const admin = service.users()[0];

    expect(() => service.toggleActive(admin.id, admin.email)).toThrow(/desactivarte/i);
  });

  it('prevents deleting self user', () => {
    const admin = service.users()[0];

    expect(() => service.remove(admin.id, admin.email)).toThrow(/propio usuario/i);
  });

  it('prevents removing the last active admin', () => {
    const admin = service.users()[0];

    expect(() => service.toggleActive(admin.id, 'otro@example.com')).toThrow(
      /al menos un administrador activo/i,
    );
  });

  it('allows admin replacement when another admin exists', () => {
    service.create({
      displayName: 'Admin 2',
      email: 'admin2@example.com',
      role: 'rrhh_admin',
      isActive: true,
      mfaRequired: false,
    });

    const admin = service.users().find((user) => user.email === 'rrhh.admin@example.com')!;
    service.updateRole(admin.id, 'readonly', 'operator@example.com');

    const updated = service.users().find((user) => user.id === admin.id)!;
    expect(updated.role).toBe('readonly');
  });
});
