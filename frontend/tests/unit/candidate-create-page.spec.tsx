import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { TranslatableError } from '../../src/app/core/i18n/translatable-error';
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
  let extract: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    bed = createCandidateTestBed();
    granted = new Set(['candidates.read', 'candidates.create', 'candidates.update']);
    extract = vi.fn().mockResolvedValue({
      draftId: 'd1',
      outcome: 'cv_draft.extracted',
      fields: {
        firstName: { value: 'Ana', confidence: 'high' },
        lastName: { value: 'Ruiz Gil', confidence: 'low' },
      },
    });
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
            candidateDraftService: { extract },
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

  it('shows only the CV picker, the core form and the post-save hint', async () => {
    await renderPage();

    expect(await screen.findByLabelText('Nombre')).toHaveValue('');
    expect(screen.getByLabelText('Rellenar desde un CV')).toHaveAttribute('accept', '.pdf,.docx');
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

  const pickCv = async () =>
    userEvent.upload(
      screen.getByTestId('cv-draft-file'),
      new File(['%PDF-test'], 'cv.pdf', { type: 'application/pdf' }),
    );

  describe('CV draft (KTL-32)', () => {
    it('fills the empty fields, says how many, and saves nothing by itself', async () => {
      await renderPage();
      await screen.findByLabelText('Nombre');

      await pickCv();

      expect(await screen.findByTestId('cv-draft-status')).toHaveTextContent(
        'Se han rellenado 2 campos a partir del CV',
      );
      expect(screen.getByLabelText('Nombre')).toHaveValue('Ana');
      expect(screen.getByLabelText('Apellidos')).toHaveValue('Ruiz Gil');
      expect(screen.getByTestId('lastName-suggested')).toHaveTextContent('Revisar');
      expect(extract).toHaveBeenCalledTimes(1);
      expect(bed.api.candidates.size).toBe(0);
    });

    it('keeps a value typed before the CV was picked', async () => {
      await renderPage();
      await userEvent.type(await screen.findByLabelText('Nombre'), 'Sara');

      await pickCv();

      expect(await screen.findByTestId('cv-draft-status')).toHaveTextContent(
        'Se ha rellenado 1 campo',
      );
      expect(screen.getByLabelText('Nombre')).toHaveValue('Sara');
      expect(screen.getByLabelText('Apellidos')).toHaveValue('Ruiz Gil');
    });

    it('asks for the form to be filled by hand when the CV has no text', async () => {
      extract.mockResolvedValue({ draftId: 'd1', outcome: 'cv_draft.no_text', fields: {} });
      await renderPage();
      await screen.findByLabelText('Nombre');

      await pickCv();

      expect(await screen.findByTestId('cv-draft-status')).toHaveTextContent(
        /Rellena el formulario a mano/,
      );
      expect(screen.getByLabelText('Nombre')).toHaveValue('');
    });

    it('explains a refused CV without touching the form', async () => {
      extract.mockRejectedValue(new TranslatableError('candidate.cvDraft.error.scanner'));
      await renderPage();
      await userEvent.type(await screen.findByLabelText('Nombre'), 'Sara');

      await pickCv();

      await waitFor(() =>
        expect(screen.getByTestId('cv-draft-status')).toHaveTextContent(
          'No se puede analizar el archivo en este momento',
        ),
      );
      expect(screen.getByTestId('cv-draft-status')).toHaveClass('error');
      expect(screen.getByLabelText('Nombre')).toHaveValue('Sara');
    });

    it('shows a busy state while the CV is read and keeps the form usable', async () => {
      let resolve: (value: unknown) => void = () => undefined;
      extract.mockReturnValue(new Promise((done) => (resolve = done)));
      await renderPage();
      await screen.findByLabelText('Nombre');

      await pickCv();

      expect(screen.getByTestId('cv-draft-status')).toHaveTextContent('Analizando el CV');
      expect(screen.getByTestId('cv-draft-file')).toBeDisabled();
      await userEvent.type(screen.getByLabelText('Apellidos'), 'Pena');
      expect(screen.getByLabelText('Apellidos')).toHaveValue('Pena');

      resolve({ draftId: 'd1', outcome: 'cv_draft.extracted', fields: {} });
      expect(await screen.findByText(/no aporta datos/)).toBeInTheDocument();
      expect(screen.getByTestId('cv-draft-file')).toBeEnabled();
    });
  });
});
