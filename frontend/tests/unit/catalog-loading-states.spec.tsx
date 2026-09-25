import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateCompetencies } from '../../src/app/features/candidates/components/candidate-competencies';
import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { FakeCandidateApi } from './support/candidate-doubles';
import { createCatalogTestBed, FakeCatalogApi } from './support/catalog-doubles';

/**
 * A component whose catalog options are still loading, or failed to load, must say so
 * rather than render an empty option list as a complete result.
 */
describe('Catalog-consuming components', () => {
  // The picker input, which is what consumes the catalog, renders only in edit mode.
  const authService = { profile: signal(null), hasPermission: () => true };
  const candidate = new FakeCandidateApi().seed({ id: 'c1', firstName: 'Ana', lastName: 'Ruiz' });
  const renderWith = (catalogService: CatalogService) =>
    render(
      <ServicesProvider value={{ ...services, catalogService, authService } as unknown as Services}>
        {/* In edit mode: the picker input and the notice render only there (KTL-29). */}
        <CandidateCompetencies
          candidate={candidate}
          control={{
            editing: true,
            canEdit: true,
            onEdit: () => undefined,
            onClose: () => undefined,
            onDirtyChange: () => undefined,
          }}
        />
      </ServicesProvider>,
    );

  it('renders the loading branch instead of an empty option list', async () => {
    let release: (() => void) | undefined;
    const api = new FakeCatalogApi();
    const originalList = api.list.bind(api);
    api.list = async (family, includeInactive) => {
      await new Promise<void>((resolve) => {
        release = resolve;
      });
      return originalList(family, includeInactive);
    };
    const { service } = createCatalogTestBed(api);

    renderWith(service);

    await waitFor(() => expect(screen.getByTestId('catalog-status')).toHaveTextContent(/Cargando/));
    expect(screen.getByTestId('candidate-skill-add')).toBeDisabled();
    release?.();
  });

  it('renders the failure branch with the Spanish message from the error model', async () => {
    const api = new FakeCatalogApi();
    api.failure = new AppError('INTERNAL_ERROR', 'No se ha podido conectar con el servidor.');
    const { service } = createCatalogTestBed(api);

    renderWith(service);

    await waitFor(() =>
      expect(screen.getByTestId('catalog-status')).toHaveTextContent(
        'No se ha podido conectar con el servidor.',
      ),
    );
    expect(screen.getByTestId('candidate-skill-add')).toBeDisabled();
    expect(screen.queryByRole('option', { name: 'Compras' })).not.toBeInTheDocument();
  });

  it('says once which family cannot be added to while its levels are all inactive', async () => {
    const { service } = createCatalogTestBed(
      new FakeCatalogApi({ skill: ['Compras'], skill_level: [], language_level: ['A1'] }),
    );

    renderWith(service);

    await waitFor(() =>
      expect(screen.getByTestId('catalog-status')).toHaveTextContent(
        'No hay niveles activos de Habilidades',
      ),
    );
    expect(screen.getAllByTestId('catalog-status')).toHaveLength(1);
    expect(screen.getByTestId('candidate-skill-add')).toBeDisabled();
    expect(screen.getByTestId('candidate-language-add')).toBeEnabled();
  });

  it('offers the loaded options once the vocabulary arrives', async () => {
    const { service } = createCatalogTestBed();

    renderWith(service);

    await waitFor(() => expect(screen.getByTestId('candidate-skill-add')).toBeEnabled());
    await userEvent.click(screen.getByTestId('candidate-skill-add'));
    expect(screen.queryByTestId('catalog-status')).not.toBeInTheDocument();
    await userEvent.type(screen.getByTestId('candidate-skill-input'), 'comp');
    expect(screen.getByRole('option', { name: 'Compras' })).toBeInTheDocument();
  });
});
