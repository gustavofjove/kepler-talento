import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateRelationSection } from '../../src/app/features/candidates/components/candidate-relation-section';
import {
  RELATION_DEFINITIONS,
  type RelationKind,
} from '../../src/app/features/candidates/components/candidate-relation-section.logic';
import type { Candidate } from '../../src/app/features/candidates/models/candidate.models';
import type { PickerItem } from '../../src/app/features/catalogs/components/catalog-value-picker.logic';
import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { FakeCandidateApi } from './support/candidate-doubles';
import {
  createCatalogTestBed,
  FakeCatalogApi,
  loadedCatalogService,
} from './support/catalog-doubles';

/**
 * The one table-driven row behind the candidate languages, programs, skills and tags. Since
 * KTL-29 it is controlled: it proposes each add, change and removal as a new draft list and
 * writes nothing itself. The harness applies every proposal and records it.
 */
describe('CandidateRelationSection', () => {
  let catalogService: CatalogService;
  let proposals: { items: PickerItem[]; changed: PickerItem }[];

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

  function Harness(props: { kind: RelationKind; subject: Candidate; editing: boolean }) {
    const [items, setItems] = useState(() => RELATION_DEFINITIONS[props.kind].items(props.subject));
    return (
      <CandidateRelationSection
        kind={props.kind}
        items={items}
        editing={props.editing}
        onItemsChange={(next, changed) => {
          proposals.push({ items: next, changed });
          setItems(next);
        }}
      />
    );
  }

  const renderSection = (kind: RelationKind, subject = candidate(), editing = true) =>
    render(
      <ServicesProvider
        value={
          {
            ...services,
            catalogService,
            authService: { profile: signal(null), hasPermission: vi.fn(() => true) },
          } as unknown as Services
        }
      >
        <Harness kind={kind} subject={subject} editing={editing} />
      </ServicesProvider>,
    );

  const last = () => proposals.at(-1)!;

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
    proposals = [];
  });

  describe.each([
    {
      kind: 'language',
      testId: 'candidate-languages',
      prefix: 'candidate-language',
      text: 'TOEFL',
      label: 'Idiomas',
    },
    {
      kind: 'program',
      testId: 'candidate-programs',
      prefix: 'candidate-program',
      text: '2 años',
      label: 'Programas',
    },
    {
      kind: 'skill',
      testId: 'candidate-skills',
      prefix: 'candidate-skill',
      text: 'Compras',
      label: 'Habilidades',
    },
    {
      kind: 'tag',
      testId: 'candidate-tags',
      prefix: 'candidate-tag',
      text: 'Antigua',
      label: 'Etiquetas',
    },
  ] as const)('$kind', ({ kind, testId, prefix, text, label }) => {
    it('shows its entries as chips with a picker in edit mode, under the kept test id', () => {
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
      expect(section.getByTestId(`${prefix}-add`)).toBeEnabled();
    });

    it('shows the entries and no editing control outside edit mode', async () => {
      renderSection(kind, candidate(), false);

      const section = within(screen.getByTestId(testId));
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
      expect(section.queryByRole('combobox')).toBeNull();
      expect(section.queryByRole('button')).toBeNull();
      await userEvent.click(section.getAllByTestId(`${prefix}-chip`)[0]);
      expect(screen.queryByTestId(`${prefix}-editor`)).toBeNull();
    });

    it('disables adding while catalogs cannot load, leaving the notice to the panel', async () => {
      const api = new FakeCatalogApi();
      api.failure = new AppError('INTERNAL_ERROR', 'No se ha podido conectar con el servidor.');
      catalogService = createCatalogTestBed(api).service;
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      await waitFor(() => expect(section.getByTestId(`${prefix}-add`)).toBeDisabled());
      expect(section.getAllByTestId(`${prefix}-chip`)[0]).toHaveTextContent(text);
      expect(section.queryByTestId('catalog-status')).toBeNull();
    });

    it('names its picker with its visible label instead of a heading', () => {
      renderSection(kind);

      const section = within(screen.getByTestId(testId));
      expect(section.queryByRole('heading')).toBeNull();
      expect(section.getByRole('grid')).toHaveAccessibleName(
        section.getByText(label, { selector: '.catalog-picker-label' }).textContent!,
      );
    });
  });

  it('shows the empty state outside edit mode, and only (+) in it', () => {
    const { unmount } = renderSection('language', candidate({ languages: [] }), false);
    expect(screen.getByText('Sin idiomas asociados.')).toBeInTheDocument();
    unmount();

    renderSection('language', candidate({ languages: [] }));
    expect(screen.queryByText('Sin idiomas asociados.')).toBeNull();
    expect(screen.getByTestId('candidate-language-add')).toBeInTheDocument();
  });

  it('proposes a language at the lowest active level, opening no editor', async () => {
    const lowest = catalogService.activeNames('language_level')[0]!;
    renderSection('language', candidate({ languages: [] }));

    await typeInto('candidate-language', 'ingl');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(screen.queryByTestId('candidate-language-editor')).toBeNull();
    expect(last().changed).toEqual(expect.objectContaining({ value: 'Inglés', level: lowest }));
    expect(chip('candidate-language', 'Inglés')).toHaveTextContent(lowest);
  });

  it('cannot add a skill while no skill level is active', async () => {
    const api = new FakeCatalogApi({ skill: ['Compras', 'Gestión documental'], skill_level: [] });
    catalogService = new CatalogService(api);
    await catalogService.ensureLoaded();
    renderSection('skill');

    expect(screen.getByTestId('candidate-skill-add')).toBeDisabled();
    expect(proposals).toHaveLength(0);
  });

  it('proposes a language level change in place, keeping its key and certification', async () => {
    renderSection('language');

    await userEvent.click(chip('candidate-language', 'Inglés'));
    const editor = await screen.findByTestId('candidate-language-editor');
    await userEvent.click(within(editor).getByRole('radio', { name: 'C1' }));

    expect(last().items).toEqual([
      expect.objectContaining({
        key: 'l1',
        value: 'Inglés',
        level: 'C1',
        details: { certification: 'TOEFL' },
      }),
    ]);
  });

  it('proposes the years of a program in place', async () => {
    renderSection('program');

    await userEvent.click(chip('candidate-program', 'SAP'));
    const years = await screen.findByLabelText('Años de experiencia');
    await userEvent.clear(years);
    await userEvent.type(years, '5{Enter}');

    expect(last().changed).toEqual(
      expect.objectContaining({ key: 'p1', level: 'Medio', details: { yearsExperience: 5 } }),
    );
  });

  it('proposes a tag without a level', async () => {
    await useTagCatalog();
    await catalogService.ensureLoaded();
    renderSection('tag', candidate({ tags: [] }));

    await typeInto('candidate-tag', 'activ');
    await userEvent.keyboard('{ArrowDown}{Enter}');

    expect(screen.queryByTestId('candidate-tag-editor')).toBeNull();
    expect(last().items).toEqual([expect.objectContaining({ value: 'Activa', level: '' })]);
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

  it('offers removal again, and no stale input, when edit mode is re-entered', async () => {
    const subject = candidate();
    const view = renderSection('skill', subject);
    await typeInto('candidate-skill', 'gest');
    const wrap = (editing: boolean) => (
      <ServicesProvider
        value={
          {
            ...services,
            catalogService,
            authService: { profile: signal(null), hasPermission: vi.fn(() => true) },
          } as unknown as Services
        }
      >
        <Harness kind="skill" subject={subject} editing={editing} />
      </ServicesProvider>
    );

    view.rerender(wrap(false));
    expect(screen.queryByRole('button', { name: 'Quitar Compras' })).toBeNull();
    view.rerender(wrap(true));

    expect(screen.getByRole('button', { name: 'Quitar Compras' })).toBeInTheDocument();
    expect(screen.queryByTestId('candidate-skill-input')).toBeNull();
  });

  it('proposes the list without a removed entry', async () => {
    renderSection('skill');

    await userEvent.click(screen.getByRole('button', { name: 'Quitar Compras' }));

    expect(last().items.map((item) => item.key)).toEqual(['s2']);
    expect(screen.queryByRole('button', { name: 'Quitar Compras' })).toBeNull();
  });
});
