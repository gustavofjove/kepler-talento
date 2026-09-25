import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateCreatePage } from '../../src/app/features/candidates/pages/candidate-create-page';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>;
}

const SECTIONS = [
  'candidate-competencies',
  'candidate-education',
  'candidate-experience',
  'candidate-notes',
  'candidate-documents',
  'candidate-cv-preview',
];

describe('CandidateCreatePage (KTL-29)', () => {
  let bed: CandidateTestBed;
  let granted: Set<string>;

  beforeEach(() => {
    vi.clearAllMocks();
    bed = createCandidateTestBed();
    granted = new Set(['candidates.read', 'candidates.create', 'candidates.update']);
  });

  const renderPage = async () => {
    const catalogService = await loadedCatalogService();
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
            catalogService,
            authService,
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={['/app/candidates/new']}>
          <LocationProbe />
          <Routes>
            <Route path="/app/candidates/new" element={<CandidateCreatePage />} />
            <Route path="/app/candidates/:id" element={<p data-testid="candidate-page" />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  const trailText = () =>
    within(screen.getByTestId('breadcrumb'))
      .getAllByRole('listitem')
      .map((item) => item.textContent);

  it('shows only the core form and the post-save hint', async () => {
    await renderPage();

    expect(await screen.findByLabelText('Nombre')).toHaveValue('');
    for (const testId of SECTIONS) expect(screen.queryByTestId(testId)).not.toBeInTheDocument();
    expect(screen.getByTestId('candidate-create-hint')).toHaveTextContent(/Guarda el candidato/);
    expect(trailText()).toEqual(['Candidatos', 'Nuevo candidato']);
    expect(screen.getByText('Nuevo candidato')).toHaveAttribute('aria-current', 'page');
  });

  it.each([
    ['may update', true],
    ['may not update', false],
  ])(
    'opens the new candidate’s page after the first save when the creator %s',
    async (_, update) => {
      if (!update) granted.delete('candidates.update');
      await renderPage();

      await userEvent.type(await screen.findByLabelText('Nombre'), 'Sara');
      await userEvent.type(screen.getByLabelText('Apellidos'), 'Pena');
      await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

      expect(await screen.findByTestId('candidate-page')).toBeInTheDocument();
      await waitFor(() =>
        expect(screen.getByTestId('location').textContent).toMatch(/^\/app\/candidates\/[^/]+$/),
      );
      expect([...bed.api.candidates.values()].map((item) => item.firstName)).toContain('Sara');
    },
  );

  it('renders «Candidatos» as text for a creator who may not read candidates', async () => {
    granted.delete('candidates.read');
    await renderPage();
    await screen.findByLabelText('Nombre');

    expect(within(screen.getByTestId('breadcrumb')).queryByRole('link')).not.toBeInTheDocument();
  });
});
