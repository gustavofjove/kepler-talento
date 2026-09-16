import type { TFunction } from 'i18next';
import type {
  CandidateListQuery,
  CandidateSortDirection,
  CandidateSortField,
  CandidateStatus,
  HasCvFilter,
} from '../models/candidate.models';

export type SortField = CandidateSortField;
export type SortDirection = CandidateSortDirection;
export type { HasCvFilter };

/**
 * The list's sort state. `field` is a string rather than `SortField` because it is read from
 * the URL and an unknown value is sent to the API to be refused, not silently replaced
 * (KTL-18 design D6).
 */
export interface ListSort {
  field: string;
  direction: SortDirection;
}

export interface CandidateFilters {
  textFilter: string;
  statusFilter: CandidateStatus | '';
  hasCvFilter: HasCvFilter;
  includeInactive: boolean;
}

/** Everything that defines one view of the list; it lives in the URL and nowhere else. */
export interface ListView {
  filters: CandidateFilters;
  sort: ListSort;
  page: number;
  pageSize: number;
}

export interface FilterChip {
  key: string;
  label: string;
}

export const EMPTY_FILTERS: CandidateFilters = {
  textFilter: '',
  statusFilter: '',
  hasCvFilter: '',
  includeInactive: false,
};

export const STATUS_OPTIONS: CandidateStatus[] = [
  'new',
  'available',
  'in_process',
  'hired',
  'rejected',
];

/** The search contract's documented default and maximum page sizes. */
export const DEFAULT_PAGE_SIZE = 25;
export const MAX_PAGE_SIZE = 100;
export const PAGE_SIZE_OPTIONS = [25, 50, 100];

export const DEFAULT_SORT: ListSort = { field: 'updatedAt', direction: 'desc' };

/**
 * URL parameter names. Changing one breaks every link a user has copied.
 *
 * The free-text filter is deliberately absent. A search term is personal data (a name, an
 * email), and a URL is recorded by every access log a reload or a pasted link passes
 * through — the same reason search is a POST. The text filter is therefore held by the page
 * and not shared.
 */
export const LIST_PARAMS = {
  page: 'page',
  pageSize: 'pageSize',
  sort: 'sort',
  direction: 'dir',
  status: 'status',
  hasCv: 'cv',
  includeInactive: 'inactive',
} as const;

/** Newest first for update time; alphabetical for the rest. */
export function defaultDirection(field: string): SortDirection {
  return field === 'updatedAt' ? 'desc' : 'asc';
}

function positiveInteger(value: string | null): number | undefined {
  if (!value || !/^\d+$/.test(value)) {
    return undefined;
  }
  const parsed = Number(value);
  return parsed >= 1 ? parsed : undefined;
}

/**
 * Reads a list view from URL parameters. Malformed values fall back to their default
 * rather than throwing — except an unknown sort field, which is kept so the API can refuse
 * it and the page can say so.
 */
export function readListView(params: URLSearchParams, textFilter = ''): ListView {
  const pageSize = positiveInteger(params.get(LIST_PARAMS.pageSize));
  const field = params.get(LIST_PARAMS.sort)?.trim() || DEFAULT_SORT.field;
  const direction = params.get(LIST_PARAMS.direction);
  const status = params.get(LIST_PARAMS.status) ?? '';
  const hasCv = params.get(LIST_PARAMS.hasCv) ?? '';
  return {
    filters: {
      textFilter,
      statusFilter: (STATUS_OPTIONS as string[]).includes(status)
        ? (status as CandidateStatus)
        : '',
      hasCvFilter: hasCv === 'yes' || hasCv === 'no' ? hasCv : '',
      includeInactive: params.get(LIST_PARAMS.includeInactive) === '1',
    },
    sort: {
      field,
      direction: direction === 'asc' || direction === 'desc' ? direction : defaultDirection(field),
    },
    page: positiveInteger(params.get(LIST_PARAMS.page)) ?? 1,
    pageSize: pageSize && pageSize <= MAX_PAGE_SIZE ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

/**
 * Writes a list view to URL parameters, omitting every default so a plain URL stays clean.
 * The text filter is never written; see `LIST_PARAMS`.
 */
export function writeListView(view: ListView): URLSearchParams {
  const params = new URLSearchParams();
  if (view.filters.statusFilter) {
    params.set(LIST_PARAMS.status, view.filters.statusFilter);
  }
  if (view.filters.hasCvFilter) {
    params.set(LIST_PARAMS.hasCv, view.filters.hasCvFilter);
  }
  if (view.filters.includeInactive) {
    params.set(LIST_PARAMS.includeInactive, '1');
  }
  if (view.sort.field !== DEFAULT_SORT.field) {
    params.set(LIST_PARAMS.sort, view.sort.field);
  }
  if (view.sort.direction !== defaultDirection(view.sort.field)) {
    params.set(LIST_PARAMS.direction, view.sort.direction);
  }
  if (view.page !== 1) {
    params.set(LIST_PARAMS.page, String(view.page));
  }
  if (view.pageSize !== DEFAULT_PAGE_SIZE) {
    params.set(LIST_PARAMS.pageSize, String(view.pageSize));
  }
  return params;
}

/** The one request a view issues. */
export function toListQuery(view: ListView): CandidateListQuery {
  return {
    page: view.page,
    pageSize: view.pageSize,
    sortField: view.sort.field,
    sortDirection: view.sort.direction,
    text: view.filters.textFilter.trim(),
    status: view.filters.statusFilter,
    hasCv: view.filters.hasCvFilter,
    includeInactive: view.filters.includeInactive,
  };
}

export function statusLabel(status: CandidateStatus, t: TFunction): string {
  return t(`search.criteria.status.${status}`);
}

export function buildFilterChips(filters: CandidateFilters, t: TFunction): FilterChip[] {
  const chips: FilterChip[] = [];
  const text = filters.textFilter.trim();
  if (text) {
    chips.push({ key: 'text', label: t('candidates.list.chip.text', { text }) });
  }
  if (filters.statusFilter) {
    chips.push({
      key: 'status',
      label: t('candidates.list.chip.status', { status: statusLabel(filters.statusFilter, t) }),
    });
  }
  if (filters.hasCvFilter) {
    chips.push({
      key: 'hasCv',
      label: t(
        filters.hasCvFilter === 'yes'
          ? 'candidates.list.chip.withCv'
          : 'candidates.list.chip.withoutCv',
      ),
    });
  }
  if (filters.includeInactive) {
    chips.push({ key: 'includeInactive', label: t('candidates.list.chip.includeInactive') });
  }
  return chips;
}

/** Clears a single filter by chip key, leaving the rest untouched. */
export function removeFilter(filters: CandidateFilters, key: string): CandidateFilters {
  switch (key) {
    case 'text':
      return { ...filters, textFilter: '' };
    case 'status':
      return { ...filters, statusFilter: '' };
    case 'hasCv':
      return { ...filters, hasCvFilter: '' };
    case 'includeInactive':
      return { ...filters, includeInactive: false };
    default:
      return filters;
  }
}

export function nextSort(current: ListSort, field: SortField): ListSort {
  if (current.field === field) {
    return { field, direction: current.direction === 'asc' ? 'desc' : 'asc' };
  }
  return { field, direction: defaultDirection(field) };
}

export function sortIndicator(current: ListSort, field: SortField): string {
  if (current.field !== field) {
    return '';
  }
  return current.direction === 'asc' ? '↑' : '↓';
}
