import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateDetailPage } from '../../src/app/features/candidates/pages/candidate-detail-page';
import { CandidateNotesService } from '../../src/app/features/candidates/services/candidate-notes.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { CandidateDocument } from '../../src/app/features/candidates/models/candidate.models';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

describe('CandidateDetailPage breadcrumb (KTL-23)', () => {
  let bed: CandidateTestBed;
  let granted: Set<string>;
  let listed: CandidateDocument[];

  beforeEach(() => {
    listed = [];
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'c1', firstName: 'Ona', lastName: 'Marti' });
    granted = new Set(['candidates.read']);
  });

  const renderAt = async (path: string) => {
    const catalogService = await loadedCatalogService();
    const documentService = {
      list: vi.fn().mockResolvedValue(listed),
      observeUntilSettled: vi.fn(),
      // Never settles: the specs only look at where the preview is placed.
      openPreview: vi.fn(() => new Promise(() => {})),
    } as unknown as DocumentService;
    const authService = {
      profile: signal(null),
      hasPermission: vi.fn((permission: string) => granted.has(permission)),
    };
    render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateService: bed.service,
            candidateRelationsService: new CandidateRelationsService(bed.service),
            candidateNotesService: new CandidateNotesService(bed.api),
            catalogService,
            documentService,
            authService,
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/app/candidates/:id" element={<CandidateDetailPage />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  const trail = () => within(screen.getByTestId('breadcrumb'));
  const trailText = () =>
    trail()
      .getAllByRole('listitem')
      .map((item) => item.textContent);

  it('reads «Candidatos › name» with the name as the current page', async () => {
    await renderAt('/app/candidates/c1');
    await screen.findByRole('heading', { level: 1, name: 'Ona Marti' });

    expect(trailText()).toEqual(['Candidatos', 'Ona Marti']);
    expect(trail().getByRole('link', { name: 'Candidatos' })).toHaveAttribute(
      'href',
      '/app/candidates',
    );
    expect(trail().getByText('Ona Marti')).toHaveAttribute('aria-current', 'page');
    expect(trail().getAllByRole('link')).toHaveLength(1);
  });

  it('stacks every section full width, with a read-only Competencias panel (KTL-27)', async () => {
    bed.api.seed({ id: 'c2', firstName: 'Eva', lastName: 'Sanz' });
    await renderAt('/app/candidates/c2');
    await screen.findByRole('heading', { level: 1, name: 'Eva Sanz' });

    const order = [
      'Datos principales',
      'Auditoría',
      'Competencias',
      'Formación',
      'Experiencia',
      'Notas personalizadas',
      'Documentos',
    ];
    const headings = screen
      .getAllByRole('heading')
      .map((heading) => heading.textContent)
      .filter((text) => order.includes(text ?? ''));
    expect(headings).toEqual(order);
    expect(screen.getByTestId('candidate-competencies').parentElement).not.toHaveClass('two');

    const panel = within(screen.getByTestId('candidate-competencies'));
    expect(panel.queryByRole('button')).toBeNull();
    expect(panel.getByText('Sin programas asociados.')).toBeInTheDocument();
  });

  it('places the CV preview in the aside, after the sections (KTL-28)', async () => {
    granted.add('documents.download');
    listed = [
      {
        id: 'd1',
        documentType: 'CV',
        originalFilename: 'cv.pdf',
        mimeType: 'application/pdf',
        sizeBytes: 10,
        isPrimary: true,
        uploadedAt: '2026-01-01T00:00:00Z',
        availabilityState: 'Available',
      },
    ];
    await renderAt('/app/candidates/c1');

    const preview = await screen.findByTestId('candidate-cv-preview');
    expect(preview.parentElement).toHaveClass('page-split__aside');
    expect(preview.closest('.page-split__layout')?.firstElementChild).toHaveClass(
      'page-split__main',
    );
  });

  it('leaves the aside empty without the download permission (KTL-28)', async () => {
    await renderAt('/app/candidates/c1');
    await screen.findByRole('heading', { level: 1, name: 'Ona Marti' });

    expect(screen.queryByTestId('candidate-cv-preview')).not.toBeInTheDocument();
    expect(document.querySelector('.page-split__aside')).toBeEmptyDOMElement();
  });

  it('offers only the list while the candidate loads', async () => {
    await renderAt('/app/candidates/c1');

    expect(trailText()).toEqual(['Candidatos']);
    expect(trail().getByRole('link', { name: 'Candidatos' })).toBeInTheDocument();
    await screen.findByRole('heading', { level: 1, name: 'Ona Marti' });
  });

  it('keeps the way back when the candidate fails to load', async () => {
    bed.api.failure = new Error('Servicio no disponible');
    await renderAt('/app/candidates/c1');

    expect(await screen.findByTestId('candidate-detail-error')).toBeInTheDocument();
    expect(trailText()).toEqual(['Candidatos']);
    expect(trail().getByRole('link', { name: 'Candidatos' })).toHaveAttribute(
      'href',
      '/app/candidates',
    );
  });

  it('keeps the way back when the candidate does not exist', async () => {
    await renderAt('/app/candidates/missing');

    expect(await screen.findByRole('heading', { level: 1 })).not.toHaveTextContent('Ona');
    expect(screen.queryByTestId('candidate-detail-error')).not.toBeInTheDocument();
    expect(trailText()).toEqual(['Candidatos']);
    expect(trail().getByRole('link', { name: 'Candidatos' })).toBeInTheDocument();
  });
});
