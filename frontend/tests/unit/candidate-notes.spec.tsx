import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { services, type Services } from '../../src/app/core/di/services';
import { ServicesProvider } from '../../src/app/core/di/services-context';
import { signal } from '../../src/app/core/state/signal';
import { CandidateNotes } from '../../src/app/features/candidates/components/candidate-notes';
import type { CandidateNote } from '../../src/app/features/candidates/models/candidate.models';
import type { CandidateGateway } from '../../src/app/features/candidates/services/candidate.api';
import { CandidateNotesService } from '../../src/app/features/candidates/services/candidate-notes.service';
import { AppError } from '../../src/app/shared/models/error.models';

const note = (overrides: Partial<CandidateNote> = {}): CandidateNote => ({
  id: 'note-1',
  body: 'Seguimiento inicial',
  authorUserId: null,
  authorDisplayName: null,
  createdAt: '2026-09-17T10:00:00Z',
  updatedAt: '2026-09-17T10:00:00Z',
  isActive: true,
  version: 1,
  ...overrides,
});

describe('CandidateNotes', () => {
  it('renders note bodies as text and identifies an unknown author', () => {
    const api = {} as CandidateGateway;
    const candidateNotesService = new CandidateNotesService(api);
    const authService = { profile: signal(null), hasPermission: vi.fn(() => false) };
    render(
      <ServicesProvider
        value={{ ...services, candidateNotesService, authService } as unknown as Services}
      >
        <CandidateNotes candidateId="candidate-1" initialNotes={[note()]} />
      </ServicesProvider>,
    );
    expect(screen.getByText('Seguimiento inicial')).toBeInTheDocument();
    expect(screen.getByText(/Autor desconocido/)).toBeInTheDocument();
  });

  it('shows notes without add, edit or retire controls when read-only, even with update permission', () => {
    const candidateNotesService = new CandidateNotesService({} as CandidateGateway);
    const authService = { profile: signal(null), hasPermission: vi.fn(() => true) };
    render(
      <ServicesProvider
        value={{ ...services, candidateNotesService, authService } as unknown as Services}
      >
        <CandidateNotes candidateId="candidate-1" initialNotes={[note()]} readOnly />
      </ServicesProvider>,
    );
    expect(screen.getByText('Seguimiento inicial')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.queryByTestId('candidate-note-body')).not.toBeInTheDocument();
  });

  it('adds, edits and retires notes through their independent versions', async () => {
    const added = note({ body: 'Nueva', authorDisplayName: 'Ana' });
    const updated = note({ body: 'Editada', authorDisplayName: 'Ana', version: 2 });
    const api = {
      addNote: vi.fn().mockResolvedValue(added),
      updateNote: vi.fn().mockResolvedValue(updated),
      setNoteActive: vi.fn().mockResolvedValue({ ...updated, isActive: false, version: 3 }),
    } as unknown as CandidateGateway;
    const service = new CandidateNotesService(api);

    await service.add('candidate-1', '  Nueva  ');
    await service.update('candidate-1', 'note-1', 'Editada');
    await service.retire('candidate-1', 'note-1');

    expect(api.addNote).toHaveBeenCalledWith('candidate-1', 'Nueva');
    expect(api.updateNote).toHaveBeenCalledWith('candidate-1', 'note-1', 'Editada', 1);
    expect(api.setNoteActive).toHaveBeenCalledWith('candidate-1', 'note-1', false, 2);
    expect(service.list('candidate-1')).toEqual([]);
  });

  it('rejects a blank note before calling the API', async () => {
    const api = { addNote: vi.fn() } as unknown as CandidateGateway;
    const service = new CandidateNotesService(api);
    await expect(service.add('candidate-1', '   ')).rejects.toMatchObject({
      key: 'candidate.profile.notes.required',
    });
    expect(api.addNote).not.toHaveBeenCalled();
  });

  it('propagates a note concurrency conflict without replacing the visible note', async () => {
    const conflict = Object.assign(new Error('La nota ha cambiado.'), { code: 'CONFLICT' });
    const api = { updateNote: vi.fn().mockRejectedValue(conflict) } as unknown as CandidateGateway;
    const service = new CandidateNotesService(api);
    service.hydrate('candidate-1', [note()]);

    await expect(service.update('candidate-1', 'note-1', 'Cambio')).rejects.toBe(conflict);
    expect(service.list('candidate-1')[0].body).toBe('Seguimiento inicial');
  });

  it('surfaces a 409 conflict to the user while editing', async () => {
    const conflict = new AppError('CONFLICT', 'La nota ha cambiado.');
    const api = { updateNote: vi.fn().mockRejectedValue(conflict) } as unknown as CandidateGateway;
    const candidateNotesService = new CandidateNotesService(api);
    const authService = { profile: signal(null), hasPermission: vi.fn(() => true) };
    render(
      <ServicesProvider
        value={{ ...services, candidateNotesService, authService } as unknown as Services}
      >
        <CandidateNotes candidateId="candidate-1" initialNotes={[note()]} />
      </ServicesProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Editar nota' }));
    const editor = screen.getByTestId('candidate-note-edit-note-1');
    await userEvent.clear(editor);
    await userEvent.type(editor, 'Cambio concurrente');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar nota' }));

    expect(await screen.findByText('La nota ha cambiado.')).toBeInTheDocument();
  });
});
