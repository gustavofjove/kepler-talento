import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CandidateCvPreview } from '../../src/app/features/candidates/components/candidate-cv-preview';
import { FIT_WIDTH_FRAGMENT } from '../../src/app/features/candidates/components/candidate-cv-preview.logic';
import type {
  Candidate,
  CandidateDocument,
} from '../../src/app/features/candidates/models/candidate.models';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';

const pdf = (overrides: Partial<CandidateDocument> = {}): CandidateDocument => ({
  id: 'pdf',
  documentType: 'CV',
  originalFilename: 'cv.pdf',
  mimeType: 'application/pdf',
  sizeBytes: 10,
  isPrimary: true,
  uploadedAt: '2026-01-01T00:00:00Z',
  availabilityState: 'Available',
  ...overrides,
});

const candidate = (documents: CandidateDocument[]): Candidate => ({
  id: 'candidate',
  firstName: 'Ana',
  lastName: 'López',
  phone: '',
  email: '',
  location: '',
  province: '',
  country: 'España',
  availability: '',
  status: 'new',
  source: '',
  notes: '',
  receivedAt: '',
  consentAt: '',
  reviewDueAt: '',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  version: 1,
  documentCount: documents.length,
  primaryDocumentId: documents.find((item) => item.isPrimary)?.id ?? null,
  languages: [],
  programs: [],
  education: [],
  experience: [],
  skills: [],
  tags: [],
  customNotes: [],
  documents,
});

