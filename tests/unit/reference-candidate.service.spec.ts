import { ApiTransport } from '../../src/app/core/http/api-transport';
import { ReferenceCandidateService } from '../../src/app/features/reference/reference-candidate.service';

describe('ReferenceCandidateService', () => {
  it('stores the API result in its subscribed signal', async () => {
    const transport = {
      request: vi.fn().mockResolvedValue({
        id: '1',
        firstName: 'Candidata',
        lastName: 'Sintética',
        isActive: true,
      }),
    } as unknown as ApiTransport;
    const service = new ReferenceCandidateService(transport);
    const listener = vi.fn();
    const unsubscribe = service.candidate.subscribe(listener);
    await service.load('1');
    expect(listener).toHaveBeenCalledOnce();
    expect(service.candidate()?.lastName).toBe('Sintética');
    expect(service.loading()).toBe(false);
    unsubscribe();
  });

  it('does not replace candidate state after cancellation', async () => {
    const transport = {
      request: vi.fn().mockRejectedValue(new Error('cancelled')),
    } as unknown as ApiTransport;
    const service = new ReferenceCandidateService(transport);
    await expect(service.load('1')).rejects.toThrow('cancelled');
    expect(service.candidate()).toBeNull();
    expect(service.loading()).toBe(false);
  });
});
