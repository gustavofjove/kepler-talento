import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { readFileSync } from 'node:fs';
import type { MockedObject } from 'vitest';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateEducation } from '../../src/app/features/candidates/components/candidate-education';
import { CandidateExperience } from '../../src/app/features/candidates/components/candidate-experience';
import type { PanelControl } from '../../src/app/features/candidates/components/candidate-panel.logic';
import type { Candidate } from '../../src/app/features/candidates/models/candidate.models';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { FakeCandidateApi } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

const candidate: Candidate = new FakeCandidateApi().seed({
  id: 'c1',
  education: [
    {
      id: 'e1',
      educationType: 'Grado',
      degree: 'Grado en ADE',
      specialty: '',
      institution: 'UCM',
      endYear: 2020,
      status: 'Finalizado',
    },
  ],
  experience: [
    {
      id: 'x1',
      company: 'Kepler',
      position: 'Técnico',
      sector: 'Industria',
      startDate: '2020-01-01',
      yearsExperience: 4,
      isCurrent: true,
    },
  ],
});

describe('Candidate profile section panels (KTL-29)', () => {
  it('keeps the CV preview after the panels, in the page aside (KTL-28)', () => {
    const source = readFileSync(
      'src/app/features/candidates/pages/candidate-detail-page.tsx',
      'utf8',
    );
    const preview = source.indexOf('<CandidateCvPreview candidate={item} />');
    const documents = source.indexOf('<CandidateDocuments');
    expect(documents).toBeGreaterThan(-1);
    expect(preview).toBeGreaterThan(documents);
    expect(source.slice(preview)).toMatch(
      /<CandidateCvPreview candidate=\{item\} \/>\s*<\/aside>\s*<\/div>\s*<\/div>\s*<\/section>/,
    );
  });

  let relations: MockedObject<CandidateRelationsService>;
  let catalogService: CatalogService;
  let control: PanelControl;
  const toastService = { show: vi.fn() };

  const renderPanel = (ui: (control: PanelControl) => React.ReactElement) => {
    const view = render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateRelationsService: relations,
            catalogService,
            toastService,
            authService: { profile: signal(null), hasPermission: vi.fn(() => true) },
          } as unknown as Services
        }
      >
        {ui(control)}
      </ServicesProvider>,
    );
    return {
      ...view,
      rerenderWith: (next: PanelControl) =>
        view.rerender(
          <ServicesProvider
            value={
              {
                ...services,
                candidateRelationsService: relations,
                catalogService,
                toastService,
                authService: { profile: signal(null), hasPermission: vi.fn(() => true) },
              } as unknown as Services
            }
          >
            {ui(next)}
          </ServicesProvider>,
        ),
    };
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    catalogService = await loadedCatalogService();
    relations = {
      saveEducation: vi.fn().mockResolvedValue(undefined),
      saveExperience: vi.fn().mockResolvedValue(undefined),
    } as unknown as MockedObject<CandidateRelationsService>;
    control = {
      editing: true,
      canEdit: true,
      onEdit: vi.fn(),
      onClose: vi.fn(),
      onDirtyChange: vi.fn(),
    };
  });

  describe('CandidateEducation', () => {
    const education = (c: PanelControl) => <CandidateEducation candidate={candidate} control={c} />;

    it('refuses an invalid entry locally and keeps the typed values', async () => {
      renderPanel(education);

      await userEvent.type(screen.getByLabelText('Centro'), 'UCM');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(screen.getByText(/titulación es obligatoria/i)).toBeInTheDocument();
      expect(screen.getByLabelText('Centro')).toHaveValue('UCM');
      expect(relations.saveEducation).not.toHaveBeenCalled();
    });

    it('stages an added entry, reports the panel dirty, and saves the whole list', async () => {
      renderPanel(education);

      await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Grado');
      await userEvent.type(screen.getByLabelText('Titulación'), 'Máster en RRHH');
      await userEvent.selectOptions(screen.getByLabelText('Estado'), 'Finalizada');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(screen.getByLabelText('Titulación')).toHaveValue('');
      expect(control.onDirtyChange).toHaveBeenLastCalledWith(true);
      expect(relations.saveEducation).not.toHaveBeenCalled();

      await userEvent.click(screen.getByTestId('candidate-panel-education-save'));

      await waitFor(() => expect(control.onClose).toHaveBeenCalled());
      expect(relations.saveEducation).toHaveBeenCalledWith('c1', [
        expect.objectContaining({ id: 'e1' }),
        expect.objectContaining({ degree: 'Máster en RRHH' }),
      ]);
      expect(toastService.show).toHaveBeenCalledWith('Formación guardada.', 'success');
    });

    it('keeps the panel open with the Spanish error when the save is refused', async () => {
      relations.saveEducation.mockRejectedValue(new Error('El candidato ha cambiado.'));
      renderPanel(education);
      await userEvent.click(screen.getByRole('button', { name: 'Quitar' }));

      await userEvent.click(screen.getByTestId('candidate-panel-education-save'));

      expect(await screen.findByText('El candidato ha cambiado.')).toBeInTheDocument();
      expect(control.onClose).not.toHaveBeenCalled();
      expect(screen.queryByText('Grado en ADE')).not.toBeInTheDocument();
    });

    it('drops the draft when edit mode ends', async () => {
      const { rerenderWith } = renderPanel(education);
      await userEvent.click(screen.getByRole('button', { name: 'Quitar' }));
      expect(screen.queryByText('Grado en ADE')).not.toBeInTheDocument();

      rerenderWith({ ...control, editing: false });

      expect(screen.getByText('Grado en ADE')).toBeInTheDocument();
      expect(control.onDirtyChange).toHaveBeenLastCalledWith(false);
    });
  });

  describe('CandidateExperience', () => {
    it('refuses an end date before the start date locally', async () => {
      renderPanel((c) => <CandidateExperience candidate={candidate} control={c} />);

      await userEvent.type(screen.getByLabelText('Fecha inicio'), '2024-06-01');
      await userEvent.type(screen.getByLabelText('Fecha fin'), '2024-01-01');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir experiencia' }));

      expect(
        screen.getByText('La fecha de fin no puede ser anterior a la fecha de inicio.'),
      ).toBeInTheDocument();
      expect(relations.saveExperience).not.toHaveBeenCalled();
    });
  });

  describe.each([
    {
      name: 'CandidateEducation',
      testId: 'candidate-education',
      text: 'Grado en ADE',
      ui: (c: PanelControl) => <CandidateEducation candidate={candidate} control={c} />,
    },
    {
      name: 'CandidateExperience',
      testId: 'candidate-experience',
      text: 'Kepler',
      ui: (c: PanelControl) => <CandidateExperience candidate={candidate} control={c} />,
    },
  ])('$name read-only mode', ({ testId, text, ui }) => {
    it('shows the entries and no editing control outside edit mode', () => {
      control = { ...control, editing: false };
      renderPanel(ui);

      const section = within(screen.getByTestId(testId));
      expect(section.getByText(text, { exact: false })).toBeInTheDocument();
      expect(section.queryByRole('button')).not.toBeInTheDocument();
      expect(section.queryByRole('combobox')).not.toBeInTheDocument();
    });

    it('offers editing controls in edit mode', () => {
      renderPanel(ui);

      expect(within(screen.getByTestId(testId)).getAllByRole('button').length).toBeGreaterThan(0);
    });
  });
});
