import { NavLink, Outlet } from 'react-router';
import { ConfirmDialog } from '../../shared/components/confirm-dialog';
import { usePermission, useServices } from '../di/services-context';
import { useSignal } from '../state/use-signal';
import './app-layout.css';

const navClass = ({ isActive }: { isActive: boolean }): string => (isActive ? 'active' : '');

export function AppLayout() {
  const { authService, toastService } = useServices();
  const profile = useSignal(authService.profile);
  const messages = useSignal(toastService.messages);

  const canViewCandidates = usePermission('view_candidates');
  const canManageCatalogs = usePermission('manage_catalogs');
  const canManageUsers = usePermission('manage_users');
  const canManageRoles = usePermission('manage_roles');
  const canImport = usePermission('import_candidates');

  return (
    <div className="shell">
      <a className="skip-link" href="#main-content">
        Saltar al contenido principal
      </a>
      <header>
        <div className="brand">
          <strong>Kepker Talento</strong>
          <span className="muted">Gestión interna de candidatos del ecosistema Kepker</span>
        </div>
        <nav aria-label="Navegación principal">
          <NavLink to="/app" end className={navClass}>
            Dashboard
          </NavLink>
          {canViewCandidates ? (
            <>
              <NavLink to="/app/candidates" className={navClass}>
                Candidatos
              </NavLink>
              <NavLink to="/app/search" className={navClass}>
                Búsqueda
              </NavLink>
            </>
          ) : null}
          {canManageCatalogs ? (
            <NavLink to="/app/catalogs" className={navClass}>
              Catálogos
            </NavLink>
          ) : null}
          {canManageUsers ? (
            <NavLink to="/app/admin/users" className={navClass}>
              Usuarios
            </NavLink>
          ) : null}
          {canManageRoles ? (
            <NavLink to="/app/admin/roles" className={navClass}>
              Roles
            </NavLink>
          ) : null}
          {canImport ? (
            <NavLink to="/app/admin/import" className={navClass}>
              Importación
            </NavLink>
          ) : null}
        </nav>
        <div className="user">
          <span className="badge">{profile?.role}</span>
          <button className="button secondary" type="button" onClick={() => authService.signOut()}>
            Salir
          </button>
        </div>
      </header>
      <main id="main-content" tabIndex={-1}>
        <Outlet />
      </main>
      <div className="toasts" role="region" aria-label="Notificaciones">
        {messages.map((message) => (
          <article className="toast" role="status" aria-live="polite" key={message.id}>
            <div>{message.text}</div>
            <button
              className="button ghost dismiss"
              type="button"
              onClick={() => toastService.dismiss(message.id)}
            >
              Cerrar
            </button>
          </article>
        ))}
      </div>
      <ConfirmDialog />
    </div>
  );
}
