import { TranslatableError } from '../../../core/i18n/translatable-error';
import { signal } from '../../../core/state/signal';
import type { CandidateNote } from '../models/candidate.models';
import type { CandidateGateway } from './candidate.api';

export interface CandidateNotesState {
  byCandidate: Record<string, CandidateNote[]>;
}

export class CandidateNotesService {
  readonly state = signal<CandidateNotesState>({ byCandidate: {} });

  constructor(private readonly api: CandidateGateway) {}

  list(candidateId: string): CandidateNote[] {
    return this.state().byCandidate[candidateId] ?? [];
  }

  hydrate(candidateId: string, notes: CandidateNote[]): void {
    const current = this.state();
    if (current.byCandidate[candidateId]) return;
    this.store(candidateId, notes);
  }

  async refresh(candidateId: string): Promise<void> {
    this.store(candidateId, await this.api.listNotes(candidateId));
  }

  async add(candidateId: string, body: string): Promise<void> {
    const normalized = this.validate(body);
    const note = await this.api.addNote(candidateId, normalized);
    this.store(candidateId, [note, ...this.list(candidateId)]);
  }

  async update(candidateId: string, noteId: string, body: string): Promise<void> {
    const normalized = this.validate(body);
    const current = this.list(candidateId);
    const note = current.find((item) => item.id === noteId);
    if (!note) throw new TranslatableError('candidate.profile.validation.notFound');
    const updated = await this.api.updateNote(candidateId, noteId, normalized, note.version);
    this.store(
      candidateId,
      current.map((item) => (item.id === noteId ? updated : item)),
    );
  }

  async retire(candidateId: string, noteId: string): Promise<void> {
    const current = this.list(candidateId);
    const note = current.find((item) => item.id === noteId);
    if (!note) return;
    await this.api.setNoteActive(candidateId, noteId, false, note.version);
    this.store(
      candidateId,
      current.filter((item) => item.id !== noteId),
    );
  }

  private validate(body: string): string {
    const normalized = body.trim();
    if (!normalized) throw new TranslatableError('candidate.profile.notes.required');
    if (normalized.length > 4000) {
      throw new TranslatableError('candidate.profile.notes.tooLong');
    }
    return normalized;
  }

  private store(candidateId: string, notes: CandidateNote[]): void {
    const current = this.state();
    this.state.set({
      byCandidate: {
        ...current.byCandidate,
        [candidateId]: [...notes].sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
      },
    });
  }
}
