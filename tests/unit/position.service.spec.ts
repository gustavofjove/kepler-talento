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
});