describe('CandidateCvPreview', () => {
  const openPreview = vi.fn();
  const download = vi.fn();
  const list = vi.fn();
  const observeUntilSettled = vi.fn();
  const createObjectURL = vi.fn(() => 'blob:preview');
  const revokeObjectURL = vi.fn();

  const renderPreview = (
    documents: CandidateDocument[],
    permitted = true,
    options: Partial<ComponentProps<typeof CandidateCvPreview>> = {},
  ) => {
    services.authService.profile.set({
      id: 'user',
      displayName: 'User',
      email: 'user@example.test',
      role: 'recruiter',
      roleLabel: 'Recruiter',
      isActive: true,
      permissions: permitted ? ['documents.download'] : [],
    });
    list.mockResolvedValue(documents);
    const documentService = {
      openPreview,
      download,
      list,
      observeUntilSettled,
    } as unknown as DocumentService;
    const renderCandidate = (current: CandidateDocument[]) => (
      <ServicesProvider value={{ ...services, documentService } as Services}>
        <CandidateCvPreview
          candidateId={candidate(current).id}
          initialDocuments={current}
          {...options}
        />
      </ServicesProvider>
    );
    const view = render(renderCandidate(documents));
    return {
      ...view,
      rerenderCandidate: (current: CandidateDocument[]) => view.rerender(renderCandidate(current)),
    };
  };

  beforeEach(() => {
    vi.clearAllMocks();
    openPreview.mockResolvedValue({
      blob: new Blob(['pdf']),
      fileName: 'cv.pdf',
      contentType: 'application/pdf',
    });
    download.mockResolvedValue(undefined);
    observeUntilSettled.mockResolvedValue(undefined);
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
  });

  afterEach(() => {
    act(() => services.authService.profile.set(null));
    vi.unstubAllGlobals();
  });

  it('renders a clean PDF and revokes its URL on unmount', async () => {
    const view = renderPreview([pdf()]);
    expect(await screen.findByTestId('cv-preview-viewer')).toHaveAttribute(
      'data',
      `blob:preview${FIT_WIDTH_FRAGMENT}`,
    );
    expect(screen.getByTestId('cv-preview-viewer')).toHaveAttribute(
      'title',
      'Vista previa del documento PDF',
    );
    expect(openPreview).toHaveBeenCalledOnce();
    view.unmount();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview');
  });

  it('is absent without the permission', () => {
    renderPreview([pdf()], false);
    expect(screen.queryByTestId('candidate-cv-preview')).not.toBeInTheDocument();
    expect(openPreview).not.toHaveBeenCalled();
  });

  it.each(['Pending', 'Error', 'Refused', 'LegacyUnavailable'] as const)(
    'does not fetch a %s document',
    (availabilityState) => {
      renderPreview([pdf({ availabilityState })]);
      expect(screen.getByTestId('cv-preview-unsupported')).toBeInTheDocument();
      expect(openPreview).not.toHaveBeenCalled();
    },
  );

  it('does not fetch an unsupported format and preserves download', async () => {
    renderPreview([pdf({ mimeType: 'application/msword', originalFilename: 'cv.doc' })]);
    expect(screen.getByTestId('cv-preview-unsupported')).toBeInTheDocument();
    expect(openPreview).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Descargar' }));
    expect(download).toHaveBeenCalledOnce();
  });

  it('switches documents, revokes each active URL and reuses cached bytes', async () => {
    createObjectURL
      .mockReturnValueOnce('blob:first')
      .mockReturnValueOnce('blob:second')
      .mockReturnValueOnce('blob:first-again');
    renderPreview([pdf({ id: 'first' }), pdf({ id: 'second', isPrimary: false })]);
    await screen.findByTestId('cv-preview-viewer');
    const picker = screen.getByTestId('preview-document-select');
    await userEvent.selectOptions(picker, 'second');
    await waitFor(() =>
      expect(screen.getByTestId('cv-preview-viewer')).toHaveAttribute(
        'data',
        `blob:second${FIT_WIDTH_FRAGMENT}`,
      ),
    );
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:first');
    await userEvent.selectOptions(picker, 'first');
    await waitFor(() =>
      expect(screen.getByTestId('cv-preview-viewer')).toHaveAttribute(
        'data',
        `blob:first-again${FIT_WIDTH_FRAGMENT}`,
      ),
    );
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:second');
    expect(openPreview).toHaveBeenCalledTimes(2);
  });

  it('does not restart a preview when refreshed metadata has the same stable fields', async () => {
    let resolvePreview!: (value: { blob: Blob; fileName: string; contentType: string }) => void;
    openPreview.mockReturnValueOnce(
      new Promise((resolve) => {
        resolvePreview = resolve;
      }),
    );
    const document = pdf();
    list.mockResolvedValueOnce([{ ...document }]);
    renderPreview([document]);
    await waitFor(() => expect(list).toHaveBeenCalledOnce());
    expect(openPreview).toHaveBeenCalledOnce();
    resolvePreview({
      blob: new Blob(['pdf']),
      fileName: 'cv.pdf',
      contentType: 'application/pdf',
    });
    await screen.findByTestId('cv-preview-viewer');
    expect(openPreview).toHaveBeenCalledOnce();
  });

  it('preserves the active preview across same-candidate aggregate refreshes', async () => {
    const document = pdf();
    const view = renderPreview([document]);
    expect(await screen.findByTestId('cv-preview-viewer')).toHaveAttribute(
      'data',
      `blob:preview${FIT_WIDTH_FRAGMENT}`,
    );
    view.rerenderCandidate([{ ...document, sizeBytes: 20 }]);
    await waitFor(() => expect(list).toHaveBeenCalledTimes(2));
    expect(screen.getByTestId('cv-preview-viewer')).toHaveAttribute(
      'data',
      `blob:preview${FIT_WIDTH_FRAGMENT}`,
    );
    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(revokeObjectURL).not.toHaveBeenCalled();
  });

  it('polls a pending document and previews it when scanning settles', async () => {
    const pending = pdf({ availabilityState: 'Pending', isPrimary: false });
    observeUntilSettled.mockImplementationOnce(
      async (
        _candidateId: string,
        _documentId: string,
        onUpdate: (document: CandidateDocument) => void,
      ) => {
        const available = { ...pending, availabilityState: 'Available' as const };
        onUpdate(available);
        return available;
      },
    );
    renderPreview([pending]);
    expect(await screen.findByTestId('cv-preview-viewer')).toBeInTheDocument();
    expect(observeUntilSettled).toHaveBeenCalledOnce();
    expect(openPreview).toHaveBeenCalledOnce();
  });

  it('shows loading, then failure, and retries exactly once', async () => {
    let reject!: (reason: Error) => void;
    openPreview.mockReturnValueOnce(
      new Promise((_resolve, rejectPromise) => {
        reject = rejectPromise;
      }),
    );
    renderPreview([pdf()]);
    expect(screen.getByText('Cargando vista previa…')).toHaveAttribute('aria-busy', 'true');
    reject(new Error('failed'));
    await screen.findByRole('button', { name: 'Reintentar' });
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    await screen.findByTestId('cv-preview-viewer');
    expect(openPreview).toHaveBeenCalledTimes(2);
  });

  describe('candidate page gate (KTL-35)', () => {
    const gated = { requirePreviewablePrimary: true };

    it('shows a clean PDF primary', async () => {
      renderPreview([pdf()], true, gated);
      expect(await screen.findByTestId('cv-preview-viewer')).toBeInTheDocument();
    });

    it.each([
      ['a clean .docx primary', [pdf({ mimeType: 'application/msword' })]],
      ['a refused primary', [pdf({ availabilityState: 'Refused' })]],
      ['a legacy primary without a binary', [pdf({ availabilityState: 'LegacyUnavailable' })]],
      ['only non-primary documents', [pdf({ isPrimary: false })]],
    ])('shows nothing and requests no content for %s', async (_case, documents) => {
      renderPreview(documents, true, gated);
      await waitFor(() => expect(list).toHaveBeenCalledOnce());
      expect(screen.queryByTestId('candidate-cv-preview')).not.toBeInTheDocument();
      expect(openPreview).not.toHaveBeenCalled();
    });

    it('appears once a pending primary settles clean', async () => {
      const pending = pdf({ availabilityState: 'Pending' });
      observeUntilSettled.mockImplementationOnce(
        async (
          _candidateId: string,
          _documentId: string,
          onUpdate: (document: CandidateDocument) => void,
        ) => {
          await Promise.resolve();
          const available = { ...pending, availabilityState: 'Available' as const };
          onUpdate(available);
          return available;
        },
      );
      renderPreview([pending], true, gated);
      expect(screen.queryByTestId('candidate-cv-preview')).not.toBeInTheDocument();
      expect(await screen.findByTestId('cv-preview-viewer')).toBeInTheDocument();
      expect(openPreview).toHaveBeenCalledOnce();
    });

    it('previews the primary even when a newer non-primary PDF exists', async () => {
      renderPreview(
        [pdf(), pdf({ id: 'newer', isPrimary: false, uploadedAt: '2026-06-01T00:00:00Z' })],
        true,
        gated,
      );
      await screen.findByTestId('cv-preview-viewer');
      expect(openPreview.mock.calls[0]?.[1]).toBe('pdf');
    });
  });

  describe('row CV mode (KTL-35)', () => {
    it('shows its empty message rather than nothing once the list is known to be empty', async () => {
      list.mockResolvedValue([]);
      services.authService.profile.set({
        id: 'user',
        displayName: 'User',
        email: 'user@example.test',
        role: 'recruiter',
        roleLabel: 'Recruiter',
        isActive: true,
        permissions: ['documents.download'],
      });
      render(
        <ServicesProvider
          value={
            {
              ...services,
              documentService: { openPreview, download, list, observeUntilSettled },
            } as unknown as Services
          }
        >
          <CandidateCvPreview candidateId="candidate" emptyMessage="Sin CV" />
        </ServicesProvider>,
      );
      expect(await screen.findByTestId('cv-preview-empty')).toHaveTextContent('Sin CV');
      expect(openPreview).not.toHaveBeenCalled();
    });

    it('reuses content from an external cache without a new request', async () => {
      const cache = new Map<string, Blob>([['pdf', new Blob(['cached'])]]);
      renderPreview([pdf()], true, {
        contentCache: {
          get: (id) => cache.get(id),
          set: (id, blob) => void cache.set(id, blob),
        },
      });
      expect(await screen.findByTestId('cv-preview-viewer')).toBeInTheDocument();
      expect(openPreview).not.toHaveBeenCalled();
    });
  });

  it('aborts an in-flight request on unmount', () => {
    openPreview.mockReturnValue(new Promise(() => undefined));
    const view = renderPreview([pdf()]);
    const signal = openPreview.mock.calls[0]?.[3] as AbortSignal;
    view.unmount();
    expect(signal.aborted).toBe(true);
  });
});
