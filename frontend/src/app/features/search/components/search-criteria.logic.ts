import type { TFunction } from 'i18next';
import type { CandidateStatus } from '../../candidates/models/candidate.models';
import {
  ALL_CANDIDATE_STATUSES,
  type MultiValueMode,
  type SearchFilters,
} from '../models/search.models';
import { CRITERIA_GROUPS } from './criteria-group.model';

export interface StatusOption {
  value: CandidateStatus;
  label: string;
}

export interface SummaryGroup {
  label: string;
  values: string[];
}

export function statusOptions(t: TFunction): StatusOption[] {
  return ALL_CANDIDATE_STATUSES.map((value) => ({
    value,
    label: t(`search.criteria.status.${value}`),
  }));
}

const modeLabel = (mode: MultiValueMode, t: TFunction): string =>
  t(mode === 'ALL' ? 'search.criteria.mode.all' : 'search.criteria.mode.any');

/**
 * One block per filter type, laid out in columns. The single source of what a filter set
 * "says" in words: the search page, the preset list and the preset detail all render it.
 */
export function buildSummaryGroups(filters: SearchFilters, t: TFunction): SummaryGroup[] {
  const options = statusOptions(t);
  const groups: SummaryGroup[] = [];
  const statusLabel = (status: string): string =>
    options.find((option) => option.value === status)?.label ?? status;

  if (filters.text.trim()) {
    groups.push({ label: t('search.criteria.text'), values: [filters.text.trim()] });
  }
  if (filters.statusValues.length < options.length) {
    groups.push({
      label: t('search.criteria.statuses'),
      values: filters.statusValues.map((status) => statusLabel(status)),
    });
  }
  if (filters.hasCv) {
    groups.push({
      label: t('search.criteria.cv'),
      values: [t(filters.hasCv === 'yes' ? 'search.criteria.cv.yes' : 'search.criteria.cv.no')],
    });
  }
  for (const group of CRITERIA_GROUPS) {
    const criteria = filters[`${group.kind}Criteria`];
    if (!criteria.length) {
      continue;
    }
    groups.push({
      label:
        criteria.length > 1
          ? t('search.criteria.summary.groupWithMode', {
              label: t(group.labelKey),
              mode: modeLabel(filters[`${group.kind}Mode`], t),
            })
          : t(group.labelKey),
      values: criteria.map(
        (c) =>
          `${c.value}${c.level ? ` · ${t('search.criteria.level.atLeast', { level: c.level })}` : ''}`,
      ),
    });
  }
  return groups;
}
