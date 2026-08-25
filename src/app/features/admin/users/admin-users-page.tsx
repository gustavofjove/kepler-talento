import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useSignal } from '../../../core/state/use-signal';

const EMPTY_DRAFT = {
  displayName: '',
  email: '',
  role: 'rrhh_user',
  isActive: true,
  mfaRequired: false,
};

export function AdminUsersPage() {
  const { authService, profileService, roleService, toastService, confirmDialogService } =
    useServices();
  const notify = useErrorToast();
  const users = useSignal(profileService.users);
  const roles = useSignal(roleService.roles);

  const [draft, setDraft] = useState(EMPTY_DRAFT);

  const createUser = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      profileService.create(draft);
      setDraft({ ...EMPTY_DRAFT, role: roles[0]?.name || 'rrhh_user' });
      toastService.show('Usuario creado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo crear el usuario.');
    }
  };

  const changeRole = (userId: string, role: string): void => {
    try {
      profileService.updateRole(userId, role, authService.profile()?.email);
      toastService.show('Rol actualizado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo actualizar el rol.');
    }
  };

  const toggleActive = (userId: string): void => {
    try {
      profileService.toggleActive(userId, authService.profile()?.email);
      toastService.show('Estado actualizado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo actualizar el estado.');
    }
  };

  const toggleMfa = (userId: string): void => {
    try {
      profileService.toggleMfa(userId);
      toastService.show('MFA actualizado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo actualizar MFA.');
    }
  };

  const remove = async (userId: string): Promise<void> => {
    const confirmed = await confirmDialogService.confirm({
      title: 'Eliminar usuario',
      message: 'Esta acción retirará el usuario de la operación actual.',
      confirmText: 'Eliminar usuario',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      profileService.remove(userId, authService.profile()?.email);
      toastService.show('Usuario eliminado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo eliminar el usuario.');
    }
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>Usuarios</h1>
        <p className="muted">Alta y mantenimiento de usuarios internos por rol.</p>
      </div>

      <form className="panel grid two" onSubmit={createUser} noValidate>
        <div className="field">
          <label htmlFor="displayName">Nombre</label>
          <input
            id="displayName"
            name="displayName"
            value={draft.displayName}
            onChange={(e) => setDraft({ ...draft, displayName: e.target.value })}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="user-email">Email</label>
          <input
            id="user-email"
            name="email"
            type="email"
            value={draft.email}
            onChange={(e) => setDraft({ ...draft, email: e.target.value })}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="user-role">Rol</label>
          <select
            id="user-role"
            name="role"
            value={draft.role}
            onChange={(e) => setDraft({ ...draft, role: e.target.value })}
            required
          >
            {roles.map((role) => (
              <option key={role.name} value={role.name}>
                {role.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label className="inline-check">
            <input
              name="mfa"
              type="checkbox"
              checked={draft.mfaRequired}
              onChange={(e) => setDraft({ ...draft, mfaRequired: e.target.checked })}
            />
            Requerir MFA
          </label>
        </div>
        <div className="form-actions span-all">
          <button className="button" type="submit">
            Crear usuario
          </button>
        </div>
      </form>

      <div className="panel table-wrap">
        {!users.length ? (
          <p className="empty-state">
            No hay usuarios cargados. Crea el primero para iniciar la operación.
          </p>
        ) : null}
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Email</th>
              <th>Rol</th>
              <th>Estado</th>
              <th>MFA</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.displayName}</td>
                <td>{user.email}</td>
                <td>
                  <select
                    value={user.role}
                    name={`role_${user.id}`}
                    onChange={(e) => changeRole(user.id, e.target.value)}
                  >
                    {roles.map((role) => (
                      <option key={role.name} value={role.name}>
                        {role.label}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <span className="badge">{user.isActive ? 'Activo' : 'Inactivo'}</span>
                </td>
                <td>
                  <span className="badge">{user.mfaRequired ? 'Obligatoria' : 'Opcional'}</span>
                </td>
                <td>
                  <div className="form-actions">
                    <button
                      className="button secondary"
                      type="button"
                      onClick={() => toggleActive(user.id)}
                    >
                      {user.isActive ? 'Desactivar' : 'Activar'}
                    </button>
                    <button
                      className="button secondary"
                      type="button"
                      onClick={() => toggleMfa(user.id)}
                    >
                      Cambiar MFA
                    </button>
                    <button className="button danger" type="button" onClick={() => remove(user.id)}>
                      Eliminar
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
