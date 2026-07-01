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

export interface RoleDefinition {
  name: string;
  label: string;
  permissions: Permission[];
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
    permissions: ['view_candidates', 'download_candidate_documents'],
  },
  {
    name: 'readonly',
    label: 'Solo lectura',
    permissions: ['view_candidates'],
  },
  {
    name: 'system_admin',
    label: 'System admin',
    permissions: [
      'view_candidates',
      'view_all_candidates',
      'manage_catalogs',
      'manage_users',
      'manage_roles',
    ],
  },
];
