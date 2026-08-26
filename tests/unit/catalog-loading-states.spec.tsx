import { render, screen, waitFor } from '@testing-library/react';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { CandidateSkills } from '../../src/app/features/candidates/components/candidate-skills';
import { CatalogService } from '../../src/app/features/catalogs/services/catalog.service';
import { AppError } from '../../src/app/shared/models/error.models';
import { createCatalogTestBed, FakeCatalogApi } from './support/catalog-doubles';

/**
 * A component whose catalog options are still loading, or failed to load, must say so
 * rather than render an empty option list as a complete result.
 */
describe('Catalog-consuming components', () => {
  const renderWith = (catalogService: CatalogService) =>
    render(
      <ServicesProvider value={{ ...services, catalogService } as unknown as Services}>
        <CandidateSkills candidateId="c1" skills={[]} canEdit />
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
    expect(screen.getByRole('button', { name: 'Añadir habilidad' })).toBeDisabled();
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
    expect(screen.getByRole('button', { name: 'Añadir habilidad' })).toBeDisabled();
    expect(screen.queryByRole('option', { name: 'Compras' })).not.toBeInTheDocument();
  });

  it('renders the loaded options once the vocabulary arrives', async () => {
    const { service } = createCatalogTestBed();

    renderWith(service);

    await waitFor(() =>
      expect(screen.getByRole('option', { name: 'Compras' })).toBeInTheDocument(),
    );
    expect(screen.queryByTestId('catalog-status')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Añadir habilidad' })).toBeEnabled();
  });
});
