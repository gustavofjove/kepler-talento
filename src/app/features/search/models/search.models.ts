import { CandidateStatus } from '../../candidates/models/candidate.models';

export type MultiValueMode = 'ANY' | 'ALL';

export interface SearchFilters {
  text: string;
  statusValues: CandidateStatus[];
  languageValues: string[];
  languageMode: MultiValueMode;
  programValues: string[];
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
  updatedAt: string;
}

export const EMPTY_SEARCH_FILTERS: SearchFilters = {
  text: '',
  statusValues: [],
  languageValues: [],
  languageMode: 'ANY',
  programValues: [],
  programMode: 'ANY',
  hasCv: '',
};
