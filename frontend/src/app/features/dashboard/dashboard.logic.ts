import type { CandidateListQuery } from '../candidates/models/candidate.models';
import {
  DEFAULT_PAGE_SIZE,
  DEFAULT_SORT,
  EMPTY_FILTERS,
  writeListView,
  type CandidateFilters,
  type ListSort,
} from '../candidates/pages/candidate-list.logic';
import type { PositionListQuery } from '../positions/position.models';
import type { SearchPreset } from '../search/models/search.models';

/**
 * The home page's pure parts (KTL-40): what each panel asks for, how the figures are derived and
 * where each one leads. The page composes existing endpoints; there is no dashboard endpoint.
 */

/** Rows per list panel. */
export const DASHBOARD_ROWS = 5;

/** A one-row search: only its `totalCount` is read, so no candidate list is fetched to count. */
const COUNT: CandidateListQuery = {
  page: 1,
  pageSize: 1,
  sortField: 'updatedAt',
  sortDirection: 'desc',
  text: '',
  availability: '',
  hasCv: '',
  includeInactive: false,
};

/**
 * The five candidate searches. None carries a text filter, so all stay on the SQL path. The
 * active total and the available figure are the `totalCount`s of the two list panels, which keeps
 * the page within its budget of five searches, one position list and one preset list.
 */
export const CANDIDATE_QUERIES = {
  recentAvailable: {
    ...COUNT,
    pageSize: DASHBOARD_ROWS,
    availability: 'available',
    sortField: 'availabilityCheckedOn',
    sortDirection: 'desc',
  },
  recentAdded: {
    ...COUNT,
    pageSize: DASHBOARD_ROWS,
    sortField: 'createdAt',
    sortDirection: 'desc',
  },
  unavailableCount: { ...COUNT, availability: 'unavailable' },
  withoutCvCount: { ...COUNT, hasCv: 'no' },
  /** Active and removed together; only sent for `candidates.delete` holders. */
  everyoneCount: { ...COUNT, includeInactive: true },
} satisfies Record<string, CandidateListQuery>;

/** The most recently updated open positions; its `totalCount` is the open-positions figure. */
export const OPEN_POSITIONS_QUERY: PositionListQuery = {
  status: 'open',
  sortField: 'updatedAt',
  sortDirection: 'desc',
  page: 1,
  pageSize: DASHBOARD_ROWS,
};

export interface AvailabilitySplit {
  total: number;
  available: number;
  unavailable: number;
  unknown: number;
}

/**
 * The card's split. «Sin comprobar» is not counted by a request of its own: it is what remains of
 * the active total, and never below zero when the figures, taken by separate requests, momentarily
 * disagree.
 */
export function availabilitySplit(
  total: number,
  available: number,
  unavailable: number,
): AvailabilitySplit {
  return { total, available, unavailable, unknown: Math.max(0, total - available - unavailable) };
}

/** Removed candidates: everyone minus the active ones, never below zero. */
export function inactiveCount(everyone: number, active: number): number {
  return Math.max(0, everyone - active);
}

/** A candidate list URL, written by the list's own serializer so parameter names live in one place. */
function candidateListHref(filters: Partial<CandidateFilters>, sort: ListSort = DEFAULT_SORT) {
  const query = writeListView({
    filters: { ...EMPTY_FILTERS, ...filters },
    sort,
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  }).toString();
  return query ? `/app/candidates?${query}` : '/app/candidates';
}

/** Where each figure and «Ver todos» leads. */
export const DASHBOARD_HREFS = {
  active: candidateListHref({}),
  available: candidateListHref(
    { availabilityFilter: 'available' },
    { field: 'availabilityCheckedOn', direction: 'desc' },
  ),
  unavailable: candidateListHref({ availabilityFilter: 'unavailable' }),
  unknown: candidateListHref({ availabilityFilter: 'unknown' }),
  withoutCv: candidateListHref({ hasCvFilter: 'no' }),
  recentAdded: candidateListHref({}, { field: 'createdAt', direction: 'desc' }),
  positions: '/app/positions',
  presets: '/app/admin/presets',
};

export function candidateHref(candidateId: string): string {
  return `/app/candidates/${encodeURIComponent(candidateId)}`;
}

export function positionHref(positionId: string): string {
  return `/app/positions/${encodeURIComponent(positionId)}`;
}

/**
 * The saved searches the panel shows: most recently used first, never-used ones last, then by
 * name, at most `DASHBOARD_ROWS`.
 */
export function topPresets(presets: readonly SearchPreset[]): SearchPreset[] {
  const used = (preset: SearchPreset) =>
    preset.lastUsedAt ? Date.parse(preset.lastUsedAt) : Number.NEGATIVE_INFINITY;
  return [...presets]
    .sort((left, right) => {
      const byUse = used(right) - used(left);
      if (byUse !== 0 && !Number.isNaN(byUse)) return byUse;
      return left.name.localeCompare(right.name, 'es', { sensitivity: 'base' });
    })
    .slice(0, DASHBOARD_ROWS);
}

/** Segment widths of the availability bar as fractions of the total; empty when there is none. */
export function barSegments(split: AvailabilitySplit): { tone: BarTone; share: number }[] {
  if (split.total <= 0) return [];
  return (
    [
      { tone: 'success', share: split.available / split.total },
      { tone: 'danger', share: split.unavailable / split.total },
      { tone: 'neutral', share: split.unknown / split.total },
    ] as const
  ).filter((segment) => segment.share > 0);
}

/** The KTL-38 chip tones: available, unavailable and unchecked. */
export type BarTone = 'success' | 'danger' | 'neutral';
