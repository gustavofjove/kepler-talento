import type { ApiTransport } from '../../core/http/api-transport';
import { signal } from '../../core/state/signal';
import type { Position, PositionDraft, PositionListQuery, PositionPage } from './position.models';

const EMPTY_PAGE: PositionPage = { items: [], page: 1, pageSize: 25, totalCount: 0 };

export class PositionService {
  readonly state = signal<PositionPage>(EMPTY_PAGE);
  constructor(private readonly api: ApiTransport) {}
  async list(query: PositionListQuery = {}): Promise<PositionPage> {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(query))
      if (value !== undefined && value !== '') params.set(key, String(value));
    const page = await this.api.request<PositionPage>(`/positions?${params}`);
    this.state.set(page);
    return page;
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
}
