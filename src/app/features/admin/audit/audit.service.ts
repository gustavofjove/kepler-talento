import type { ApiTransport } from '../../../core/http/api-transport';
import { signal } from '../../../core/state/signal';
import {
  AUDIT_PAGE_SIZE,
  auditQuery,
  EMPTY_AUDIT_FILTERS,
  type AuditFilters,
  type AuditPage,
} from './audit.logic';

export interface AuditState {
  filters: AuditFilters;
  page: AuditPage;
}

interface UserPage {
  items: { id: string; displayName: string }[];
}

/** The users listing's largest page; ids beyond it stay unresolved and render by id. */
const USER_LOOKUP_PAGE_SIZE = 100;

/**
 * The audit trail, as an API gateway (KTL-19). The API returns identifiers and codes only;
 * display names are resolved here, per page, through the users endpoint (design D9).
 */
export class AuditService {
  readonly state = signal<AuditState>({
    filters: EMPTY_AUDIT_FILTERS,
    page: { items: [], page: 1, pageSize: AUDIT_PAGE_SIZE, totalCount: 0 },
  });

  constructor(private readonly api: ApiTransport) {}

  async load(filters: AuditFilters, page = 1): Promise<AuditPage> {
    const result = await this.api.request<AuditPage>(`/audit/events?${auditQuery(filters, page)}`);
    this.state.set({ filters, page: result });
    return result;
  }

  /**
   * Resolves display names for the given user ids in one call. The caller only asks when the
   * reader holds `users.manage`; anyone else sees the internal id, which is the honest
   * degradation rather than an error.
   */
  async resolveActorNames(ids: string[]): Promise<Record<string, string>> {
    if (ids.length === 0) {
      return {};
    }
    const wanted = new Set(ids);
    const users = await this.api.request<UserPage>(
      `/admin/users?includeInactive=true&page=1&pageSize=${USER_LOOKUP_PAGE_SIZE}`,
    );
    return Object.fromEntries(
      users.items.filter((user) => wanted.has(user.id)).map((user) => [user.id, user.displayName]),
    );
  }
}
