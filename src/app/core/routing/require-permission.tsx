import { Navigate, Outlet } from 'react-router';
import type { Permission } from '../../shared/models/auth.models';
import { usePermission } from '../di/services-context';

/** Replaces the `permissionGuard(permission)` factory. */
export function RequirePermission({ permission }: { permission: Permission }) {
  const allowed = usePermission(permission);
  return allowed ? <Outlet /> : <Navigate to="/app" replace />;
}
