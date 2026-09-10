import type { ApiTransport } from '../../../core/http/api-transport';
import {
  DEFAULT_SEARCH_PAGE_SIZE,
  EMPTY_SEARCH_FILTERS,
  type SearchFilters,
  type SearchResultPage,
} from '../models/search.models';

/**
 * Candidate search, over the API.
 *
 * The filtering, counting, ordering and paging all happen in PostgreSQL. Nothing here
 * hydrates candidate aggregates or holds a candidate collection: the browser used to load
 * every candidate - and, for skill/language/program criteria, every candidate's aggregate -
 * before filtering in memory, which is exactly what KTL-10 removes.
 *
 * `search` is a POST because the filter value carries nested criteria pairs that a query
 * string expresses badly and, more importantly, because search terms are personal data that
 * would otherwise land in every proxy and access log that records a URL.
 */
export class CandidateSearchService {
  constructor(private readonly transport: ApiTransport) {}

  emptyFilters(): SearchFilters {
    return structuredClone(EMPTY_SEARCH_FILTERS);
  }

  /**
   * Runs one search.
   *
   * The caller's `signal` reaches the transport unchanged, so a superseded search is really
   * aborted at the network rather than merely ignored when it eventually answers. The
   * cancellation surfaces as the shared transport's `CANCELLED` error, which the caller
   * suppresses; this service does not swallow it, because a service that hid cancellation
   * would also hide a caller's bug.
   */
  search(
    filters: SearchFilters,
    options: { page?: number; pageSize?: number; signal?: AbortSignal } = {},
  ): Promise<SearchResultPage> {
    return this.transport.request<SearchResultPage>('/candidates/search', {
      method: 'POST',
      body: JSON.stringify({
        filters,
        page: options.page ?? 1,
        pageSize: options.pageSize ?? DEFAULT_SEARCH_PAGE_SIZE,
      }),
      signal: options.signal,
    });
  }
}
