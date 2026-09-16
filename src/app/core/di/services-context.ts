import { createContext, useContext } from 'react';
import type { PresetsState } from '../../features/search/services/search-presets.service';
import type { Permission } from '../../shared/models/auth.models';
import type { AdminUser } from '../../features/admin/users/profile.service';
import type { AdminRole } from '../../features/admin/roles/role.service';
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
 * Subscribes to the shared saved-search library.
 *
 * Presets are read during render, so a component must subscribe to the service's
 * signal rather than call `listPresets()` off `useServices()` - the latter would
 * read whatever was loaded at mount and never re-render when a create, rename or
 * delete changed it.
 */
export function useSearchPresets(): PresetsState {
  const { searchPresetsService } = useServices();
  return useSignal(searchPresetsService.state);
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

export function useUsers(): AdminUser[] {
  const { profileService } = useServices();
  return useSignal(profileService.users);
}

export function useRoles(): AdminRole[] {
  const { roleService } = useServices();
  return useSignal(roleService.roles);
}
