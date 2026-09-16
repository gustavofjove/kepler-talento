import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminUsersPage } from '../../src/app/features/admin/users/admin-users-page';
import { AdminRolesPage } from '../../src/app/features/admin/roles/admin-roles-page';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { services, type Services } from '../../src/app/core/di/services';
import { signal } from '../../src/app/core/state/signal';
import { AppError } from '../../src/app/shared/models/error.models';

const role = {
  id: 'r-1',
  name: 'readonly',
  label: 'Solo lectura',
  isSystem: false,
  permissions: ['candidates.read'],
  isActive: true,
  createdAt: '',
  updatedAt: '',
  version: 1,
};
const adminUser = {
  id: 'u-1',
  displayName: 'Ana Admin',
  email: 'ana@example.com',
  roleName: 'readonly',
  isActive: true,
  lastSignInAt: null,
  createdAt: '',
  updatedAt: '',
  version: 1,
};

function renderPage(page: React.ReactNode, overrides: Partial<Services> = {}) {
  return render(
    <ServicesProvider value={{ ...services, ...overrides } as Services}>{page}</ServicesProvider>,
  );
}

describe('administration pages', () => {
  it('renders API users and asks for confirmation before deactivation', async () => {
    const confirm = vi.fn().mockResolvedValue(false);
    const profileService = {
      users: signal([adminUser]),
      load: vi.fn().mockResolvedValue([adminUser]),
      create: vi.fn(),
      setRole: vi.fn(),
      setActive: vi.fn(),
    };
    const roleService = { roles: signal([role]), load: vi.fn().mockResolvedValue([role]) };
    renderPage(<AdminUsersPage />, {
      profileService: profileService as never,
      roleService: roleService as never,
      confirmDialogService: { confirm } as never,
    });
    expect(await screen.findByText('Ana Admin')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Desactivar' }));
    expect(confirm).toHaveBeenCalledOnce();
    expect(profileService.setActive).not.toHaveBeenCalled();
  });

  it('keeps entered user values and shows a distinct server refusal', async () => {
    const profileService = {
      users: signal([adminUser]),
      load: vi.fn().mockResolvedValue([adminUser]),
      create: vi.fn(),
      setRole: vi.fn(),
      setActive: vi
        .fn()
        .mockRejectedValue(
          new AppError('CONFLICT', undefined, undefined, undefined, 'admin.lastAdministrator'),
        ),
    };
    const roleService = { roles: signal([role]), load: vi.fn().mockResolvedValue([role]) };
    renderPage(<AdminUsersPage />, {
      profileService: profileService as never,
      roleService: roleService as never,
      confirmDialogService: { confirm: vi.fn().mockResolvedValue(true) } as never,
    });
    await userEvent.type(screen.getByTestId('user-display-name'), 'Nueva Persona');
    await userEvent.click(await screen.findByRole('button', { name: 'Desactivar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Debe existir al menos un administrador activo.',
    );
    expect(screen.getByTestId('user-display-name')).toHaveValue('Nueva Persona');
  });

  it('renders roles and disables system-role deactivation', async () => {
    const systemRole = { ...role, isSystem: true };
    const roleService = {
      roles: signal([systemRole]),
      allPermissions: ['candidates.read'],
      load: vi.fn().mockResolvedValue([systemRole]),
      create: vi.fn(),
      updateLabel: vi.fn(),
      setPermissions: vi.fn(),
      setActive: vi.fn(),
    };
    renderPage(<AdminRolesPage />, { roleService: roleService as never });
    await waitFor(() => expect(roleService.load).toHaveBeenCalled());
    expect(screen.getByTestId('role-active-r-1')).toBeDisabled();
    expect(screen.getByText('Rol del sistema')).toBeInTheDocument();

    await userEvent.clear(screen.getByTestId('role-label-readonly'));
    await userEvent.type(screen.getByTestId('role-label-readonly'), 'Lectura actualizada');
    await userEvent.click(screen.getByTestId('role-label-save-readonly'));

    expect(roleService.updateLabel).toHaveBeenCalledWith(systemRole, 'Lectura actualizada');
  });
});
