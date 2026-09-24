import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { MockedObject } from 'vitest';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { TranslatableError } from '../../src/app/core/i18n/translatable-error';
import { signal } from '../../src/app/core/state/signal';
import { CandidateRelationSection } from '../../src/app/features/candidates/components/candidate-relation-section';
import type { RelationKind } from '../../src/app/features/candidates/components/candidate-relation-section.logic';
import type { Candidate } from '../../src/app/features/candidates/models/candidate.models';
import type { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { FakeCandidateApi } from './support/candidate-doubles';
import {
  createCatalogTestBed,
  FakeCatalogApi,
  loadedCatalogService,
} from './support/catalog-doubles';

/**
 * The one table-driven section behind the candidate languages, programs, skills and tags.
 * The relations service is a double, so each spec controls when a write settles.
 */
describe('CandidateRelationSection', () => {
  let relations: MockedObject<CandidateRelationsService>;
  let catalogService: CatalogService;
  let canUpdate: boolean;

  const candidate = (overrides: Partial<Candidate> = {}): Candidate =>
    new FakeCandidateApi().seed({
      id: 'c1',
      firstName: 'Ana',
      lastName: 'López',
      languages: [{ id: 'l1', language: 'Inglés', level: 'B1', certification: 'TOEFL' }],
      programs: [{ id: 'p1', program: 'SAP', level: 'Medio', yearsExperience: 2 }],
      skills: [
        { id: 's1', skill: 'Compras', level: 'Medio' },
        { id: 's2', skill: 'Análisis', level: 'Alto' },
      ],
      tags: [{ id: 't1', tag: 'Antigua' }],
      ...overrides,
    });

  const renderSection = (kind: RelationKind, subject = candidate(), readOnly = false) =>
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
        <CandidateRelationSection kind={kind} candidate={subject} readOnly={readOnly} />
      </ServicesProvider>,
    );

  /** The input exists only while adding: (+) reveals it. */
  const typeInto = async (prefix: string, text: string) => {
    if (!screen.queryByTestId(`${prefix}-input`))
      await userEvent.click(screen.getByTestId(`${prefix}-add`));
    await userEvent.type(screen.getByTestId(`${prefix}-input`), text);
  };

  const chip = (prefix: string, value: string) =>
    screen
      .getAllByTestId(`${prefix}-chip`)
      .find((element) => element.getAttribute('data-value') === value) as HTMLElement;

  const useTagCatalog = async (): Promise<FakeCatalogApi> => {
    const api = new FakeCatalogApi({ tag: ['Activa', 'Antigua'] });
    catalogService = new CatalogService(api);
    return api;
  };

  beforeEach(async () => {
    catalogService = await loadedCatalogService();
    canUpdate = true;
    relations = {
      addLanguage: vi.fn(async () => undefined),
      updateLanguage: vi.fn(async () => undefined),
      removeLanguage: vi.fn(async () => undefined),
      addProgram: vi.fn(async () => undefined),
      updateProgram: vi.fn(async () => undefined),
      removeProgram: vi.fn(async () => undefined),
      addSkill: vi.fn(async () => undefined),
      updateSkill: vi.fn(async () => undefined),
      removeSkill: vi.fn(async () => undefined),
      addTag: vi.fn(async () => undefined),
      removeTag: vi.fn(async () => undefined),
    } as unknown as MockedObject<CandidateRelationsService>;
  });

  describe.each([
    {
      kind: 'language',
      testId: 'candidate-languages',
      prefix: 'candidate-language',
      text: 'TOEFL',
    },
    { kind: 'program', testId: 'candidate-programs', prefix: 'candidate-program', text: '2 años' },
    { kind: 'skill', testId: 'candidate-skills', prefix: 'candidate-skill', text: 'Compras' },
    { kind: 'tag', testId: 'candidate-tags', prefix: 'candidate-tag', text: 'Antigua' },
  ] as const)('$kind', ({ kind, testId, prefix, text }) => {
    it('shows its entries as chips with a picker, under the kept section test id', () => {
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
      expect(section.getByTestId(`${prefix}-add`)).toBeEnabled();
    });

    it('shows the entries and no editing control when read-only', async () => {
      renderSection(kind, candidate(), true);

      const section = within(screen.getByTestId(testId));
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
      expect(section.queryByRole('combobox')).toBeNull();
      expect(section.queryByRole('button')).toBeNull();
      await userEvent.click(section.getAllByTestId(`${prefix}-chip`)[0]);
      expect(screen.queryByTestId(`${prefix}-editor`)).toBeNull();
    });

    it('offers no editing control without the update permission', () => {
      canUpdate = false;
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      expect(section.queryByRole('combobox')).toBeNull();
      expect(section.queryByRole('button')).toBeNull();
    });

    it('disables its input and shows the catalog notice while catalogs cannot load', async () => {
      const api = new FakeCatalogApi();
      api.failure = new AppError('INTERNAL_ERROR', 'No se ha podido conectar con el servidor.');
      catalogService = createCatalogTestBed(api).service;
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      await waitFor(() =>
        expect(section.getByTestId('catalog-status')).toHaveTextContent(
          'No se ha podido conectar con el servidor.',
        ),
      );
      expect(section.getByTestId(`${prefix}-add`)).toBeDisabled();
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
    });
  });

  it('shows the empty state on the read-only page, and only (+) on the edit page', () => {
    const { unmount } = renderSection('language', candidate({ languages: [] }), true);
    expect(screen.getByText('Sin idiomas asociados.')).toBeInTheDocument();
    unmount();

    renderSection('language', candidate({ languages: [] }));
    expect(screen.queryByText('Sin idiomas asociados.')).toBeNull();
    expect(screen.getByTestId('candidate-language-add')).toBeInTheDocument();
  });

  it('saves a language only once its level is chosen, with its certification', async () => {
    renderSection('language');

    await typeInto('candidate-language', 'fran');
    await userEvent.keyboard('{ArrowDown}{Enter}');
    const editor = await screen.findByTestId('candidate-language-editor');
    expect(relations.addLanguage).not.toHaveBeenCalled();

    await userEvent.type(within(editor).getByLabelText('Certificación'), 'DELF');
    await userEvent.click(within(editor).getByRole('radio', { name: 'B2' }));

    expect(relations.addLanguage).toHaveBeenCalledWith('c1', {
      language: 'Francés',
      level: 'B2',
      certification: 'DELF',
    });
  });

  it('sends no write when the level choice is abandoned', async () => {
    renderSection('skill');

    await typeInto('candidate-skill', 'gest');
    await userEvent.keyboard('{ArrowDown}{Enter}');
    await screen.findByTestId('candidate-skill-editor');
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByTestId('candidate-skill-editor')).toBeNull());
    expect(relations.addSkill).not.toHaveBeenCalled();
    expect(chip('candidate-skill', 'Gestión documental')).toBeUndefined();
  });

  it('changes a language level in place and keeps its certification', async () => {
    renderSection('language');

    await userEvent.click(chip('candidate-language', 'Inglés'));
    const editor = await screen.findByTestId('candidate-language-editor');
    await userEvent.click(within(editor).getByRole('radio', { name: 'C1' }));

    expect(relations.updateLanguage).toHaveBeenCalledWith('c1', {
      id: 'l1',
      language: 'Inglés',
      level: 'C1',
      certification: 'TOEFL',
    });
    expect(relations.addLanguage).not.toHaveBeenCalled();
    expect(relations.removeLanguage).not.toHaveBeenCalled();
  });

  it('changes the years of a program in place', async () => {
    renderSection('program');

    await userEvent.click(chip('candidate-program', 'SAP'));
    const years = await screen.findByLabelText('Años de experiencia');
    await userEvent.clear(years);
    await userEvent.type(years, '5{Enter}');

    expect(relations.updateProgram).toHaveBeenCalledWith(
      'c1',
      expect.objectContaining({ id: 'p1', level: 'Medio', yearsExperience: 5 }),
    );
  });

  it('marks a pending write, then reports a refusal on its chip with a retry', async () => {
    let refuse: (error: Error) => void = () => undefined;
    relations.updateSkill.mockImplementationOnce(
      () => new Promise<void>((_, reject) => (refuse = reject)),
    );
    renderSection('skill');

    await userEvent.click(chip('candidate-skill', 'Compras'));
    const editor = await screen.findByTestId('candidate-skill-editor');
    await userEvent.click(within(editor).getByRole('radio', { name: 'Alto' }));

    expect(chip('candidate-skill', 'Compras')).toHaveAttribute('data-status', 'pending');
    expect(chip('candidate-skill', 'Compras')).toHaveTextContent('Alto');
    refuse(new AppError('CONFLICT', 'Otro usuario ha cambiado el candidato.'));

    await waitFor(() =>
      expect(chip('candidate-skill', 'Compras')).toHaveAttribute('data-status', 'error'),
    );
    // A failed change shows what is actually saved, and the other entries are untouched.
    expect(chip('candidate-skill', 'Compras')).toHaveTextContent('Medio');
    expect(chip('candidate-skill', 'Análisis')).not.toHaveAttribute('data-status');
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Compras: Otro usuario ha cambiado el candidato.',
    );

    await userEvent.click(screen.getByRole('button', { name: 'Reintentar Compras' }));

    expect(relations.updateSkill).toHaveBeenCalledTimes(2);
    expect(relations.updateSkill).toHaveBeenLastCalledWith(
      'c1',
      expect.objectContaining({ id: 's1', level: 'Alto' }),
    );
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
  });

  it('keeps a refused add as a failed chip, and removing it sends nothing', async () => {
    relations.addTag.mockRejectedValueOnce(
      new TranslatableError('candidate.profile.tags.duplicate'),
    );
    await useTagCatalog();
    await catalogService.ensureLoaded();
    renderSection('tag', candidate({ tags: [] }));

    await typeInto('candidate-tag', 'activ');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    const [failed] = screen.getAllByTestId('candidate-tag-chip');
    expect(failed).toHaveAttribute('data-status', 'error');

    await userEvent.click(within(failed).getByTestId('candidate-tag-remove'));

    expect(screen.queryAllByTestId('candidate-tag-chip')).toHaveLength(0);
    expect(relations.removeTag).not.toHaveBeenCalled();
  });

  it('adds a tag at once, since tags have no level', async () => {
    await useTagCatalog();
    await catalogService.ensureLoaded();
    renderSection('tag', candidate({ tags: [] }));

    await typeInto('candidate-tag', 'activ');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(screen.queryByTestId('candidate-tag-editor')).toBeNull();
    expect(relations.addTag).toHaveBeenCalledWith('c1', { tag: 'Activa' });
  });

  it('keeps a deactivated tag assigned but does not offer it again', async () => {
    const api = await useTagCatalog();
    api.families.get('tag')![1].isActive = false;
    await catalogService.ensureLoaded();
    renderSection('tag');

    expect(chip('candidate-tag', 'Antigua')).toBeInTheDocument();
    await typeInto('candidate-tag', 'a');

    expect(screen.getByRole('option', { name: 'Activa' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Antigua' })).toBeNull();
  });

  it('removes an entry by id', async () => {
    renderSection('skill');

    await userEvent.click(screen.getByRole('button', { name: 'Quitar Compras' }));

    expect(relations.removeSkill).toHaveBeenCalledWith('c1', 's1');
  });
});
