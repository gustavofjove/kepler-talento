/**
 * The permission vocabulary. These strings are the API's own catalogue
 * (`Permissions.All` in `backend/Application/Abstractions/Identity/ICurrentActor.cs`);
 * there is no mapping layer, and `tests/unit/permission-vocabulary.spec.ts` keeps the two
 * from drifting.
 */
export type Permission =
  | 'candidates.read'
  | 'candidates.create'
  | 'candidates.update'
  | 'candidates.delete'
  // Enforced only by the browser import page until KTL-17 gives it a server-side guard.
  | 'candidates.import'
  // Likewise browser-only: the CSV export on the advanced search page.
  | 'candidates.export'
  | 'documents.upload'
  | 'documents.download'
  | 'catalogs.read'
  | 'catalogs.manage'
  | 'presets.manage'
  | 'users.manage'
  | 'roles.manage';

export const ALL_PERMISSIONS: Permission[] = [
  'candidates.read',
  'candidates.create',
  'candidates.update',
  'candidates.delete',
  'candidates.import',
  'candidates.export',
  'documents.upload',
  'documents.download',
  'catalogs.read',
  'catalogs.manage',
  'presets.manage',
  'users.manage',
  'roles.manage',
];

export interface UserProfile {
  id: string;
  displayName: string;
  email: string;
  role: string;
  roleLabel: string;
  isActive: boolean;
  permissions: Permission[];
}
