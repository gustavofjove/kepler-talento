import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useRoles, useServices } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import { AppError } from '../../../shared/models/error.models';
import type { Permission } from '../../../shared/models/auth.models';
import type { AdminRole } from './role.service';

export function AdminRolesPage() {
  const { t } = useTranslation();
  const { roleService, toastService, confirmDialogService } = useServices();
  const roles = useRoles();
  const [name, setName] = useState('');
  const [label, setLabel] = useState('');
  const [editedLabels, setEditedLabels] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  useEffect(() => {
    void roleService.load().catch((err) => setError(roleError(err, t)));
  }, [roleService, t]);
  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    try {
      await roleService.create(name, label);
      setName('');
      setLabel('');
      toastService.show(t('admin.roles.created'), 'success');
    } catch (err) {
      setError(roleError(err, t));
    }
  };
  const togglePermission = async (role: AdminRole, permission: Permission, enabled: boolean) => {
    const permissions = enabled
      ? [...new Set([...role.permissions, permission])]
      : role.permissions.filter((item) => item !== permission);
    try {
      await roleService.setPermissions(role, permissions);
    } catch (err) {
      setError(roleError(err, t));
    }
  };
  const saveLabel = async (role: AdminRole) => {
    try {
      await roleService.updateLabel(role, editedLabels[role.id] ?? role.label);
      setEditedLabels((current) => {
        const next = { ...current };
        delete next[role.id];
        return next;
      });
      toastService.show(t('admin.roles.labelUpdated'), 'success');
    } catch (err) {
      setError(roleError(err, t));
    }
  };
  const changeActive = async (role: AdminRole) => {
    const isActive = !role.isActive;
    const confirmed = await confirmDialogService.confirm({
      title: t(isActive ? 'admin.roles.reactivateTitle' : 'admin.roles.deactivateTitle'),
      message: t(isActive ? 'admin.roles.reactivateMessage' : 'admin.roles.deactivateMessage', {
        name: role.label,
      }),
      confirmText: t(isActive ? 'admin.roles.reactivate' : 'admin.roles.deactivate'),
      cancelText: t('admin.common.cancel'),
      danger: !isActive,
    });
    if (!confirmed) return;
    try {
      await roleService.setActive(role, isActive);
    } catch (err) {
      setError(roleError(err, t));
    }
  };
  return (
    <section className="page">
      <div className="page-header">
        <h1>{t('admin.roles.title')}</h1>
        <p className="muted">{t('admin.roles.subtitle')}</p>
      </div>
      <form className="panel grid two" onSubmit={create} noValidate>
        <div className="field">
          <label htmlFor="role-name">{t('admin.roles.name')}</label>
          <input
            id="role-name"
            name="name"
            data-testid="role-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="role-label">{t('admin.roles.label')}</label>
          <input
            id="role-label"
            name="label"
            data-testid="role-label"
            value={label}
            onChange={(e) => setLabel(e.target.value)}
          />
        </div>
        {error ? (
          <p className="muted span-all" role="alert">
            {error}
          </p>
        ) : null}
        <div className="form-actions span-all">
          <button className="button" type="submit" data-testid="create-role">
            {t('admin.roles.create')}
          </button>
        </div>
      </form>
      <div className="grid two">
        {roles.map((role) => (
          <article className="panel stack" key={role.id}>
            <div className="toolbar">
              <h2>{role.label}</h2>
              <div className="toolbar">
                <span className="badge">
                  {t(role.isSystem ? 'admin.roles.system' : 'admin.roles.custom')}
                </span>
                <span
                  className="badge"
                  data-testid={`role-status-${role.name}`}
                  data-status={role.isActive ? 'active' : 'inactive'}
                >
                  {t(role.isActive ? 'admin.common.active' : 'admin.common.inactive')}
                </span>
              </div>
            </div>
            <p className="muted">{role.name}</p>
            <div className="field">
              <label htmlFor={`role-label-${role.name}`}>{t('admin.roles.label')}</label>
              <div className="toolbar">
                <input
                  id={`role-label-${role.name}`}
                  name={`label_${role.name}`}
                  data-testid={`role-label-${role.name}`}
                  value={editedLabels[role.id] ?? role.label}
                  onChange={(event) =>
                    setEditedLabels((current) => ({
                      ...current,
                      [role.id]: event.target.value,
                    }))
                  }
                />
                <button
                  className="button secondary"
                  type="button"
                  data-testid={`role-label-save-${role.name}`}
                  onClick={() => void saveLabel(role)}
                >
                  {t('admin.roles.saveLabel')}
                </button>
              </div>
            </div>
            <div className="grid">
              {roleService.allPermissions.map((permission) => (
                <label className="inline-check" key={permission}>
                  <input
                    name={`${role.name}_${permission}`}
                    data-testid={`${role.name}-${permission}`}
                    type="checkbox"
                    checked={role.permissions.includes(permission)}
                    disabled={!role.isActive}
                    onChange={(e) => void togglePermission(role, permission, e.target.checked)}
                  />
                  {permission}
                </label>
              ))}
            </div>
            <div className="form-actions">
              <button
                className={role.isActive ? 'button danger' : 'button secondary'}
                type="button"
                data-testid={`role-active-${role.id}`}
                disabled={role.isSystem}
                title={role.isSystem ? t('admin.roles.systemProtected') : undefined}
                onClick={() => void changeActive(role)}
              >
                {t(role.isActive ? 'admin.roles.deactivate' : 'admin.roles.reactivate')}
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function roleError(err: unknown, t: ReturnType<typeof useTranslation>['t']): string {
  if (err instanceof AppError && err.backendCode)
    return t(`admin.errors.${err.backendCode}`, { defaultValue: errorText(err, t) });
  return errorText(err, t);
}
