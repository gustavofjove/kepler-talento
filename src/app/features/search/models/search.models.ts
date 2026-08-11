import { CandidateStatus } from '../../candidates/models/candidate.models';

export type MultiValueMode = 'ANY' | 'ALL';

/** Every status, in display order; also the default selection of the status filter. */
export const ALL_CANDIDATE_STATUSES: CandidateStatus[] = [
  'new',
  'available',
  'in_process',
  'hired',
  'rejected',
];

/** A single filter line: a catalog value plus an optional level ('' = cualquier nivel). */
export interface CriteriaFilter {
  value: string;
  level: string;
}

export interface SearchFilters {
  text: string;
  statusValues: CandidateStatus[];
  skillCriteria: CriteriaFilter[];
  skillMode: MultiValueMode;
  languageCriteria: CriteriaFilter[];
  languageMode: MultiValueMode;
  programCriteria: CriteriaFilter[];
  programMode: MultiValueMode;
  hasCv: '' | 'yes' | 'no';
}

export interface SearchResult {
  candidateId: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string;
  status: CandidateStatus;
  hasPrimaryCv: boolean;
  primaryCvDocumentId?: string;
  updatedAt: string;
}

export interface SearchPreset {
  id: string;
  name: string;
  filters: SearchFilters;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
}

export const EMPTY_SEARCH_FILTERS: SearchFilters = {
  text: '',
  statusValues: [...ALL_CANDIDATE_STATUSES],
  skillCriteria: [],
  skillMode: 'ANY',
  languageCriteria: [],
  languageMode: 'ANY',
  programCriteria: [],
  programMode: 'ANY',
  hasCv: '',
};

export function cloneSearchFilters(filters: SearchFilters): SearchFilters {
  return {
    text: filters.text,
    statusValues: [...filters.statusValues],
    skillCriteria: filters.skillCriteria.map((item) => ({ ...item })),
    skillMode: filters.skillMode,
    languageCriteria: filters.languageCriteria.map((item) => ({ ...item })),
    languageMode: filters.languageMode,
    programCriteria: filters.programCriteria.map((item) => ({ ...item })),
    programMode: filters.programMode,
    hasCv: filters.hasCv,
  };
}
