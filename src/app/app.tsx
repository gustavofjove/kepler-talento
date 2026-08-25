import { createBrowserRouter, Navigate } from 'react-router';
import { LoginPage } from './core/auth/login-page';
import { MfaPage } from './core/auth/mfa-page';
import { AppLayout } from './core/layout/app-layout';
import { RequireAuth } from './core/routing/require-auth';
import { RequirePermission } from './core/routing/require-permission';
import { ImportPage } from './features/admin/import/import-page';
import { AdminRolesPage } from './features/admin/roles/admin-roles-page';
import { AdminUsersPage } from './features/admin/users/admin-users-page';
import { CandidateDetailPage } from './features/candidates/pages/candidate-detail-page';
import { CatalogManagementPage } from './features/catalogs/pages/catalog-management-page';
import { AdvancedSearchPage } from './features/search/pages/advanced-search-page';
import { CandidateEditPage } from './features/candidates/pages/candidate-edit-page';
import { CandidateListPage } from './features/candidates/pages/candidate-list-page';
import { DashboardPage } from './features/dashboard/dashboard-page';
import { ReferenceCandidatePage } from './features/reference/reference-candidate-page';
import { readAppConfig } from './core/services/app-config.model';

/**
 * Route table ported from app.routes.ts.
 *
 * Note `candidates/new` is declared before `candidates/:id`. Angular resolved
 * first-match-wins; React Router 7 ranks static segments above dynamic ones, so
 * both resolve /app/candidates/new to the create form - but the ordering is kept
 * to make the intent obvious.
 */
export function createAppRouter() {
  const runtimeEnvironment = readAppConfig().APP_ENV.toLowerCase();
  const developmentRoutes =
    import.meta.env.DEV ||
    import.meta.env.MODE === 'test' ||
    runtimeEnvironment === 'development' ||
    runtimeEnvironment === 'test'
      ? [{ path: '/platform/reference', element: <ReferenceCandidatePage /> }]
      : [];
  return createBrowserRouter([
    { path: '/', element: <Navigate to="/app" replace /> },
    { path: '/login', element: <LoginPage /> },
    ...developmentRoutes,
    {
      element: <RequireAuth />,
      children: [
        { path: '/mfa', element: <MfaPage /> },
        {
          path: '/app',
          element: <AppLayout />,
          children: [
            { index: true, element: <DashboardPage /> },
            {
              element: <RequirePermission permission="create_candidates" />,
              children: [{ path: 'candidates/new', element: <CandidateEditPage /> }],
            },
            {
              element: <RequirePermission permission="view_candidates" />,
              children: [
                { path: 'candidates', element: <CandidateListPage /> },
                { path: 'candidates/:id', element: <CandidateDetailPage /> },
                { path: 'search', element: <AdvancedSearchPage /> },
              ],
            },
            {
              element: <RequirePermission permission="edit_candidates" />,
              children: [{ path: 'candidates/:id/edit', element: <CandidateEditPage /> }],
            },
            {
              element: <RequirePermission permission="manage_catalogs" />,
              children: [{ path: 'catalogs', element: <CatalogManagementPage /> }],
            },
            {
              element: <RequirePermission permission="manage_users" />,
              children: [{ path: 'admin/users', element: <AdminUsersPage /> }],
            },
            {
              element: <RequirePermission permission="manage_roles" />,
              children: [{ path: 'admin/roles', element: <AdminRolesPage /> }],
            },
            {
              element: <RequirePermission permission="import_candidates" />,
              children: [{ path: 'admin/import', element: <ImportPage /> }],
            },
          ],
        },
      ],
    },
    { path: '*', element: <Navigate to="/app" replace /> },
  ]);
}
