import { render, screen, waitFor } from '@testing-library/react';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateDocuments } from '../../src/app/features/candidates/components/candidate-documents';
import type {
  Candidate,
  CandidateDocument,
} from '../../src/app/features/candidates/models/candidate.models';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';

const document = (
  availabilityState: CandidateDocument['availabilityState'],
): CandidateDocument => ({
  id: `document-${availabilityState}`,
  documentType: 'CV',
  originalFilename: 'cv.pdf',
  mimeType: 'application/pdf',
  sizeBytes: 10,
  isPrimary: false,
  uploadedAt: '2026-09-10T00:00:00Z',
  scanState: availabilityState === 'Available' ? 'Clean' : 'PendingScan',
  availabilityState,
});

const candidate: Candidate = {
  id: 'candidate-1',
  firstName: 'Ana',
  lastName: 'Gil',
  phone: '',
  email: '',
  location: '',
  province: '',
  country: 'España',
  availability: 'Inmediata',
  status: 'new',
  source: 'Email',
  notes: '',
  receivedAt: '',
  consentAt: '',
  reviewDueAt: '',
  isActive: true,
  createdAt: '2026-09-10T00:00:00Z',
  updatedAt: '2026-09-10T00:00:00Z',
  version: 1,
  documentCount: 0,
  primaryDocumentId: null,
  languages: [],
  programs: [],
  education: [],
  experience: [],
  skills: [],
  documents: [],
};

describe('CandidateDocuments', () => {
  const renderDocuments = (
    listed: CandidateDocument[],
    observe = vi.fn().mockResolvedValue(undefined),
  ) => {
    const documentService = {
      list: vi.fn().mockResolvedValue(listed),
      observeUntilSettled: observe,
      download: vi.fn(),
      upload: vi.fn(),
      setPrimary: vi.fn(),
      remove: vi.fn(),
    } as unknown as DocumentService;
    const authService = {
      profile: signal(null),
      hasPermission: vi.fn().mockReturnValue(true),
    };
    const view = render(
      <ServicesProvider
        value={{ ...services, documentService, authService } as unknown as Services}
      >
        <CandidateDocuments candidate={candidate} />
      </ServicesProvider>,
    );
    return { ...view, documentService };
  };

  it('renders every Spanish availability state and hides non-clean downloads', async () => {
    renderDocuments([
      document('Pending'),
      { ...document('Refused'), id: 'refused' },
      { ...document('Error'), id: 'error' },
      { ...document('LegacyUnavailable'), id: 'legacy' },
      { ...document('Available'), id: 'available' },
    ]);

    expect(await screen.findByText('En análisis')).toBeInTheDocument();
    expect(screen.getAllByText('No disponible')).toHaveLength(2);
    expect(screen.getByText('Error de análisis')).toBeInTheDocument();
    expect(screen.getByText('Disponible')).toBeInTheDocument();
    expect(
      screen.getByText('El archivo no ha superado el análisis de seguridad.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Documento heredado sin archivo asociado.')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Descargar' })).toHaveLength(1);
  });

  it('aborts pending observation when the view unmounts', async () => {
    let observedSignal: AbortSignal | undefined;
    const observe = vi.fn((_: string, __: string, ___: unknown, signal: AbortSignal) => {
      observedSignal = signal;
      return new Promise(() => undefined);
    });
    const view = renderDocuments([document('Pending')], observe);
    await waitFor(() => expect(observe).toHaveBeenCalledOnce());

    view.unmount();

    expect(observedSignal?.aborted).toBe(true);
  });
});
