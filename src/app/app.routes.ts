import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/guards/auth.guard';
import { AppLayoutComponent } from './core/layout/app-layout.component';
import { LoginPageComponent } from './core/auth/login-page.component';
import { MfaPageComponent } from './core/auth/mfa-page.component';
import { DashboardPageComponent } from './features/dashboard/dashboard-page.component';
import { CandidateListPageComponent } from './features/candidates/pages/candidate-list-page.component';
import { CandidateDetailPageComponent } from './features/candidates/pages/candidate-detail-page.component';
import { CandidateEditPageComponent } from './features/candidates/pages/candidate-edit-page.component';
import { AdvancedSearchPageComponent } from './features/search/pages/advanced-search-page.component';
import { CatalogManagementPageComponent } from './features/catalogs/pages/catalog-management-page.component';
import { AdminUsersPageComponent } from './features/admin/users/admin-users-page.component';
import { AdminRolesPageComponent } from './features/admin/roles/admin-roles-page.component';
import { ImportPageComponent } from './features/admin/import/import-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  { path: 'login', component: LoginPageComponent },
  { path: 'mfa', component: MfaPageComponent, canActivate: [authGuard] },
  {
    path: 'app',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', component: DashboardPageComponent },
      {
        path: 'candidates',
        component: CandidateListPageComponent,
        canActivate: [permissionGuard('view_candidates')],
      },
      {
        path: 'candidates/new',
        component: CandidateEditPageComponent,
        canActivate: [permissionGuard('create_candidates')],
      },
      {
        path: 'candidates/:id',
        component: CandidateDetailPageComponent,
        canActivate: [permissionGuard('view_candidates')],
      },
      {
        path: 'candidates/:id/edit',
        component: CandidateEditPageComponent,
        canActivate: [permissionGuard('edit_candidates')],
      },
      {
        path: 'search',
        component: AdvancedSearchPageComponent,
        canActivate: [permissionGuard('view_candidates')],
      },
      {
        path: 'catalogs',
        component: CatalogManagementPageComponent,
        canActivate: [permissionGuard('manage_catalogs')],
      },
      {
        path: 'admin/users',
        component: AdminUsersPageComponent,
        canActivate: [permissionGuard('manage_users')],
      },
      {
        path: 'admin/roles',
        component: AdminRolesPageComponent,
        canActivate: [permissionGuard('manage_roles')],
      },
      {
        path: 'admin/import',
        component: ImportPageComponent,
        canActivate: [permissionGuard('import_candidates')],
      },
    ],
  },
  { path: '**', redirectTo: 'app' },
];
