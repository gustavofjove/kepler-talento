import { Navigate, Outlet } from 'react-router';
import { useServices } from '../di/services-context';
import { useSignal } from '../state/use-signal';

/**
 * Replaces `authGuard`. The element guard subscribes to the in-memory profile
 * signal and can be swapped in tests through ServicesProvider.
 */
export function RequireAuth() {
  const { authService } = useServices();
  const profile = useSignal(authService.profile);
  if (!profile) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
