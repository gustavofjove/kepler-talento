import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { readFileSync } from 'node:fs';
import type { MockedObject } from 'vitest';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateEducation } from '../../src/app/features/candidates/components/candidate-education';
import { CandidateExperience } from '../../src/app/features/candidates/components/candidate-experience';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { loadedCatalogService } from './support/catalog-doubles';

describe('Candidate profile section components', () => {
  it('keeps the CV preview after the sections, in the page aside (KTL-28)', () => {
    const source = readFileSync(
      'src/app/features/candidates/pages/candidate-detail-page.tsx',
      'utf8',
    );
    const preview = source.indexOf('<CandidateCvPreview candidate={item} />');
    const documents = source.indexOf('<CandidateDocuments candidate={item} readOnly />');
    expect(documents).toBeGreaterThan(-1);
    expect(preview).toBeGreaterThan(documents);
    expect(source.slice(preview)).toMatch(
      /<CandidateCvPreview candidate=\{item\} \/>\s*<\/aside>\s*<\/div>\s*<\/div>\s*<\/section>/,
    );
  });
  let relations: MockedObject<CandidateRelationsService>;
  let catalogService: CatalogService;
  let canUpdate: boolean;

  const renderSection = (ui: React.ReactElement) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateRelationsService: relations,
            catalogService,
            authService: { profile: signal(null), hasPermission: vi.fn(() => canUpdate) },
          } as unknown as Services
        }
      >
        {ui}
      </ServicesProvider>,
    );

  beforeEach(async () => {
    localStorage.clear();
    // Catalogs are API-backed: components get a loaded test double through the
    // ServicesProvider seam rather than reaching the network.
    catalogService = await loadedCatalogService();
    canUpdate = true;
    relations = {
      addEducation: vi.fn(),
      removeEducation: vi.fn(),
      addExperience: vi.fn(),
      removeExperience: vi.fn(),
    } as unknown as MockedObject<CandidateRelationsService>;
  });

  describe('CandidateEducation', () => {
    it('surfaces the validation error from the relations service and keeps the draft', async () => {
      relations.addEducation.mockImplementation(() => {
        throw new Error('La titulación es obligatoria.');
      });
      renderSection(<CandidateEducation candidateId="c1" education={[]} />);

      await userEvent.type(screen.getByLabelText('Centro'), 'UCM');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(screen.getByText('La titulación es obligatoria.')).toBeInTheDocument();
      expect(screen.getByLabelText('Centro')).toHaveValue('UCM');
    });

    it('clears the draft and error after a successful add', async () => {
      renderSection(<CandidateEducation candidateId="c1" education={[]} />);

      await userEvent.type(screen.getByLabelText('Titulación'), 'Grado en ADE');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir formación' }));

      expect(relations.addEducation).toHaveBeenCalledWith(
        'c1',
        expect.objectContaining({ degree: 'Grado en ADE' }),
      );
      expect(screen.queryByText(/obligatoria/)).not.toBeInTheDocument();
      expect(screen.getByLabelText('Titulación')).toHaveValue('');
    });
  });

  describe('CandidateExperience', () => {
    it('surfaces the date-order validation error', async () => {
      relations.addExperience.mockImplementation(() => {
        throw new Error('La fecha de fin no puede ser anterior a la fecha de inicio.');
      });
      renderSection(<CandidateExperience candidateId="c1" experience={[]} />);

      await userEvent.type(screen.getByLabelText('Fecha inicio'), '2024-06-01');
      await userEvent.type(screen.getByLabelText('Fecha fin'), '2024-01-01');
      await userEvent.click(screen.getByRole('button', { name: 'Añadir experiencia' }));

      expect(
        screen.getByText('La fecha de fin no puede ser anterior a la fecha de inicio.'),
      ).toBeInTheDocument();
    });
  });

  // KTL-22: the detail page renders every section read-only, whatever the viewer may do.
  // Languages, programs, skills and tags are covered by candidate-relation-section.spec.
  describe.each([
    {
      name: 'CandidateEducation',
      testId: 'candidate-education',
      text: 'Grado en ADE',
      ui: (readOnly: boolean) => (
        <CandidateEducation
          candidateId="c1"
          education={[
            {
              id: 'e1',
              educationType: 'Grado',
              degree: 'Grado en ADE',
              specialty: '',
              institution: 'UCM',
              endYear: 2020,
              status: 'Finalizado',
            },
          ]}
          readOnly={readOnly}
        />
      ),
    },
    {
      name: 'CandidateExperience',
      testId: 'candidate-experience',
      text: 'Kepler',
      ui: (readOnly: boolean) => (
        <CandidateExperience
          candidateId="c1"
          experience={[
            {
              id: 'x1',
              company: 'Kepler',
              position: 'Técnico',
              sector: 'Industria',
              startDate: '2020-01-01',
              endDate: '',
              yearsExperience: 4,
              isCurrent: true,
            },
          ]}
          readOnly={readOnly}
        />
      ),
    },
  ])('$name read-only mode', ({ testId, text, ui }) => {
    it('shows the entries and no editing control even with update permission', () => {
      renderSection(ui(true));

      const section = within(screen.getByTestId(testId));
      expect(section.getByText(text, { exact: false })).toBeInTheDocument();
      expect(section.queryByRole('button')).not.toBeInTheDocument();
      expect(section.queryByRole('combobox')).not.toBeInTheDocument();
    });

    it('offers editing controls when editable and the update permission is held', () => {
      renderSection(ui(false));

      expect(within(screen.getByTestId(testId)).getAllByRole('button').length).toBeGreaterThan(0);
    });

    it('offers no editing control when editable but the update permission is missing', () => {
      canUpdate = false;
      renderSection(ui(false));

      expect(within(screen.getByTestId(testId)).queryByRole('button')).not.toBeInTheDocument();
    });
  });
});
