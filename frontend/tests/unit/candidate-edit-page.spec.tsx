import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateEditPage } from '../../src/app/features/candidates/pages/candidate-edit-page';
import { CandidateNotesService } from '../../src/app/features/candidates/services/candidate-notes.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

function LocationProbe() {
  return <p data-testid="location">{useLocation().pathname}</p>;
}

const SECTIONS = [
  'candidate-languages',
  'candidate-programs',
  'candidate-education',
  'candidate-experience',
  'candidate-skills',
  'candidate-tags',
  'candidate-notes',
  'candidate-documents',
];

describe('CandidateEditPage (KTL-22)', () => {
  let bed: CandidateTestBed;
  let relations: CandidateRelationsService;
  let granted: Set<string>;
  const toastService = { show: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'c1', firstName: 'Ona', lastName: 'Marti' });
    relations = new CandidateRelationsService(bed.service);
    granted = new Set(['candidates.create', 'candidates.update', 'documents.upload']);
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
            candidateRelationsService: relations,
            candidateNotesService: new CandidateNotesService(bed.api),
            catalogService,
            documentService,
            toastService,
            authService,
          } as unknown as Services
        }
      >
        <MemoryRouter initialEntries={[path]}>
          <LocationProbe />
          <Routes>
            <Route path="/app/candidates/new" element={<CandidateEditPage />} />
            <Route path="/app/candidates/:id/edit" element={<CandidateEditPage />} />
            <Route path="/app/candidates/:id" element={<p data-testid="detail-page" />} />
          </Routes>
        </MemoryRouter>
      </ServicesProvider>,
    );
  };

  it('offers every section, editable, for an existing candidate', async () => {
    await renderAt('/app/candidates/c1/edit');

    expect(await screen.findByLabelText('Nombre')).toHaveValue('Ona');
    for (const testId of SECTIONS) {
      expect(screen.getByTestId(testId)).toBeInTheDocument();
    }
    expect(within(screen.getByTestId('candidate-skills')).getByRole('button')).toBeInTheDocument();
    expect(screen.getByTestId('document-file')).toBeInTheDocument();
    expect(screen.getByTestId('candidate-edit-hint')).toHaveTextContent('«Guardar»');
    expect(screen.getByTestId('candidate-edit-view')).toHaveAttribute('href', '/app/candidates/c1');
  });

  it('keeps document upload hidden for an editor without the upload permission', async () => {
    granted.delete('documents.upload');
    await renderAt('/app/candidates/c1/edit');

    expect(await screen.findByTestId('candidate-documents')).toBeInTheDocument();
    expect(screen.queryByTestId('document-file')).not.toBeInTheDocument();
  });

  it('shows only the core form and the post-save hint for a new candidate', async () => {
    await renderAt('/app/candidates/new');

    expect(await screen.findByLabelText('Nombre')).toHaveValue('');
    for (const testId of SECTIONS) {
      expect(screen.queryByTestId(testId)).not.toBeInTheDocument();
    }
    expect(screen.getByTestId('candidate-edit-hint')).toHaveTextContent(/Guarda el candidato/);
  });

  it('stays on the edit page and confirms after saving the core record', async () => {
    await renderAt('/app/candidates/c1/edit');
    const firstName = await screen.findByLabelText('Nombre');

    await userEvent.clear(firstName);
    await userEvent.type(firstName, 'Onna');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(toastService.show).toHaveBeenCalledWith('Datos principales guardados.', 'success'),
    );
    expect(bed.api.candidates.get('c1')?.firstName).toBe('Onna');
    expect(screen.getByTestId('location')).toHaveTextContent('/app/candidates/c1/edit');
  });

  it('does not conflict with itself when a section changed before the core save', async () => {
    await renderAt('/app/candidates/c1/edit');
    const lastName = await screen.findByLabelText('Apellidos');
    await userEvent.clear(lastName);
    await userEvent.type(lastName, 'Martí');

    // A section write advances the candidate's version while the core draft is unsaved.
    await act(() => relations.addSkill('c1', { skill: 'Compras', level: 'Medio' }));
    expect(lastName).toHaveValue('Martí');

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(toastService.show).toHaveBeenCalledWith('Datos principales guardados.', 'success'),
    );
    const stored = bed.api.candidates.get('c1');
    expect(stored?.lastName).toBe('Martí');
    expect(stored?.skills.map((item) => item.skill)).toEqual(['Compras']);
  });

  it('continues on the new candidate’s edit page after the first save', async () => {
    await renderAt('/app/candidates/new');

    await userEvent.type(await screen.findByLabelText('Nombre'), 'Sara');
    await userEvent.type(screen.getByLabelText('Apellidos'), 'Pena');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(screen.getByTestId('location').textContent).toMatch(/^\/app\/candidates\/.+\/edit$/),
    );
    expect(await screen.findByTestId('candidate-skills')).toBeInTheDocument();
  });

  it('opens the detail page after creating when the user may not update', async () => {
    granted.delete('candidates.update');
    await renderAt('/app/candidates/new');

    await userEvent.type(await screen.findByLabelText('Nombre'), 'Sara');
    await userEvent.type(screen.getByLabelText('Apellidos'), 'Pena');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByTestId('detail-page')).toBeInTheDocument();
    expect(screen.getByTestId('location').textContent).not.toMatch(/\/edit$/);
  });
});
