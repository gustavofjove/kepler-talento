import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useRoles, useServices, useUsers } from '../../../core/di/services-context';
import { errorText } from '../../../core/i18n/translatable-error';
import '../../../shared/components/data-table.css';
import { AppError } from '../../../shared/models/error.models';
import type { AdminUser } from './profile.service';

const EMPTY = { displayName: '', email: '', roleName: 'readonly' };

export function AdminUsersPage() {
  const { t } = useTranslation();
  const { profileService, roleService, toastService, confirmDialogService } = useServices();
  const users = useUsers();
  const roles = useRoles();
  const [draft, setDraft] = useState(EMPTY);
  const [error, setError] = useState('');

  useEffect(() => {
    void Promise.all([profileService.load(), roleService.load()]).catch((err) =>
      setError(adminError(err, t)),
    );
  }, [profileService, roleService, t]);

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError('');
    try {
      await profileService.create(draft);
      setDraft({ ...EMPTY, roleName: roles[0]?.name ?? 'readonly' });
      toastService.show(t('admin.users.created'), 'success');
    } catch (err) {
      setError(adminError(err, t));
    }
  };

  const changeRole = async (user: AdminUser, roleName: string) => {
    try {
      await profileService.setRole(user, roleName);
      toastService.show(t('admin.users.roleUpdated'), 'success');
    } catch (err) {
      setError(adminError(err, t));
    }
  };

  const changeActive = async (user: AdminUser) => {
    const isActive = !user.isActive;
    const confirmed = await confirmDialogService.confirm({
      title: t(isActive ? 'admin.users.reactivateTitle' : 'admin.users.deactivateTitle'),
      message: t(isActive ? 'admin.users.reactivateMessage' : 'admin.users.deactivateMessage', {
        name: user.displayName,
      }),
      confirmText: t(isActive ? 'admin.users.reactivate' : 'admin.users.deactivate'),
      cancelText: t('admin.common.cancel'),
      danger: !isActive,
    });
    if (!confirmed) return;
    try {
      await profileService.setActive(user, isActive);
      toastService.show(t('admin.users.stateUpdated'), 'success');
    } catch (err) {
      setError(adminError(err, t));
    }
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>{t('admin.users.title')}</h1>
        <p className="muted">{t('admin.users.subtitle')}</p>
      </div>
      <form className="panel grid two" onSubmit={create} noValidate>
        <div className="field">
          <label htmlFor="displayName">{t('admin.users.name')}</label>
          <input
            id="displayName"
            name="displayName"
            data-testid="user-display-name"
            value={draft.displayName}
            onChange={(e) => setDraft({ ...draft, displayName: e.target.value })}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="user-email">{t('admin.users.email')}</label>
          <input
            id="user-email"
            name="email"
            data-testid="user-email"
            type="email"
            value={draft.email}
            onChange={(e) => setDraft({ ...draft, email: e.target.value })}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="user-role">{t('admin.users.role')}</label>
          <select
            id="user-role"
            name="roleName"
            data-testid="user-role"
            value={draft.roleName}
            onChange={(e) => setDraft({ ...draft, roleName: e.target.value })}
          >
            {roles
              .filter((role) => role.isActive)
              .map((role) => (
                <option key={role.id} value={role.name}>
                  {role.label}
                </option>
              ))}
          </select>
        </div>
        {error ? (
          <p className="muted span-all" role="alert">
            {error}
          </p>
        ) : null}
        <div className="form-actions span-all">
          <button className="button" type="submit" data-testid="create-user">
            {t('admin.users.create')}
          </button>
        </div>
      </form>
      <div className="panel table-wrap">
        {/* No row navigation: a user has no page of its own yet, it is edited in place. */}
        <table className="data-table" data-testid="users-table">
          <thead>
            <tr>
              <th>{t('admin.users.name')}</th>
              <th>{t('admin.users.email')}</th>
              <th>{t('admin.users.role')}</th>
              <th>{t('admin.users.state')}</th>
              <th>{t('admin.common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.displayName}</td>
                <td>{user.email}</td>
                <td>
                  <select
                    name={`role_${user.id}`}
                    data-testid={`user-role-${user.id}`}
                    value={user.roleName}
                    onChange={(e) => void changeRole(user, e.target.value)}
                  >
                    {roles.map((role) => (
                      <option key={role.id} value={role.name}>
                        {role.label}
                      </option>
                    ))}
                  </select>
                </td>
                <td>
                  <span className="badge">
                    {t(user.isActive ? 'admin.common.active' : 'admin.common.inactive')}
                  </span>
                </td>
                <td>
                  <button
                    className={user.isActive ? 'button danger' : 'button secondary'}
                    type="button"
                    data-testid={`user-active-${user.id}`}
                    onClick={() => void changeActive(user)}
                  >
                    {t(user.isActive ? 'admin.users.deactivate' : 'admin.users.reactivate')}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function adminError(err: unknown, t: ReturnType<typeof useTranslation>['t']): string {
  if (err instanceof AppError && err.backendCode) {
    const key = `admin.errors.${err.backendCode}`;
    return t(key, { defaultValue: errorText(err, t) });
  }
  return errorText(err, t);
}
