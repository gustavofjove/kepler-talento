import { Navigate, Outlet, useLocation } from 'react-router';
import { useServices } from '../di/services-context';
import { useSignal } from '../state/use-signal';

/**
 * Replaces `authGuard`. Auth state is synchronous (localStorage-backed), so an
 * element guard is enough - and unlike a loader it can subscribe to the profile
 * signal and be swapped in tests through ServicesProvider.
 */
export function RequireAuth() {
  const { authService } = useServices();
  const profile = useSignal(authService.profile);
  const { pathname } = useLocation();

  if (!profile) {
    return <Navigate to="/login" replace />;
  }

  // Mirrors the original `router.url !== '/mfa'` check: this guard wraps /mfa
  // too, so the comparison is what stops a redirect loop.
  if (profile.mfaRequired && pathname !== '/mfa') {
    return <Navigate to="/mfa" replace />;
  }

  return <Outlet />;
}
