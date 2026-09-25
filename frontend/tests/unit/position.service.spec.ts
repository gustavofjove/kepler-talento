import { PositionService } from '../../src/app/features/positions/position.service';
import { EMPTY_SEARCH_FILTERS } from '../../src/app/features/search/models/search.models';

describe('PositionService', () => {
  it('encodes bounded list options and publishes the returned page', async () => {
    const page = { items: [], page: 2, pageSize: 50, totalCount: 75 };
    const api = { request: vi.fn().mockResolvedValue(page) };
    const service = new PositionService(api as never);
    await service.list({
      status: 'closed',
      text: ' senior ',
      page: 2,
      pageSize: 50,
      sortField: 'title',
      sortDirection: 'asc',
    });
    const [path] = api.request.mock.calls[0] as [string];
    const query = new URLSearchParams(path.split('?')[1]);
    expect(query.get('status')).toBe('closed');
    expect(query.get('text')).toBe(' senior ');
    expect(query.get('pageSize')).toBe('50');
    expect(service.state()).toEqual(page);
  });

  it('creates through the API without adding status or browser persistence', async () => {
    const api = { request: vi.fn().mockResolvedValue({ id: 'p-1' }) };
    const service = new PositionService(api as never);
    await service.create({
      title: 'Analista',
      location: '',
      description: '<p>Texto</p>',
      requirements: structuredClone(EMPTY_SEARCH_FILTERS),
    });
    expect(api.request).toHaveBeenCalledWith(
      '/positions',
      expect.objectContaining({ method: 'POST' }),
    );
    expect(JSON.parse(api.request.mock.calls[0][1].body)).not.toHaveProperty('status');
  });

  it('updates against the last-read version', async () => {
    const api = { request: vi.fn().mockResolvedValue({ id: 'p-1' }) };
    const service = new PositionService(api as never);
    await service.update(
      'p-1',
      {
        title: 'Analista',
        location: 'Madrid',
        description: '',
        status: 'closed',
        requirements: structuredClone(EMPTY_SEARCH_FILTERS),
      },
      7,
    );
    expect(api.request).toHaveBeenCalledWith(
      '/positions/p-1',
      expect.objectContaining({ method: 'PUT' }),
    );
    expect(JSON.parse(api.request.mock.calls[0][1].body)).toMatchObject({
      status: 'closed',
      version: 7,
    });
  });

  it('searches positions for a picker without replacing the list page state', async () => {
    const page = { items: [], page: 1, pageSize: 10, totalCount: 0 };
    const api = { request: vi.fn().mockResolvedValue(page) };
    const service = new PositionService(api as never);
    const before = service.state();
    const controller = new AbortController();

    await service.search({ status: 'open', text: 'java', pageSize: 10 }, controller.signal);

    const [path, options] = api.request.mock.calls[0] as [string, { signal: AbortSignal }];
    expect(new URLSearchParams(path.split('?')[1]).get('status')).toBe('open');
    expect(options.signal).toBe(controller.signal);
    expect(service.state()).toBe(before);
  });

  // ---- KTL-30 position candidate links ----

  it('lists, adds, restages and removes links through the API only', async () => {
    const api = { request: vi.fn().mockResolvedValue({}) };
    const service = new PositionService(api as never);

    await service.listCandidates('p-1');
    await service.addCandidate('p-1', 'c-1');
    await service.changeStage('p-1', 'c-1', 'interview', 3);
    await service.removeCandidate('p-1', 'c-1');
    await service.listForCandidate('c-1');

    expect(api.request.mock.calls.map(([path, options]) => [path, options?.method])).toEqual([
      ['/positions/p-1/candidates', undefined],
      ['/positions/p-1/candidates', 'POST'],
      ['/positions/p-1/candidates/c-1/stage', 'PUT'],
      ['/positions/p-1/candidates/c-1', 'DELETE'],
      ['/candidates/c-1/positions', undefined],
    ]);
    expect(JSON.parse(api.request.mock.calls[1][1].body)).toEqual({ candidateId: 'c-1' });
    expect(JSON.parse(api.request.mock.calls[2][1].body)).toEqual({
      stage: 'interview',
      version: 3,
    });
  });
});
