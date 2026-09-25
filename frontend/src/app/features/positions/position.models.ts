import type { SearchFilters } from '../search/models/search.models';

export type PositionStatus = 'open' | 'closed';
export interface PositionListItem {
  id: string;
  title: string;
  location: string;
  status: PositionStatus;
  updatedAtUtc: string;
  version: number;
  /** KTL-30: how many candidates have been added to the position. */
  candidateCount: number;
}
export interface PositionPage {
  items: PositionListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface Position extends Omit<PositionListItem, 'candidateCount'> {
  description: string;
  requirements: SearchFilters;
  createdAtUtc: string;
}
export interface PositionDraft {
  title: string;
  description: string;
  location: string;
  status: PositionStatus;
  requirements: SearchFilters;
}
/** KTL-30. The closed stage vocabulary in display order, mirroring `PositionCandidateStages`. */
export const POSITION_CANDIDATE_STAGES = [
  'new',
  'shortlisted',
  'interview',
  'hired',
  'rejected',
] as const;
export type PositionCandidateStage = (typeof POSITION_CANDIDATE_STAGES)[number];

/** A position's link to one candidate: the search result's contact columns, no documents or notes. */
export interface PositionCandidate {
  candidateId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  hasPrimaryCv: boolean;
  candidateIsActive: boolean;
  stage: PositionCandidateStage;
  addedAtUtc: string;
  updatedAtUtc: string;
  version: number;
}

/** A candidate's link to one position: no description or requirements. */
export interface CandidatePosition {
  positionId: string;
  title: string;
  positionStatus: PositionStatus;
  stage: PositionCandidateStage;
  addedAtUtc: string;
  updatedAtUtc: string;
  version: number;
}
export interface PositionListQuery {
  status?: PositionStatus | 'all';
  text?: string;
  page?: number;
  pageSize?: number;
  sortField?: 'title' | 'location' | 'status' | 'updatedAt';
  sortDirection?: 'asc' | 'desc';
}
