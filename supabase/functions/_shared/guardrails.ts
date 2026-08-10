export interface AdminProfileRecord {
  id: string;
  role: string;
  isActive: boolean;
}

const ADMIN_ROLES = new Set(['rrhh_admin', 'system_admin']);

export function isAdminRole(role: string): boolean {
  return ADMIN_ROLES.has(role);
}

export function canDeactivateAdmin(
  targetUserId: string,
  actingUserId: string,
  profiles: AdminProfileRecord[],
): boolean {
  if (targetUserId === actingUserId) {
    return false;
  }

  const activeAdmins = profiles.filter((profile) => profile.isActive && isAdminRole(profile.role));
  if (activeAdmins.length <= 1 && activeAdmins.some((profile) => profile.id === targetUserId)) {
    return false;
  }

  return true;
}

export function isCatalogValueInUse(value: string, usages: string[]): boolean {
  return usages.some((usage) => usage.trim().toLowerCase() === value.trim().toLowerCase());
}
