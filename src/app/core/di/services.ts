import { ImportService } from '../../features/admin/import/import.service';
import { RoleService } from '../../features/admin/roles/role.service';
import { ProfileService } from '../../features/admin/users/profile.service';
import { CandidateRelationsService } from '../../features/candidates/services/candidate-relations.service';
import { CandidateApi } from '../../features/candidates/services/candidate.api';
import { CandidateService } from '../../features/candidates/services/candidate.service';
import { CatalogApi } from '../../features/catalogs/services/catalog.api';
import { CatalogService } from '../../features/catalogs/services/catalog.service';
import { DocumentService } from '../../features/documents/services/document.service';
import { CandidateSearchService } from '../../features/search/services/candidate-search.service';
import { ExportService } from '../../features/search/services/export.service';
import { SearchPresetsService } from '../../features/search/services/search-presets.service';
import { AuthService } from '../auth/auth.service';
import { MfaService } from '../auth/mfa.service';
import { DataRouterNavigator } from '../routing/navigator';
import { ObservabilityService } from '../services/observability.service';
import { ToastService } from '../services/toast.service';
import { SupabaseClientService } from '../supabase/supabase-client.service';
import { ConfirmDialogService } from '../../shared/components/confirm-dialog.service';
import { ApiTransport } from '../http/api-transport';
import { readAppConfig } from '../services/app-config.model';

/**
 * Composition root - replaces Angular's `providedIn: 'root'` injector.
 *
 * The graph is acyclic and shallow: CandidateService is the root of the feature
 * graph, ProfileService takes RoleService, and the auth pair take the Supabase
 * client wrapper.
 */

export const appNavigator = new DataRouterNavigator();

const supabaseClientService = new SupabaseClientService();
const roleService = new RoleService();
const apiTransport = new ApiTransport(readAppConfig().API_BASE_URL);
const candidateService = new CandidateService(new CandidateApi(apiTransport));

export const services = {
  appNavigator,
  supabaseClientService,
  authService: new AuthService(supabaseClientService, appNavigator),
  mfaService: new MfaService(supabaseClientService),
  toastService: new ToastService(),
  observabilityService: new ObservabilityService(),
  confirmDialogService: new ConfirmDialogService(),
  candidateService,
  catalogService: new CatalogService(new CatalogApi(apiTransport)),
  candidateRelationsService: new CandidateRelationsService(candidateService),
  documentService: new DocumentService(candidateService, apiTransport),
  // Search and saved searches reach the API directly. Neither depends on CandidateService
  // any more: the browser no longer holds a candidate collection to filter.
  candidateSearchService: new CandidateSearchService(apiTransport),
  searchPresetsService: new SearchPresetsService(apiTransport),
  exportService: new ExportService(),
  importService: new ImportService(),
  roleService,
  profileService: new ProfileService(roleService),
  apiTransport,
};

export type Services = typeof services;
