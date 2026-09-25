import { createBrowserRouter, Navigate } from 'react-router';
import { LoginPage } from './core/auth/login-page';
import { AppLayout } from './core/layout/app-layout';
import { RequireAuth } from './core/routing/require-auth';
import { RequirePermission } from './core/routing/require-permission';
import { AuditPage } from './features/admin/audit/audit-page';
import { ImportPage } from './features/admin/import/import-page';
import { PresetEditPage } from './features/admin/presets/preset-edit-page';
import { PresetListPage } from './features/admin/presets/preset-list-page';
import { AdminRolesPage } from './features/admin/roles/admin-roles-page';
import { AdminUsersPage } from './features/admin/users/admin-users-page';
import { CandidateDetailPage } from './features/candidates/pages/candidate-detail-page';
import { CatalogManagementPage } from './features/catalogs/pages/catalog-management-page';
import { AdvancedSearchPage } from './features/search/pages/advanced-search-page';
import { CandidateCreatePage } from './features/candidates/pages/candidate-create-page';
import { CandidateEditRedirect } from './features/candidates/pages/candidate-edit-redirect';
import { CandidateListPage } from './features/candidates/pages/candidate-list-page';
import { DashboardPage } from './features/dashboard/dashboard-page';
import { PositionListPage } from './features/positions/position-list-page';
import { PositionDetailPage } from './features/positions/position-detail-page';
import { PositionFormPage } from './features/positions/position-form-page';

/**
 * Route table ported from app.routes.ts.
 *
 * Note `candidates/new` is declared before `candidates/:id`. Angular resolved
 * first-match-wins; React Router 7 ranks static segments above dynamic ones, so
 * both resolve /app/candidates/new to the create form - but the ordering is kept
 * to make the intent obvious.
 */
export function createAppRouter() {
  return createBrowserRouter([
    { path: '/', element: <Navigate to="/app" replace /> },
    { path: '/login', element: <LoginPage /> },
    {
      element: <RequireAuth />,
      children: [
        {
          path: '/app',
          element: <AppLayout />,
          children: [
            { index: true, element: <DashboardPage /> },
            {
              element: <RequirePermission permission="positions.read" />,
              children: [
                { path: 'positions', element: <PositionListPage /> },
                { path: 'positions/:id', element: <PositionDetailPage /> },
              ],
            },
            {
              element: <RequirePermission permission="positions.manage" />,
              children: [
                { path: 'positions/new', element: <PositionFormPage /> },
                { path: 'positions/:id/edit', element: <PositionFormPage /> },
              ],
            },
            {
              element: <RequirePermission permission="candidates.create" />,
              children: [{ path: 'candidates/new', element: <CandidateCreatePage /> }],
            },
            {
              element: <RequirePermission permission="candidates.read" />,
              children: [
                { path: 'candidates', element: <CandidateListPage /> },
                { path: 'candidates/:id', element: <CandidateDetailPage /> },
                { path: 'candidates/:id/edit', element: <CandidateEditRedirect /> },
                { path: 'search', element: <AdvancedSearchPage /> },
              ],
            },
            {
              element: <RequirePermission permission="catalogs.manage" />,
              children: [{ path: 'catalogs', element: <CatalogManagementPage /> }],
            },
            {
              // No read-only preset route: a preset's criteria are viewed in the list's dialog,
              // and the separate page exists only to create or edit.
              element: <RequirePermission permission="presets.manage" />,
              children: [
                { path: 'admin/presets', element: <PresetListPage /> },
                { path: 'admin/presets/new', element: <PresetEditPage /> },
                { path: 'admin/presets/:id/edit', element: <PresetEditPage /> },
              ],
            },
            {
              element: <RequirePermission permission="users.manage" />,
              children: [{ path: 'admin/users', element: <AdminUsersPage /> }],
            },
            {
              element: <RequirePermission permission="roles.manage" />,
              children: [{ path: 'admin/roles', element: <AdminRolesPage /> }],
            },
            {
              element: <RequirePermission permission="candidates.import" />,
              children: [{ path: 'admin/import', element: <ImportPage /> }],
            },
            {
              element: <RequirePermission permission="audit.read" />,
              children: [{ path: 'admin/audit', element: <AuditPage /> }],
            },
          ],
        },
      ],
    },
    { path: '*', element: <Navigate to="/app" replace /> },
  ]);
}
