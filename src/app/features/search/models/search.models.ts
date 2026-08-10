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
  statusValues: [],
  languageValues: [],
  languageMode: 'ANY',
  programValues: [],
  programMode: 'ANY',
  hasCv: '',
};
