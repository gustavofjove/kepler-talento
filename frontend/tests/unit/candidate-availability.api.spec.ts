import type { ApiTransport } from '../../src/app/core/http/api-transport';
import { CandidateApi } from '../../src/app/features/candidates/services/candidate.api';

describe('CandidateApi availability request', () => {
  it('sends the cached version and no checker identity', async () => {
    const request = vi.fn().mockResolvedValue({ id: 'c-1' });
    const api = new CandidateApi({ request } as unknown as ApiTransport);

    await api.recordAvailability(
      'c-1',
      { state: 'unavailable', checkedOn: '2026-09-20', until: '2026-10-20' },
      7,
    );

    expect(request).toHaveBeenCalledWith('/candidates/c-1/availability', {
      method: 'PUT',
      body: JSON.stringify({
        state: 'unavailable',
        checkedOn: '2026-09-20',
        until: '2026-10-20',
        version: 7,
      }),
    });
    expect(request.mock.calls[0][1].body).not.toContain('checkedBy');
  });

  it('omits dates when resetting to unknown', async () => {
    const request = vi.fn().mockResolvedValue({ id: 'c-1' });
    const api = new CandidateApi({ request } as unknown as ApiTransport);
    await api.recordAvailability('c-1', { state: 'unknown', checkedOn: '', until: '' }, 8);
    expect(JSON.parse(request.mock.calls[0][1].body)).toEqual({
      state: 'unknown',
      checkedOn: null,
      until: null,
      version: 8,
    });
  });
});
