import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateDetailPage } from '../../src/app/features/candidates/pages/candidate-detail-page';
import { CandidateNotesService } from '../../src/app/features/candidates/services/candidate-notes.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

describe('CandidateDetailPage breadcrumb (KTL-23)', () => {
  let bed: CandidateTestBed;
  let granted: Set<string>;

  beforeEach(() => {
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'c1', firstName: 'Ona', lastName: 'Marti' });
    granted = new Set(['candidates.read']);
  });

  const renderAt = async (path: string) => {
    const catalogService = await loadedCatalogService();
    const documentService = {
      list: vi.fn().mockResolvedValue([]),
      observeUntilSettled: vi.fn(),
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
