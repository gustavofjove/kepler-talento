import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateTags } from '../../src/app/features/candidates/components/candidate-tags';
import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { FakeCatalogApi } from './support/catalog-doubles';

describe('CandidateTags', () => {
  const setup = async (
    canEdit = true,
    assigned = [{ id: 'old', tag: 'Antigua' }],
    readOnly = false,
  ) => {
    const api = new FakeCatalogApi({ tag: ['Activa', 'Antigua'] });
    api.families.get('tag')![1].isActive = false;
    const catalogService = new CatalogService(api);
    await catalogService.ensureLoaded();
    const candidateRelationsService = { addTag: vi.fn(), removeTag: vi.fn() };
    const authService = { profile: signal(null), hasPermission: vi.fn(() => canEdit) };
    render(
      <ServicesProvider
        value={
          {
            ...services,
            catalogService,
            candidateRelationsService,
            authService,
          } as unknown as Services
        }
      >
        <CandidateTags candidateId="candidate-1" tags={assigned} readOnly={readOnly} />
      </ServicesProvider>,
    );
    return candidateRelationsService;
  };

  it('keeps a deactivated assignment visible but does not offer it again', async () => {
    await setup();
    expect(screen.getByText('Antigua')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Activa' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Antigua' })).not.toBeInTheDocument();
  });

  it('assigns and removes tags through the relation service', async () => {
    const relations = await setup();
    await userEvent.selectOptions(screen.getByTestId('candidate-tag-select'), 'Activa');
    await userEvent.click(screen.getByRole('button', { name: 'Añadir etiqueta' }));
    expect(relations.addTag).toHaveBeenCalledWith('candidate-1', { tag: 'Activa' });

    await userEvent.click(screen.getByRole('button', { name: 'Quitar la etiqueta Antigua' }));
    expect(relations.removeTag).toHaveBeenCalledWith('candidate-1', 'old');
  });

  it('hides assignment controls without update permission', async () => {
    await setup(false);
    expect(screen.queryByTestId('candidate-tag-select')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows tags without assignment controls when read-only, even with update permission', async () => {
    await setup(true, undefined, true);
    expect(screen.getByText('Antigua')).toBeInTheDocument();
    expect(screen.queryByTestId('candidate-tag-select')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows the empty state when no tag is assigned', async () => {
    await setup(true, []);
    expect(screen.getByText('Sin etiquetas asignadas.')).toBeInTheDocument();
  });
});
