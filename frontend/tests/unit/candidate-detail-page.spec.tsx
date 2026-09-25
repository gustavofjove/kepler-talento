import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider, type DataRouter } from 'react-router';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateDetailPage } from '../../src/app/features/candidates/pages/candidate-detail-page';
import { CandidateEditRedirect } from '../../src/app/features/candidates/pages/candidate-edit-redirect';
import { CandidateNotesService } from '../../src/app/features/candidates/services/candidate-notes.service';
import { CandidateRelationsService } from '../../src/app/features/candidates/services/candidate-relations.service';
import type { CandidateDocument } from '../../src/app/features/candidates/models/candidate.models';
import type { DocumentService } from '../../src/app/features/documents/services/document.service';
import { createCandidateTestBed, type CandidateTestBed } from './support/candidate-doubles';
import { loadedCatalogService } from './support/catalog-doubles';

const EDITABLE = ['main', 'competencies', 'education', 'experience', 'notes', 'documents'];
const editButton = (panel: string) => screen.queryByTestId(`candidate-panel-${panel}-edit`);

describe('CandidateDetailPage', () => {
  let bed: CandidateTestBed;
  let granted: Set<string>;
  let listed: CandidateDocument[];
  let router: DataRouter;
  const toastService = { show: vi.fn() };
  const confirm = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    listed = [];
    bed = createCandidateTestBed();
    bed.api.seed({ id: 'c1', firstName: 'Ona', lastName: 'Marti' });
    granted = new Set(['candidates.read']);
    confirm.mockResolvedValue(true);
  });

  const renderAt = async (path: string) => {
    const catalogService = await loadedCatalogService();
    const documentService = {
      list: vi.fn().mockResolvedValue(listed),
      observeUntilSettled: vi.fn(),
      // Never settles: the specs only look at where the preview is placed.
      openPreview: vi.fn(() => new Promise(() => {})),
    } as unknown as DocumentService;
    const authService = {
      profile: signal(null),
      hasPermission: vi.fn((permission: string) => granted.has(permission)),
    };
    // A data router: the page guards leaving with unsaved changes through `useBlocker`.
    router = createMemoryRouter(
      [
        { path: '/app/candidates', element: <p data-testid="list-page" /> },
        { path: '/app/candidates/:id', element: <CandidateDetailPage /> },
        { path: '/app/candidates/:id/edit', element: <CandidateEditRedirect /> },
      ],
      { initialEntries: [path] },
    );
    render(
      <ServicesProvider
        value={
          {
            ...services,
            candidateService: bed.service,
            candidateRelationsService: new CandidateRelationsService(bed.service),
            candidateNotesService: new CandidateNotesService(bed.api),
            catalogService,
            documentService,
            toastService,
            confirmDialogService: { confirm },
            authService,
          } as unknown as Services
        }
      >
        <RouterProvider router={router} />
      </ServicesProvider>,
    );
  };

  const loaded = () => screen.findByRole('heading', { level: 1, name: 'Ona Marti' });
  const asEditor = () =>
    ['candidates.update', 'documents.upload'].forEach((permission) => granted.add(permission));

  describe('breadcrumb (KTL-23)', () => {
    const trail = () => within(screen.getByTestId('breadcrumb'));
    const trailText = () =>
      trail()
        .getAllByRole('listitem')
        .map((item) => item.textContent);

    it('reads «Candidatos › name» with the name as the current page', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      expect(trailText()).toEqual(['Candidatos', 'Ona Marti']);
      expect(trail().getByRole('link', { name: 'Candidatos' })).toHaveAttribute(
        'href',
        '/app/candidates',
      );
      expect(trail().getByText('Ona Marti')).toHaveAttribute('aria-current', 'page');
      expect(trail().getAllByRole('link')).toHaveLength(1);
    });

    it('shows the stored name, not an unsaved draft', async () => {
      asEditor();
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('main')!);
      const firstName = screen.getByLabelText('Nombre');
      await userEvent.clear(firstName);
      await userEvent.type(firstName, 'Borrador');

      expect(trailText()).toEqual(['Candidatos', 'Ona Marti']);
    });

    it('offers only the list while the candidate loads', async () => {
      vi.spyOn(bed.api, 'get').mockReturnValue(new Promise(() => {}));
      await renderAt('/app/candidates/c1');

      expect(await screen.findByText('Cargando candidato…')).toBeInTheDocument();
      expect(trailText()).toEqual(['Candidatos']);
      expect(trail().getByRole('link', { name: 'Candidatos' })).toBeInTheDocument();
    });

    it('keeps the way back when the candidate fails to load', async () => {
      bed.api.failure = new Error('Servicio no disponible');
      await renderAt('/app/candidates/c1');

      expect(await screen.findByTestId('candidate-detail-error')).toBeInTheDocument();
      expect(trailText()).toEqual(['Candidatos']);
    });

    it('keeps the way back when the candidate does not exist', async () => {
      await renderAt('/app/candidates/missing');

      expect(await screen.findByRole('heading', { level: 1 })).not.toHaveTextContent('Ona');
      expect(screen.queryByTestId('candidate-detail-error')).not.toBeInTheDocument();
      expect(trailText()).toEqual(['Candidatos']);
    });
  });

  it('stacks every panel full width, with a read-only Competencias panel (KTL-27)', async () => {
    await renderAt('/app/candidates/c1');
    await loaded();

    const order = [
      'Datos principales',
      'Auditoría',
      'Competencias',
      'Formación',
      'Experiencia',
      'Notas personalizadas',
      'Documentos',
    ];
    const headings = screen
      .getAllByRole('heading')
      .map((heading) => heading.textContent)
      .filter((text) => order.includes(text ?? ''));
    expect(headings).toEqual(order);
    expect(screen.getByTestId('candidate-competencies').parentElement).not.toHaveClass('two');

    const panel = within(screen.getByTestId('candidate-competencies'));
    expect(panel.queryByRole('button')).toBeNull();
    expect(panel.getByText('Sin programas asociados.')).toBeInTheDocument();
  });

  describe('CV preview (KTL-28)', () => {
    it('places the preview in the aside, after the panels', async () => {
      granted.add('documents.download');
      listed = [
        {
          id: 'd1',
          documentType: 'CV',
          originalFilename: 'cv.pdf',
          mimeType: 'application/pdf',
          sizeBytes: 10,
          isPrimary: true,
          uploadedAt: '2026-01-01T00:00:00Z',
          availabilityState: 'Available',
        },
      ];
      asEditor();
      await renderAt('/app/candidates/c1');

      const preview = await screen.findByTestId('candidate-cv-preview');
      expect(preview.parentElement).toHaveClass('page-split__aside');
      expect(preview.closest('.page-split__layout')?.firstElementChild).toHaveClass(
        'page-split__main',
      );
      // And it stays there while a panel is edited.
      await userEvent.click(editButton('main')!);
      expect(screen.getByTestId('candidate-cv-preview')).toBe(preview);
    });

    it('leaves the aside empty without the download permission', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      expect(screen.queryByTestId('candidate-cv-preview')).not.toBeInTheDocument();
      expect(document.querySelector('.page-split__aside')).toBeEmptyDOMElement();
    });
  });

  describe('who may edit (KTL-29)', () => {
    it('offers no «Editar» and no form to a reader', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      for (const panel of EDITABLE) expect(editButton(panel)).toBeNull();
      expect(document.querySelector('form')).toBeNull();
      expect(screen.queryByRole('button', { name: /baja lógica/i })).toBeNull();
    });

    it('offers «Editar» on every editable panel to an editor, never on Auditoría', async () => {
      asEditor();
      await renderAt('/app/candidates/c1');
      await loaded();

      for (const panel of EDITABLE) expect(editButton(panel)).toBeInTheDocument();
      expect(screen.getAllByRole('button', { name: /^Editar / })).toHaveLength(EDITABLE.length);
      expect(screen.getByRole('button', { name: 'Editar Formación' })).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Editar' })).toBeNull();
      expect(document.querySelector('form')).toBeNull();
    });

    it('keeps Documentos read-only for an editor without the upload permission', async () => {
      granted.add('candidates.update');
      await renderAt('/app/candidates/c1');
      await loaded();

      expect(editButton('documents')).toBeNull();
      expect(editButton('education')).toBeInTheDocument();
      expect(screen.getByTestId('candidate-documents')).toBeInTheDocument();
    });

    it('offers only Documentos to an uploader who may not update candidates', async () => {
      granted.add('documents.upload');
      await renderAt('/app/candidates/c1');
      await loaded();

      expect(EDITABLE.filter((panel) => editButton(panel))).toEqual(['documents']);
    });

    it('offers no relation or note editing on a removed candidate, and says why', async () => {
      bed.api.seed({ id: 'c9', firstName: 'Ida', lastName: 'Baja', isActive: false });
      asEditor();
      await renderAt('/app/candidates/c9');
      await screen.findByRole('heading', { level: 1, name: 'Ida Baja' });

      expect(EDITABLE.filter((panel) => editButton(panel))).toEqual(['main', 'documents']);
      expect(screen.getByTestId('candidate-removed-hint')).toHaveTextContent(/reactívalo/);
    });
  });

  describe('panel edit mode (KTL-29)', () => {
    beforeEach(asEditor);

    it('switches a panel to its editor in place and moves focus into it', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      await userEvent.click(editButton('education')!);

      const education = screen.getByTestId('candidate-education');
      expect(within(education).getByLabelText('Tipo')).toHaveFocus();
      expect(screen.getByTestId('candidate-panel-education-save')).toBeInTheDocument();
      expect(screen.getByTestId('candidate-panel-education-cancel')).toBeInTheDocument();
      // Every other panel stays read-only.
      expect(document.querySelectorAll('form')).toHaveLength(1);
      expect(editButton('education')).toBeNull();
    });

    it('stages education and writes it once on «Guardar»', async () => {
      const setEducation = vi.spyOn(bed.api, 'setEducation');
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('education')!);

      const education = within(screen.getByTestId('candidate-education'));
      await userEvent.selectOptions(education.getByLabelText('Tipo'), 'Grado');
      await userEvent.type(education.getByLabelText('Titulación'), 'Grado en ADE');
      await userEvent.selectOptions(education.getByLabelText('Estado'), 'Finalizada');
      await userEvent.click(education.getByRole('button', { name: 'Añadir formación' }));
      expect(education.getByText('Grado en ADE')).toBeInTheDocument();
      expect(setEducation).not.toHaveBeenCalled();

      await userEvent.click(screen.getByTestId('candidate-panel-education-save'));

      await waitFor(() => expect(setEducation).toHaveBeenCalledTimes(1));
      expect(bed.api.candidates.get('c1')?.education).toEqual([
        expect.objectContaining({ degree: 'Grado en ADE' }),
      ]);
      expect(toastService.show).toHaveBeenCalledWith('Formación guardada.', 'success');
      await waitFor(() => expect(editButton('education')).toHaveFocus());
      expect(screen.getByTestId('candidate-education')).toHaveTextContent('Grado en ADE');
    });

    it('refuses an invalid entry before it joins the draft', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('education')!);

      const education = within(screen.getByTestId('candidate-education'));
      await userEvent.click(education.getByRole('button', { name: 'Añadir formación' }));

      expect(education.getByText(/titulación es obligatoria/i)).toBeInTheDocument();
      expect(education.getByText('Sin formación registrada.')).toBeInTheDocument();
    });

    it('discards the draft on «Cancelar» without writing', async () => {
      bed.api.seed({
        id: 'c2',
        firstName: 'Eva',
        lastName: 'Sanz',
        experience: [
          {
            id: 'x1',
            company: 'Acme',
            position: 'Analista',
            sector: 'Servicios',
            isCurrent: false,
          },
        ],
      });
      const setExperience = vi.spyOn(bed.api, 'setExperience');
      await renderAt('/app/candidates/c2');
      await screen.findByRole('heading', { level: 1, name: 'Eva Sanz' });
      await userEvent.click(editButton('experience')!);

      const experience = within(screen.getByTestId('candidate-experience'));
      await userEvent.click(experience.getByRole('button', { name: 'Quitar' }));
      expect(experience.queryByText('Analista')).toBeNull();
      await userEvent.click(screen.getByTestId('candidate-panel-experience-cancel'));

      expect(setExperience).not.toHaveBeenCalled();
      expect(screen.getByTestId('candidate-experience')).toHaveTextContent('Analista');
      expect(editButton('experience')).toHaveFocus();
    });

    it('saves the core record, updates the header and announces it', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('main')!);

      const email = screen.getByLabelText('Email');
      await userEvent.clear(email);
      await userEvent.type(email, 'ona@example.com');
      await userEvent.click(screen.getByTestId('candidate-panel-main-save'));

      await waitFor(() =>
        expect(toastService.show).toHaveBeenCalledWith('Datos principales guardados.', 'success'),
      );
      expect(bed.api.candidates.get('c1')?.email).toBe('ona@example.com');
      expect(screen.getByRole('link', { name: 'ona@example.com' })).toBeInTheDocument();
      expect(screen.queryByLabelText('Email')).toBeNull();
    });

    it('writes nothing when the core record has no name', async () => {
      const update = vi.spyOn(bed.api, 'update');
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('main')!);

      await userEvent.clear(screen.getByLabelText('Nombre'));
      await userEvent.click(screen.getByTestId('candidate-panel-main-save'));

      expect(await screen.findByText(/nombre y apellidos/i)).toBeInTheDocument();
      expect(update).not.toHaveBeenCalled();
    });

    it('keeps the draft when someone else changed the candidate', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('main')!);
      const lastName = screen.getByLabelText('Apellidos');
      await userEvent.clear(lastName);
      await userEvent.type(lastName, 'Martí');

      // Another user's write advances the stored version behind the page's back.
      bed.api.candidates.get('c1')!.version += 1;
      await userEvent.click(screen.getByTestId('candidate-panel-main-save'));

      await waitFor(() =>
        expect(toastService.show).toHaveBeenCalledWith(expect.any(String), 'error'),
      );
      expect(screen.getByLabelText('Apellidos')).toHaveValue('Martí');
      expect(bed.api.candidates.get('c1')?.lastName).toBe('Marti');
    });

    it('saves panels in turn without conflicting with itself', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      await userEvent.click(editButton('competencies')!);
      await userEvent.click(screen.getByTestId('candidate-skill-add'));
      await userEvent.type(screen.getByTestId('candidate-skill-input'), 'compr');
      await userEvent.keyboard('{ArrowDown}{Enter}');
      await userEvent.click(screen.getByTestId('candidate-panel-competencies-save'));
      await waitFor(() => expect(editButton('competencies')).toBeInTheDocument());

      await userEvent.click(editButton('main')!);
      const lastName = screen.getByLabelText('Apellidos');
      await userEvent.clear(lastName);
      await userEvent.type(lastName, 'Martí');
      await userEvent.click(screen.getByTestId('candidate-panel-main-save'));

      await waitFor(() => expect(bed.api.candidates.get('c1')?.lastName).toBe('Martí'));
      expect(bed.api.candidates.get('c1')?.skills.map((item) => item.skill)).toEqual(['Compras']);
    });
  });

  describe('Competencias (KTL-29)', () => {
    beforeEach(asEditor);

    // Through the real relations service and the fake API, not a service double.
    it('stages a language at the lowest level and saves it with the chosen level', async () => {
      const setLanguages = vi.spyOn(bed.api, 'setLanguages');
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('competencies')!);

      await userEvent.click(screen.getByTestId('candidate-language-add'));
      await userEvent.type(screen.getByTestId('candidate-language-input'), 'ingl');
      await userEvent.keyboard('{ArrowDown}{Enter}');
      expect(screen.getByTestId('candidate-language-chip')).toHaveTextContent('A1');

      await userEvent.click(screen.getByTestId('candidate-language-chip'));
      const editor = await screen.findByTestId('candidate-language-editor');
      await userEvent.click(within(editor).getByRole('radio', { name: 'C1' }));
      expect(setLanguages).not.toHaveBeenCalled();

      await userEvent.click(screen.getByTestId('candidate-panel-competencies-save'));

      await waitFor(() => expect(setLanguages).toHaveBeenCalledTimes(1));
      expect(bed.api.candidates.get('c1')?.languages).toEqual([
        expect.objectContaining({ language: 'Inglés', level: 'C1' }),
      ]);
      expect(toastService.show).toHaveBeenCalledWith('Competencias guardadas.', 'success');
    });

    it('writes only the families that changed', async () => {
      const setSkills = vi.spyOn(bed.api, 'setSkills');
      const setLanguages = vi.spyOn(bed.api, 'setLanguages');
      const setPrograms = vi.spyOn(bed.api, 'setPrograms');
      const setTags = vi.spyOn(bed.api, 'setTags');
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('competencies')!);

      await userEvent.click(screen.getByTestId('candidate-skill-add'));
      await userEvent.type(screen.getByTestId('candidate-skill-input'), 'compr');
      await userEvent.keyboard('{ArrowDown}{Enter}');
      await userEvent.click(screen.getByTestId('candidate-panel-competencies-save'));

      await waitFor(() => expect(setSkills).toHaveBeenCalledTimes(1));
      expect(setLanguages).not.toHaveBeenCalled();
      expect(setPrograms).not.toHaveBeenCalled();
      expect(setTags).not.toHaveBeenCalled();
    });

    it('keeps a refused family in the draft and retries only it', async () => {
      const setSkills = vi.spyOn(bed.api, 'setSkills');
      const setLanguages = vi
        .spyOn(bed.api, 'setLanguages')
        .mockRejectedValueOnce(new Error('Idioma rechazado.'));
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('competencies')!);

      await userEvent.click(screen.getByTestId('candidate-skill-add'));
      await userEvent.type(screen.getByTestId('candidate-skill-input'), 'compr');
      await userEvent.keyboard('{ArrowDown}{Enter}');
      await userEvent.click(screen.getByTestId('candidate-language-add'));
      await userEvent.type(screen.getByTestId('candidate-language-input'), 'ingl');
      await userEvent.keyboard('{ArrowDown}{Enter}');
      await userEvent.click(screen.getByTestId('candidate-panel-competencies-save'));

      expect(await screen.findByTestId('candidate-language-error')).toHaveTextContent(
        'Idioma rechazado.',
      );
      expect(bed.api.candidates.get('c1')?.skills).toHaveLength(1);
      expect(screen.getByTestId('candidate-panel-competencies-save')).toBeInTheDocument();

      await userEvent.click(screen.getByTestId('candidate-panel-competencies-save'));

      await waitFor(() => expect(editButton('competencies')).toBeInTheDocument());
      expect(setSkills).toHaveBeenCalledTimes(1);
      expect(setLanguages).toHaveBeenCalledTimes(2);
      expect(bed.api.candidates.get('c1')?.languages).toHaveLength(1);
    });

    it('refuses a duplicate value in the draft', async () => {
      bed.api.seed({
        id: 'c3',
        firstName: 'Ana',
        lastName: 'Ruiz',
        skills: [{ id: 's1', skill: 'Compras', level: 'Medio' }],
      });
      await renderAt('/app/candidates/c3');
      await screen.findByRole('heading', { level: 1, name: 'Ana Ruiz' });
      await userEvent.click(editButton('competencies')!);

      // The picker offers only values not held, so a duplicate can only arrive by change;
      // the per-entry validator is covered by the service spec, and the picker's filter here.
      await userEvent.click(screen.getByTestId('candidate-skill-add'));
      await userEvent.type(screen.getByTestId('candidate-skill-input'), 'compras');
      expect(screen.queryByRole('option', { name: 'Compras' })).toBeNull();
    });
  });

  describe('one panel at a time and unsaved changes (KTL-29)', () => {
    beforeEach(asEditor);

    const dirtyEducation = async () => {
      await userEvent.click(editButton('education')!);
      const education = within(screen.getByTestId('candidate-education'));
      await userEvent.selectOptions(education.getByLabelText('Tipo'), 'Grado');
      await userEvent.type(education.getByLabelText('Titulación'), 'Grado en ADE');
      await userEvent.selectOptions(education.getByLabelText('Estado'), 'Finalizada');
      await userEvent.click(education.getByRole('button', { name: 'Añadir formación' }));
    };

    it('closes a clean panel when another one opens', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();

      await userEvent.click(editButton('education')!);
      await userEvent.click(editButton('experience')!);

      expect(confirm).not.toHaveBeenCalled();
      expect(editButton('education')).toBeInTheDocument();
      expect(screen.getByTestId('candidate-panel-experience-save')).toBeInTheDocument();
    });

    it('asks before discarding unsaved changes, and keeps them when declined', async () => {
      confirm.mockResolvedValue(false);
      await renderAt('/app/candidates/c1');
      await loaded();
      await dirtyEducation();

      await userEvent.click(editButton('experience')!);

      expect(confirm).toHaveBeenCalledWith(expect.objectContaining({ title: 'Descartar cambios' }));
      expect(screen.getByTestId('candidate-panel-education-save')).toBeInTheDocument();
      expect(screen.getByTestId('candidate-education')).toHaveTextContent('Grado en ADE');
    });

    it('discards unsaved changes when confirmed and opens the other panel', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await dirtyEducation();

      await userEvent.click(editButton('experience')!);

      await waitFor(() =>
        expect(screen.getByTestId('candidate-panel-experience-save')).toBeInTheDocument(),
      );
      expect(screen.getByTestId('candidate-education')).not.toHaveTextContent('Grado en ADE');
    });

    it('asks before leaving the page with unsaved changes', async () => {
      confirm.mockResolvedValue(false);
      await renderAt('/app/candidates/c1');
      await loaded();
      await dirtyEducation();

      await act(() => router.navigate('/app/candidates'));

      expect(confirm).toHaveBeenCalledWith(expect.objectContaining({ title: 'Salir sin guardar' }));
      expect(router.state.location.pathname).toBe('/app/candidates/c1');
      expect(screen.getByTestId('candidate-education')).toHaveTextContent('Grado en ADE');
    });

    it('leaves when confirmed', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await dirtyEducation();

      await act(() => router.navigate('/app/candidates'));

      expect(await screen.findByTestId('list-page')).toBeInTheDocument();
    });

    it('leaves without asking when nothing is unsaved', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('education')!);

      await act(() => router.navigate('/app/candidates'));

      expect(confirm).not.toHaveBeenCalled();
      expect(await screen.findByTestId('list-page')).toBeInTheDocument();
    });

    it('arms the browser prompt only while a panel is dirty', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      const clean = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(clean);
      expect(clean.defaultPrevented).toBe(false);

      await dirtyEducation();
      const dirty = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(dirty);
      expect(dirty.defaultPrevented).toBe(true);
    });

    it('counts typed note text as unsaved', async () => {
      confirm.mockResolvedValue(false);
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('notes')!);
      await userEvent.type(screen.getByTestId('candidate-note-body'), 'Llamar el lunes');

      await userEvent.click(editButton('main')!);

      expect(confirm).toHaveBeenCalled();
      expect(screen.getByTestId('candidate-panel-notes-done')).toBeInTheDocument();
    });

    it('closes an action panel with «Hecho»', async () => {
      await renderAt('/app/candidates/c1');
      await loaded();
      await userEvent.click(editButton('documents')!);
      expect(screen.getByTestId('document-file')).toBeInTheDocument();

      await userEvent.click(screen.getByTestId('candidate-panel-documents-done'));

      expect(screen.queryByTestId('document-file')).toBeNull();
      expect(editButton('documents')).toHaveFocus();
    });
  });

  it('redirects the former edit address to the candidate page (KTL-29)', async () => {
    await renderAt('/app/candidates/c1/edit');

    await loaded();
    expect(router.state.location.pathname).toBe('/app/candidates/c1');
    expect(router.state.historyAction).toBe('REPLACE');
  });
});
