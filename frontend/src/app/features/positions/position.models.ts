import type { SearchFilters } from '../search/models/search.models';

export type PositionStatus = 'open' | 'closed';
export interface PositionListItem {
  id: string;
  title: string;
  location: string;
  status: PositionStatus;
  updatedAtUtc: string;
  version: number;
}
export interface PositionPage {
  items: PositionListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface Position extends PositionListItem {
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
export interface PositionListQuery {
  status?: PositionStatus | 'all';
  text?: string;
  page?: number;
  pageSize?: number;
  sortField?: 'title' | 'location' | 'status' | 'updatedAt';
  sortDirection?: 'asc' | 'desc';
}
