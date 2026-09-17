import {
  ADMIN_GROUP,
  ADMIN_ROUTES,
  isAdminRoute,
  NAV_PERMISSIONS,
  TOP_LEVEL_ITEMS,
  visibleItems,
} from '../../src/app/core/layout/nav-items';

describe('nav-items table', () => {
  it('maps every label to its route and permission', () => {
    const mapped = [...TOP_LEVEL_ITEMS, ...ADMIN_GROUP.items].map((item) => [
      item.label,
      item.to,
      item.permission ?? null,
    ]);

    expect(mapped).toEqual([
      ['Dashboard', '/app', null],
      ['Candidatos', '/app/candidates', 'candidates.read'],
      ['Búsqueda', '/app/search', 'candidates.read'],
      ['Catálogos', '/app/catalogs', 'catalogs.manage'],
      ['Presets', '/app/admin/presets', 'presets.manage'],
      ['Usuarios', '/app/admin/users', 'users.manage'],
      ['Roles', '/app/admin/roles', 'roles.manage'],
      ['Importación', '/app/admin/import', 'candidates.import'],
      ['Auditoría', '/app/admin/audit', 'audit.read'],
    ]);
  });

  it('end-matches only the dashboard, so /app does not match every child route', () => {
    expect(TOP_LEVEL_ITEMS.filter((item) => item.end).map((item) => item.to)).toEqual(['/app']);
  });

  it('gives every entry a unique test id', () => {
    const ids = [...TOP_LEVEL_ITEMS, ...ADMIN_GROUP.items].map((item) => item.testId);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('declares every permission the tables reference', () => {
    const used = [...TOP_LEVEL_ITEMS, ...ADMIN_GROUP.items]
      .map((item) => item.permission)
      .filter((permission) => permission !== undefined);

    expect(new Set(NAV_PERMISSIONS)).toEqual(new Set(used));
  });

  it('derives the group routes from the group itself', () => {
    expect(ADMIN_ROUTES).toEqual(ADMIN_GROUP.items.map((item) => item.to));
  });
});

describe('isAdminRoute', () => {
  it('matches an administration route and its nested paths', () => {
    expect(isAdminRoute('/app/admin/roles')).toBe(true);
    expect(isAdminRoute('/app/catalogs')).toBe(true);
    expect(isAdminRoute('/app/admin/users/u-1')).toBe(true);
    expect(isAdminRoute('/app/admin/presets')).toBe(true);
    expect(isAdminRoute('/app/admin/presets/new')).toBe(true);
    expect(isAdminRoute('/app/admin/presets/p-1/edit')).toBe(true);
    expect(isAdminRoute('/app/admin/audit')).toBe(true);
  });

  it('does not match the day-to-day routes', () => {
    expect(isAdminRoute('/app')).toBe(false);
    expect(isAdminRoute('/app/candidates')).toBe(false);
    expect(isAdminRoute('/app/search')).toBe(false);
  });
});

describe('visibleItems', () => {
  it('keeps entries without a permission requirement', () => {
    expect(visibleItems(TOP_LEVEL_ITEMS, {}).map((item) => item.label)).toEqual(['Dashboard']);
  });

  it('keeps only the entries the permission map grants', () => {
    const granted = { 'roles.manage': true, 'users.manage': false };
    expect(visibleItems(ADMIN_GROUP.items, granted).map((item) => item.label)).toEqual(['Roles']);
  });
});
