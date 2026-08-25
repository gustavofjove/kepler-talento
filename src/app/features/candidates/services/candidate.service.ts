import { signal } from '../../../core/state/signal';
import {
  Candidate,
  CandidateDraft,
  CandidateDocument,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateProgram,
  CandidateSkill,
} from '../models/candidate.models';

const STORAGE_KEY = 'rrhh-candidates';

export class CandidateService {
  readonly candidates = signal<Candidate[]>(this.restore());

  list(includeInactive = false): Candidate[] {
    const candidates = this.candidates();
    return includeInactive ? candidates : candidates.filter((candidate) => candidate.isActive);
  }

  find(id: string): Candidate | undefined {
    return this.candidates().find((candidate) => candidate.id === id);
  }

  create(draft: CandidateDraft): Candidate {
    const now = new Date().toISOString();
    const candidate: Candidate = {
      ...draft,
      id: crypto.randomUUID(),
      createdAt: now,
      updatedAt: now,
      languages: [],
      programs: [],
      education: [],
      experience: [],
      skills: [],
      documents: [],
    };
    this.persist([...this.candidates(), candidate]);
    return candidate;
  }

  update(id: string, draft: CandidateDraft): Candidate {
    const updated = this.candidates().map((candidate) =>
      candidate.id === id
        ? { ...candidate, ...draft, updatedAt: new Date().toISOString() }
        : candidate,
    );
    this.persist(updated);
    const candidate = this.find(id);
    if (!candidate) {
      throw new Error('Candidato no encontrado');
    }
    return candidate;
  }

  deactivate(id: string): void {
    this.persist(
      this.candidates().map((candidate) =>
        candidate.id === id
          ? { ...candidate, isActive: false, updatedAt: new Date().toISOString() }
          : candidate,
      ),
    );
  }

  reactivate(id: string): void {
    this.persist(
      this.candidates().map((candidate) =>
        candidate.id === id
          ? { ...candidate, isActive: true, updatedAt: new Date().toISOString() }
          : candidate,
      ),
    );
  }

  deactivateMany(ids: string[]): number {
    return this.setActiveMany(ids, false);
  }

  reactivateMany(ids: string[]): number {
    return this.setActiveMany(ids, true);
  }

  /** Flips the logical-delete flag, counting only the rows that actually changed. */
  private setActiveMany(ids: string[], isActive: boolean): number {
    if (!ids.length) {
      return 0;
    }

    const idSet = new Set(ids);
    let updated = 0;

    this.persist(
      this.candidates().map((candidate) => {
        if (!idSet.has(candidate.id) || candidate.isActive === isActive) {
          return candidate;
        }

        updated += 1;
        return {
          ...candidate,
          isActive,
          updatedAt: new Date().toISOString(),
        };
      }),
    );

    return updated;
  }

  setLanguages(id: string, languages: CandidateLanguage[]): void {
    this.patch(id, { languages });
  }

  setPrograms(id: string, programs: CandidateProgram[]): void {
    this.patch(id, { programs });
  }

  setEducation(id: string, education: CandidateEducation[]): void {
    this.patch(id, { education });
  }

  setExperience(id: string, experience: CandidateExperience[]): void {
    this.patch(id, { experience });
  }

  setSkills(id: string, skills: CandidateSkill[]): void {
    this.patch(id, { skills });
  }

  addDocument(id: string, document: CandidateDocument): void {
    const candidate = this.find(id);
    if (!candidate) {
      throw new Error('Candidato no encontrado');
    }
    const documents = document.isPrimary
      ? candidate.documents.map((item) => ({ ...item, isPrimary: false }))
      : candidate.documents;
    this.patch(id, { documents: [...documents, document] });
  }

  setDocuments(id: string, documents: CandidateDocument[]): void {
    this.patch(id, { documents });
  }

  private patch(id: string, patch: Partial<Candidate>): void {
    this.persist(
      this.candidates().map((candidate) =>
        candidate.id === id
          ? { ...candidate, ...patch, updatedAt: new Date().toISOString() }
          : candidate,
      ),
    );
  }

  private persist(candidates: Candidate[]): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(candidates));
    this.candidates.set(candidates);
  }

  private restore(): Candidate[] {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      try {
        return JSON.parse(raw) as Candidate[];
      } catch {
        localStorage.removeItem(STORAGE_KEY);
      }
    }
    return [
      {
        id: 'demo-1',
        firstName: 'Laura',
        lastName: 'Garcia',
        phone: '+34 600 100 200',
        email: 'laura.garcia@example.com',
        location: 'Madrid',
        province: 'Madrid',
        country: 'España',
        availability: 'Inmediata',
        status: 'available',
        source: 'LinkedIn',
        notes: 'Perfil administrativo con experiencia internacional.',
        receivedAt: '2026-05-10',
        consentAt: '2026-05-10',
        reviewDueAt: '2027-05-10',
        isActive: true,
        createdAt: '2026-05-10T09:00:00Z',
        updatedAt: '2026-06-15T12:00:00Z',
        languages: [
          { id: 'l1', language: 'Inglés', level: 'B2', certification: 'Cambridge' },
          { id: 'l2', language: 'Francés', level: 'B1' },
        ],
        programs: [
          { id: 'p1', program: 'Excel', level: 'Avanzado', yearsExperience: 5 },
          { id: 'p2', program: 'SAP', level: 'Medio', yearsExperience: 2 },
        ],
        education: [
          {
            id: 'e1',
            educationType: 'Grado',
            degree: 'Grado en ADE',
            institution: 'UCM',
            status: 'Finalizada',
          },
        ],
        experience: [
          {
            id: 'x1',
            company: 'Servicios Norte',
            position: 'Administrativa',
            sector: 'Servicios',
            yearsExperience: 4,
            isCurrent: false,
          },
        ],
        skills: [{ id: 's1', skill: 'Gestión documental', level: 'Alto' }],
        documents: [
          {
            id: 'd1',
            documentType: 'CV',
            originalFilename: 'cv_laura_garcia.pdf',
            mimeType: 'application/pdf',
            sizeBytes: 124_000,
            isPrimary: true,
            uploadedAt: '2026-05-10T10:00:00Z',
          },
        ],
      },
    ];
  }
}
