import type { TFunction } from 'i18next';
import { formatDay } from '../../../core/i18n/format';
import type { CandidateAvailabilityState } from '../../candidates/models/candidate.models';
import { availabilityLabel } from '../../candidates/components/candidate-availability.logic';
import {
  ALL_AVAILABILITY_STATES,
  type MultiValueMode,
  type SearchFilters,
} from '../models/search.models';
import { CATALOG_FAMILY_ORDER } from '../../catalogs/components/catalog-family-rows.logic';
import { CRITERIA_GROUPS } from './criteria-group.model';
import { selectedAvailability } from './search-basic-filters.logic';

export interface AvailabilityOption {
  value: CandidateAvailabilityState;
  label: string;
}

export interface SummaryGroup {
  label: string;
  values: string[];
}

export function availabilityOptions(t: TFunction): AvailabilityOption[] {
  return ALL_AVAILABILITY_STATES.map((value) => ({ value, label: availabilityLabel(value, t) }));
}

const modeLabel = (mode: MultiValueMode, t: TFunction): string =>
  t(mode === 'ALL' ? 'search.criteria.mode.all' : 'search.criteria.mode.any');

/**
 * One block per filter type, laid out in columns. The single source of what a filter set
 * "says" in words: the search page, the preset list and the preset detail all render it.
 */
export function buildSummaryGroups(filters: SearchFilters, t: TFunction): SummaryGroup[] {
  const options = availabilityOptions(t);
  const groups: SummaryGroup[] = [];
  const selected = selectedAvailability(
    filters.availabilityValues,
    options.map((option) => option.value),
  );
  const optionLabel = (value: string): string =>
    options.find((option) => option.value === value)?.label ?? value;

  if (filters.text.trim()) {
    groups.push({ label: t('search.criteria.text'), values: [filters.text.trim()] });
  }
  if (selected.length < options.length) {
    groups.push({
      label: t('search.criteria.availability.label'),
      values: selected.map((value) => optionLabel(value)),
    });
  }
  if (filters.availabilityCheckedFrom) {
    groups.push({
      label: t('search.criteria.checkedFrom.label'),
      values: [formatDay(filters.availabilityCheckedFrom)],
    });
  }
  if (filters.hasCv) {
    groups.push({
      label: t('search.criteria.cv'),
      values: [t(filters.hasCv === 'yes' ? 'search.criteria.cv.yes' : 'search.criteria.cv.no')],
    });
  }
  for (const group of CATALOG_FAMILY_ORDER.map((kind) => CRITERIA_GROUPS[kind])) {
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
