export type Permission =
  | 'view_candidates'
  | 'create_candidates'
  | 'edit_candidates'
  | 'delete_candidates'
  | 'view_all_candidates'
  | 'download_candidate_documents'
  | 'upload_candidate_documents'
  | 'export_candidates'
  | 'import_candidates'
  | 'manage_catalogs'
  | 'manage_users'
  | 'manage_roles';

export const ALL_PERMISSIONS: Permission[] = [
  'view_candidates',
  'create_candidates',
  'edit_candidates',
  'delete_candidates',
  'view_all_candidates',
  'download_candidate_documents',
  'upload_candidate_documents',
  'export_candidates',
  'import_candidates',
  'manage_catalogs',
  'manage_users',
  'manage_roles',
];

export interface RoleDefinition {
  name: string;
  label: string;
  permissions: Permission[];
  isSystem?: boolean;
}

export interface UserProfile {
  id: string;
  displayName: string;
  email: string;
  role: string;
  isActive: boolean;
  mfaRequired: boolean;
  permissions: Permission[];
}

export const DEFAULT_ROLES: RoleDefinition[] = [
  {
    name: 'rrhh_admin',
    label: 'RRHH Admin',
    isSystem: true,
    permissions: [
      'view_candidates',
      'create_candidates',
      'edit_candidates',
      'delete_candidates',
      'view_all_candidates',
      'download_candidate_documents',
      'upload_candidate_documents',
      'export_candidates',
      'import_candidates',
      'manage_catalogs',
      'manage_users',
      'manage_roles',
    ],
  },
  {
    name: 'rrhh_user',
    label: 'RRHH User',
    isSystem: true,
    permissions: [
      'view_candidates',
      'create_candidates',
      'edit_candidates',
      'download_candidate_documents',
      'upload_candidate_documents',
      'export_candidates',
    ],
  },
  {
    name: 'manager_reader',
    label: 'Manager reader',
    isSystem: true,
    permissions: ['view_candidates', 'download_candidate_documents'],
  },
  {
    name: 'readonly',
    label: 'Solo lectura',
    isSystem: true,
    permissions: ['view_candidates'],
  },
  {
    name: 'system_admin',
    label: 'System admin',
    isSystem: true,
    permissions: [
      'view_candidates',
      'view_all_candidates',
      'manage_catalogs',
      'manage_users',
      'manage_roles',
    ],
  },
];
