import type { ApiTransport } from '../../src/app/core/http/api-transport';
import { TranslatableError } from '../../src/app/core/i18n/translatable-error';
import { CandidateDraftService } from '../../src/app/features/candidates/services/candidate-draft.service';
import { AppError } from '../../src/app/shared/models/error.models';

const pdf = () => new File(['%PDF-test'], 'cv.pdf', { type: 'application/pdf' });

describe('CandidateDraftService (KTL-32)', () => {
  let request: ReturnType<typeof vi.fn>;
  let service: CandidateDraftService;

  beforeEach(() => {
    request = vi.fn().mockResolvedValue({
      draftId: 'd1',
      outcome: 'cv_draft.extracted',
      fields: { firstName: { value: ' Ana ', confidence: 'high' } },
    });
    service = new CandidateDraftService({ request } as unknown as ApiTransport);
  });

  it('posts the file as FormData with a timeout that allows for the scan', async () => {
    const file = pdf();

    await service.extract(file);

    const [path, options] = request.mock.calls[0] as [string, RequestInit & { timeoutMs: number }];
    expect(path).toBe('/candidates/draft-from-document');
    expect(options.method).toBe('POST');
    expect((options.body as FormData).get('file')).toBe(file);
    expect(options.timeoutMs).toBeGreaterThanOrEqual(60_000);
  });

  it('keeps only the six form fields, trimmed, whatever the API sends', async () => {
    request.mockResolvedValue({
      draftId: 'd1',
      outcome: 'cv_draft.extracted',
      fields: {
        firstName: { value: ' Ana ', confidence: 'high' },
        phone: { value: '   ', confidence: 'low' },
        notes: { value: 'no', confidence: 'high' },
        email: { value: 'ana@example.test', confidence: 'weird' },
      },
    });

    const draft = await service.extract(pdf());

    expect(draft.fields).toEqual({
      firstName: { value: 'Ana', confidence: 'high' },
      email: { value: 'ana@example.test', confidence: 'low' },
    });
  });

  it.each([
    ['cv.doc', 9, 'candidate.cvDraft.error.format'],
    ['cv.pdf', 0, 'candidate.cvDraft.error.empty'],
    ['cv.pdf', 20 * 1024 * 1024 + 1, 'candidate.cvDraft.error.size'],
  ])('refuses %s of %i bytes before calling the API', async (name, size, key) => {
    const file = new File(['x'], name);
    Object.defineProperty(file, 'size', { value: size });

    await expect(service.extract(file)).rejects.toMatchObject({ key });
    expect(request).not.toHaveBeenCalled();
  });

  it.each([
    [
      new AppError('VALIDATION_ERROR', 'x', { errors: [{ code: 'cv_draft.format.unsupported' }] }),
      'candidate.cvDraft.error.format',
    ],
    [
      new AppError('VALIDATION_ERROR', 'x', undefined, undefined, 'cv_draft.rejected'),
      'candidate.cvDraft.error.unreadable',
    ],
    [
      new AppError('VALIDATION_ERROR', 'x', undefined, undefined, 'cv_draft.too_complex'),
      'candidate.cvDraft.error.tooComplex',
    ],
    [
      new AppError('INTERNAL_ERROR', 'x', undefined, undefined, 'cv_draft.scanner_unavailable'),
      'candidate.cvDraft.error.scanner',
    ],
    [
      new AppError('RATE_LIMITED', 'x', undefined, undefined, 'cv_draft.busy'),
      'candidate.cvDraft.error.busy',
    ],
    [new AppError('TIMEOUT'), 'candidate.cvDraft.error.timeout'],
  ])('explains a known refusal in the page’s own words', async (error, key) => {
    request.mockRejectedValue(error);

    const failure = await service.extract(pdf()).catch((caught: unknown) => caught);

    expect(failure).toBeInstanceOf(TranslatableError);
    expect(failure).toMatchObject({ key });
  });

  it('passes an unknown failure through untouched', async () => {
    const error = new AppError('FORBIDDEN');
    request.mockRejectedValue(error);

    await expect(service.extract(pdf())).rejects.toBe(error);
  });
});
