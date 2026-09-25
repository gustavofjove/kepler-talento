import type { ApiTransport } from '../../core/http/api-transport';
import { signal } from '../../core/state/signal';
import type {
  CandidatePosition,
  Position,
  PositionCandidate,
  PositionCandidateStage,
  PositionDraft,
  PositionListQuery,
  PositionPage,
} from './position.models';

const EMPTY_PAGE: PositionPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };

export class PositionService {
  readonly state = signal<PositionPage>(EMPTY_PAGE);
  constructor(private readonly api: ApiTransport) {}
  async list(query: PositionListQuery = {}): Promise<PositionPage> {
    const page = await this.search(query);
    this.state.set(page);
    return page;
  }
  /** One page of positions without touching the list page's shared `state` (KTL-30 picker). */
  search(query: PositionListQuery = {}, signal?: AbortSignal): Promise<PositionPage> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query))
      if (value !== undefined && value !== '') params.set(key, String(value));
    return this.api.request<PositionPage>(`/positions?${params}`, { signal });
  }
  get(id: string): Promise<Position> {
    return this.api.request<Position>(`/positions/${id}`);
  }
  create(draft: Omit<PositionDraft, 'status'>): Promise<Position> {
    return this.api.request<Position>('/positions', {
      method: 'POST',
      body: JSON.stringify(draft),
    });
  }
  update(id: string, draft: PositionDraft, version: number): Promise<Position> {
    return this.api.request<Position>(`/positions/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ ...draft, version }),
    });
  }

  // KTL-30 position candidate links. Page-local state owns the results, so no signal.
  listCandidates(positionId: string): Promise<PositionCandidate[]> {
    return this.api.request<PositionCandidate[]>(`/positions/${positionId}/candidates`);
  }
  addCandidate(positionId: string, candidateId: string): Promise<PositionCandidate> {
    return this.api.request<PositionCandidate>(`/positions/${positionId}/candidates`, {
      method: 'POST',
      body: JSON.stringify({ candidateId }),
    });
  }
  changeStage(
    positionId: string,
    candidateId: string,
    stage: PositionCandidateStage,
    version: number,
  ): Promise<PositionCandidate> {
    return this.api.request<PositionCandidate>(
      `/positions/${positionId}/candidates/${candidateId}/stage`,
      { method: 'PUT', body: JSON.stringify({ stage, version }) },
    );
  }
  removeCandidate(positionId: string, candidateId: string): Promise<void> {
    return this.api.request<void>(`/positions/${positionId}/candidates/${candidateId}`, {
      method: 'DELETE',
    });
  }
  listForCandidate(candidateId: string): Promise<CandidatePosition[]> {
    return this.api.request<CandidatePosition[]>(`/candidates/${candidateId}/positions`);
  }
}
