import type { CandidateStatus } from '../../candidates/models/candidate.models';
import type { MultiValueMode, SearchFilters } from '../models/search.models';
import { CRITERIA_GROUPS } from './criteria-group.model';

export interface StatusOption {
  value: CandidateStatus;
  label: string;
}

export interface SummaryGroup {
  label: string;
  values: string[];
}

const modeLabel = (mode: MultiValueMode): string => (mode === 'ALL' ? 'Todos' : 'Cualquiera');

/** One block per filter type for the collapsed view, laid out in columns. */
export function buildSummaryGroups(
  filters: SearchFilters,
  statusOptions: StatusOption[],
): SummaryGroup[] {
  const groups: SummaryGroup[] = [];
  const statusLabel = (status: string): string =>
    statusOptions.find((option) => option.value === status)?.label ?? status;

  if (filters.text.trim()) {
    groups.push({ label: 'Texto', values: [filters.text.trim()] });
  }
  if (filters.statusValues.length < statusOptions.length) {
    groups.push({
      label: 'Estados',
      values: filters.statusValues.map((status) => statusLabel(status)),
    });
  }
  if (filters.hasCv) {
    groups.push({ label: 'CV', values: [filters.hasCv === 'yes' ? 'Con CV' : 'Sin CV'] });
  }
  for (const group of CRITERIA_GROUPS) {
    const criteria = filters[`${group.kind}Criteria`];
    if (!criteria.length) {
      continue;
    }
    groups.push({
      label:
        criteria.length > 1
          ? `${group.label} (${modeLabel(filters[`${group.kind}Mode`])})`
          : group.label,
      values: criteria.map((c) => `${c.value}${c.level ? ` · ${c.level}` : ''}`),
    });
  }
  return groups;
}
