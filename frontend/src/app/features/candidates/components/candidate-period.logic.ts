import type { TFunction } from 'i18next';
import type { CandidateEducation, CandidateExperience } from '../models/candidate.models';

const yearOf = (date: string | undefined): number | undefined => {
  const year = Number(date?.slice(0, 4));
  return Number.isInteger(year) && year > 0 ? year : undefined;
};

/** «2019–2024», or the one year known (also when both are the same); undefined without either. */
function yearRange(t: TFunction, start?: number, end?: number): string | undefined {
  if (start !== undefined && end !== undefined && start !== end) {
    return t('candidate.profile.period.range', { start, end });
  }
  const only = end ?? start;
  return only === undefined ? undefined : String(only);
}

/** Education keeps only an end year, so its period is that year. */
export function educationPeriod(entry: CandidateEducation, t: TFunction): string | undefined {
  return yearRange(t, undefined, entry.endYear);
}

/**
 * The years an experience spans, from its dates. A current role runs «2019–actualidad»; one
 * with no start date has no period, and its row keeps the «Actual» marker instead.
 */
export function experiencePeriod(entry: CandidateExperience, t: TFunction): string | undefined {
  const start = yearOf(entry.startDate);
  if (entry.isCurrent) {
    return start === undefined ? undefined : t('candidate.profile.period.current', { start });
  }
  return yearRange(t, start, yearOf(entry.endDate));
}
