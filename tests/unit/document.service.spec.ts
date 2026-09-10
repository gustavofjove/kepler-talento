import type { ApiTransport } from '../../src/app/core/http/api-transport';
import type { CandidateDocument } from '../../src/app/features/candidates/models/candidate.models';
import type { CandidateService } from '../../src/app/features/candidates/services/candidate.service';
import { DocumentService } from '../../src/app/features/documents/services/document.service';

const candidateId = 'candidate-1';
const pending: CandidateDocument = {
  id: 'document-1',
  documentType: 'CV',
  originalFilename: 'cv.pdf',
  mimeType: 'application/pdf',
  sizeBytes: 9,
  isPrimary: true,
  uploadedAt: '2026-09-10T00:00:00Z',
  scanState: 'PendingScan',
  availabilityState: 'Pending',
};

describe('DocumentService', () => {
  let service: DocumentService;
  let request: ReturnType<typeof vi.fn>;
  let download: ReturnType<typeof vi.fn>;
  let refreshAggregate: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    request = vi.fn().mockResolvedValue(pending);
    download = vi.fn();
    refreshAggregate = vi.fn().mockResolvedValue(undefined);
    service = new DocumentService(
      { refreshAggregate } as unknown as CandidateService,
      { request, download } as unknown as ApiTransport,
    );
  });

  it('transmits the selected file bytes as FormData without setting Content-Type', async () => {
    const file = new File(['%PDF-test'], 'cv.pdf', { type: 'application/pdf' });

    await expect(service.upload({ candidateId, file, isPrimary: true })).resolves.toEqual(pending);

    const [path, options] = request.mock.calls[0] as [string, RequestInit];
    expect(path).toBe('/candidates/candidate-1/documents');
    expect(options.method).toBe('POST');
    expect(options.headers).toBeUndefined();
    expect(options.body).toBeInstanceOf(FormData);
    const form = options.body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('documentType')).toBe('CV');
    expect(form.get('isPrimary')).toBe('true');
    expect(refreshAggregate).toHaveBeenCalledWith(candidateId);
  });

  it.each(['doc', 'docx', 'odt', 'rtf', 'txt', 'jpg', 'png', 'tiff', 'bmp'])(
    'accepts .%s as a courtesy check and leaves content validation to the API',
    async (extension) => {
      await service.upload({
        candidateId,
        file: new File(['content'], `cv.${extension}`),
        isPrimary: false,
      });
      expect(request).toHaveBeenCalledOnce();
    },
  );

  it('rejects an empty, oversized or disallowed file before transmission', async () => {
    await expect(
      service.upload({ candidateId, file: new File([], 'empty.pdf'), isPrimary: false }),
    ).rejects.toThrow('vacío');
    await expect(
      service.upload({
        candidateId,
        file: new File([new Uint8Array(20 * 1024 * 1024 + 1)], 'large.pdf'),
        isPrimary: false,
      }),
    ).rejects.toThrow('20 MB');
    await expect(
      service.upload({ candidateId, file: new File(['x'], 'cv.svg'), isPrimary: false }),
    ).rejects.toThrow('no está permitido');
    expect(request).not.toHaveBeenCalled();
  });

  it('uses the six document routes', async () => {
    request.mockResolvedValueOnce([pending]);
    await service.list(candidateId);
    await service.get(candidateId, pending.id);
    await service.setPrimary(candidateId, pending.id);
    request.mockResolvedValueOnce(undefined);
    await service.remove(candidateId, pending.id);

    expect(request.mock.calls.map(([path, options]) => [path, options?.method])).toEqual([
      ['/candidates/candidate-1/documents', undefined],
      ['/candidates/candidate-1/documents/document-1', undefined],
      ['/candidates/candidate-1/documents/document-1/primary', 'PUT'],
      ['/candidates/candidate-1/documents/document-1', 'DELETE'],
    ]);
  });

  it('downloads through the transport and saves its sanitised filename', async () => {
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:download');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    download.mockResolvedValue({
      blob: new Blob(['bytes']),
      fileName: 'cv_seguro.pdf',
      contentType: 'application/pdf',
    });

    await service.download(candidateId, pending.id, 'cv.pdf');

    expect(download).toHaveBeenCalledWith(
      '/candidates/candidate-1/documents/document-1/content',
      'cv.pdf',
    );
    expect(click).toHaveBeenCalledOnce();
  });

  it('polls with bounded backoff and stops at the settled state', async () => {
    vi.useFakeTimers();
    const clean = {
      ...pending,
      scanState: 'Clean' as const,
      availabilityState: 'Available' as const,
    };
    request.mockResolvedValueOnce(pending).mockResolvedValueOnce(clean);
    const updates = vi.fn();
    const observation = service.observeUntilSettled(
      candidateId,
      pending.id,
      updates,
      new AbortController().signal,
    );

    await vi.advanceTimersByTimeAsync(1_000);
    await vi.advanceTimersByTimeAsync(2_000);

    await expect(observation).resolves.toEqual(clean);
    expect(updates).toHaveBeenCalledTimes(2);
    vi.useRealTimers();
  });

  it('issues no polling request after cancellation', async () => {
    vi.useFakeTimers();
    const controller = new AbortController();
    const observation = service.observeUntilSettled(
      candidateId,
      pending.id,
      vi.fn(),
      controller.signal,
    );
    controller.abort();
    await expect(observation).rejects.toBeDefined();
    await vi.runAllTimersAsync();
    expect(request).not.toHaveBeenCalled();
    vi.useRealTimers();
  });
});
