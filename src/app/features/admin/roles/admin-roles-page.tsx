import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useSignal } from '../../../core/state/use-signal';
import type { Permission, RoleDefinition } from '../../../shared/models/auth.models';

export function AdminRolesPage() {
  const { roleService, profileService, toastService, confirmDialogService } = useServices();
  const notify = useErrorToast();
  const roles = useSignal(roleService.roles);
  const users = useSignal(profileService.users);

  const [newRoleName, setNewRoleName] = useState('');
  const [newRoleLabel, setNewRoleLabel] = useState('');
  const [editingRole, setEditingRole] = useState('');
  const [editLabel, setEditLabel] = useState('');

  const createRole = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      roleService.create(newRoleName, newRoleLabel);
      setNewRoleName('');
      setNewRoleLabel('');
      toastService.show('Rol creado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo crear el rol.');
    }
  };

  const saveLabel = (role: RoleDefinition): void => {
    try {
      roleService.updateLabel(role.name, editLabel);
      setEditingRole('');
      setEditLabel('');
      toastService.show('Rol actualizado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo actualizar.');
    }
  };

  const togglePermission = (
    role: RoleDefinition,
    permission: Permission,
    enabled: boolean,
  ): void => {
    try {
      roleService.setPermission(role.name, permission, enabled);
    } catch (error) {
      notify(error, 'No se pudo actualizar permisos.');
    }
  };

  const remove = async (role: RoleDefinition): Promise<void> => {
    const confirmed = await confirmDialogService.confirm({
      title: 'Eliminar rol',
      message: `Se eliminará el rol "${role.label}".`,
      confirmText: 'Eliminar rol',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmed) {
      return;
    }
    try {
      roleService.remove(role.name);
      toastService.show('Rol eliminado.', 'success');
    } catch (error) {
      notify(error, 'No se pudo eliminar el rol.');
    }
  };

  const isRoleInUse = (roleName: string): boolean => users.some((user) => user.role === roleName);

  return (
    <section className="page">
      <div className="page-header">
        <h1>Roles</h1>
        <p className="muted">Gestiona roles y permisos funcionales para operar la aplicación.</p>
      </div>

      <form className="panel grid two" onSubmit={createRole} noValidate>
        <div className="field">
          <label htmlFor="role-name">Nombre interno</label>
          <input
            id="role-name"
            name="name"
            value={newRoleName}
            placeholder="ej: recruiter_junior"
            onChange={(e) => setNewRoleName(e.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="role-label">Etiqueta visible</label>
          <input
            id="role-label"
            name="label"
            value={newRoleLabel}
            placeholder="ej: Recruiter Junior"
            onChange={(e) => setNewRoleLabel(e.target.value)}
            required
          />
        </div>
        <div className="form-actions span-all">
          <button className="button" type="submit">
            Crear rol
          </button>
        </div>
      </form>

      <div className="grid two">
        {roles.map((role) => (
          <article className="panel stack" key={role.name}>
            <div className="toolbar">
              {editingRole === role.name ? (
                <>
                  <input
                    name="editLabel"
                    value={editLabel}
                    onChange={(e) => setEditLabel(e.target.value)}
                  />
                  <button className="button" type="button" onClick={() => saveLabel(role)}>
                    Guardar
                  </button>
                </>
              ) : (
                <>
                  <h2>{role.label}</h2>
                  <button
                    className="button secondary"
                    type="button"
                    onClick={() => {
                      setEditingRole(role.name);
                      setEditLabel(role.label);
                    }}
                  >
                    Editar nombre
                  </button>
                </>
              )}
            </div>
            <p className="muted">
              {role.name} · {role.isSystem ? 'Rol de sistema' : 'Rol personalizado'}
            </p>
            <div className="grid">
              {roleService.allPermissions.map((permission) => (
                <label className="inline-check" key={permission}>
                  <input
                    type="checkbox"
                    checked={role.permissions.includes(permission)}
                    onChange={(e) => togglePermission(role, permission, e.target.checked)}
                  />
                  {permission}
                </label>
              ))}
            </div>
            <div className="form-actions">
              <button
                className="button danger"
                type="button"
                disabled={role.isSystem || isRoleInUse(role.name)}
                onClick={() => remove(role)}
              >
                Eliminar rol
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
