import type { Candidate, CandidateStatus } from '../models/candidate.models';

export type SortField = 'updatedAt' | 'lastName' | 'status';
export type SortDirection = 'asc' | 'desc';
export type HasCvFilter = '' | 'yes' | 'no';

export interface CandidateFilters {
  textFilter: string;
  statusFilter: CandidateStatus | '';
  hasCvFilter: HasCvFilter;
  includeInactive: boolean;
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

/** Filters an already status-scoped list. `candidates` is the service output. */
export function filterCandidates(candidates: Candidate[], filters: CandidateFilters): Candidate[] {
  const text = filters.textFilter.trim().toLowerCase();

  return candidates.filter((candidate) => {
    const textMatch =
      !text ||
      [candidate.firstName, candidate.lastName, candidate.email, candidate.phone]
        .join(' ')
        .toLowerCase()
        .includes(text);
    const statusMatch = !filters.statusFilter || candidate.status === filters.statusFilter;
    const hasCv = candidate.documents.length > 0;
    const cvMatch = !filters.hasCvFilter || (filters.hasCvFilter === 'yes' ? hasCv : !hasCv);
    return textMatch && statusMatch && cvMatch;
  });
}

export function sortCandidates(
  candidates: Candidate[],
  field: SortField,
  direction: SortDirection,
): Candidate[] {
  return candidates.slice().sort((a, b) => {
    let value = 0;
    if (field === 'updatedAt') {
      value = a.updatedAt.localeCompare(b.updatedAt);
    } else if (field === 'lastName') {
      value = `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`);
    } else {
      value = a.status.localeCompare(b.status);
    }
    return direction === 'asc' ? value : -value;
  });
}

export function totalPages(itemCount: number, pageSize: number): number {
  return Math.max(Math.ceil(itemCount / pageSize), 1);
}

export function paginate<T>(items: T[], page: number, pageSize: number): T[] {
  const validPage = Math.min(Math.max(page, 1), totalPages(items.length, pageSize));
  const start = (validPage - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

export function buildFilterChips(filters: CandidateFilters): FilterChip[] {
  const chips: FilterChip[] = [];
  if (filters.textFilter.trim()) {
    chips.push({ key: 'text', label: `Texto: ${filters.textFilter.trim()}` });
  }
  if (filters.statusFilter) {
    chips.push({ key: 'status', label: `Estado: ${filters.statusFilter}` });
  }
  if (filters.hasCvFilter) {
    chips.push({
      key: 'hasCv',
      label: filters.hasCvFilter === 'yes' ? 'CV: Con CV' : 'CV: Sin CV',
    });
  }
  if (filters.includeInactive) {
    chips.push({ key: 'includeInactive', label: 'Incluye inactivos' });
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

export function nextSort(
  current: { field: SortField; direction: SortDirection },
  field: SortField,
): { field: SortField; direction: SortDirection } {
  if (current.field === field) {
    return { field, direction: current.direction === 'asc' ? 'desc' : 'asc' };
  }
  return { field, direction: field === 'updatedAt' ? 'desc' : 'asc' };
}

export function sortIndicator(
  current: { field: SortField; direction: SortDirection },
  field: SortField,
): string {
  if (current.field !== field) {
    return '';
  }
  return current.direction === 'asc' ? '↑' : '↓';
}
