import { createContext, useContext } from 'react';
import type { Permission } from '../../shared/models/auth.models';
import { useSignal } from '../state/use-signal';
import { services, type Services } from './services';

/**
 * The context default is the real singleton graph, so production renders no
 * provider at all. Tests wrap with `<ServicesProvider value={{...services, x}}>`
 * to swap in doubles - the seam that replaces Angular's TestBed providers.
 */
const ServicesContext = createContext<Services>(services);

export const ServicesProvider = ServicesContext.Provider;

export function useServices(): Services {
  return useContext(ServicesContext);
}

/**
 * `hasPermission()` is a method, not a signal, so a component must subscribe to
 * the profile signal first or it will never re-render on sign-in/sign-out.
 */
export function usePermission(permission: Permission): boolean {
  const { authService } = useServices();
  useSignal(authService.profile);
  return authService.hasPermission(permission);
}
