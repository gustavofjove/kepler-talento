import { AppError } from '../../../src/app/shared/models/error.models';
import type { CandidateGateway } from '../../../src/app/features/candidates/services/candidate.api';
import { CandidateService } from '../../../src/app/features/candidates/services/candidate.service';
import type {
  Candidate,
  CandidateDraft,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateProgram,
  CandidateSkill,
  CandidateSummary,
} from '../../../src/app/features/candidates/models/candidate.models';

/**
 * In-memory stand-in for the candidate API, so service and component tests exercise the
 * real `CandidateService` without any network access.
 *
 * It reproduces the two server behaviors the service depends on: every write answers with
 * the complete aggregate, and every write advances the candidate's version, so a caller
 * holding a stale one is refused.
 */
export class FakeCandidateApi implements CandidateGateway {
  readonly candidates = new Map<string, Candidate>();
  /** When set, every call rejects with it — used to drive the failure branch. */
  failure: Error | null = null;
  getCalls: string[] = [];
  listCalls = 0;

  seed(candidate: Partial<Candidate> & { id: string }): Candidate {
    const stored = { ...blank(candidate.id), ...candidate };
    this.candidates.set(stored.id, stored);
    return stored;
  }

  async list(includeInactive: boolean): Promise<CandidateSummary[]> {
    this.reject();
    this.listCalls += 1;
    return [...this.candidates.values()]
      .filter((candidate) => includeInactive || candidate.isActive)
      .map(toSummary);
  }

  async get(id: string): Promise<Candidate> {
    this.reject();
    this.getCalls.push(id);
    return clone(this.require(id));
  }

  async create(draft: CandidateDraft): Promise<Candidate> {
    this.reject();
    const now = new Date().toISOString();
    const created: Candidate = {
      ...blank(crypto.randomUUID()),
      ...draft,
      createdAt: now,
      updatedAt: now,
    };
    this.candidates.set(created.id, created);
    return clone(created);
  }

  async update(id: string, draft: CandidateDraft, version: number): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, ...draft }));
  }

  async setActive(id: string, isActive: boolean, version: number): Promise<Candidate> {
    const candidate = this.require(id);
    // Matches the slice: a candidate already in the requested state is left alone, so no
    // version check and no change.
    if (candidate.isActive === isActive) {
      return clone(candidate);
    }
    return this.write(id, version, (current) => ({ ...current, isActive }));
  }

  setLanguages(id: string, languages: CandidateLanguage[], version: number): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, languages }));
  }

  setPrograms(id: string, programs: CandidateProgram[], version: number): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, programs }));
  }

  setEducation(id: string, education: CandidateEducation[], version: number): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, education }));
  }

  setExperience(
    id: string,
    experience: CandidateExperience[],
    version: number,
  ): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, experience }));
  }

  setSkills(id: string, skills: CandidateSkill[], version: number): Promise<Candidate> {
    return this.write(id, version, (candidate) => ({ ...candidate, skills }));
  }

  private async write(
    id: string,
    version: number,
    change: (candidate: Candidate) => Candidate,
  ): Promise<Candidate> {
    this.reject();
    const candidate = this.require(id);
    if (candidate.version !== version) {
      throw new ConflictError(
        'El candidato ha cambiado desde que se cargó. Vuelva a cargarlo e inténtelo de nuevo.',
      );
    }
    const next = {
      ...change(candidate),
      version: candidate.version + 1,
      updatedAt: new Date().toISOString(),
    };
    next.documentCount = next.documents.length;
    next.primaryDocumentId = next.documents.find((document) => document.isPrimary)?.id ?? null;
    this.candidates.set(id, next);
    return clone(next);
  }

  private require(id: string): Candidate {
    const candidate = this.candidates.get(id);
    if (!candidate) {
      throw new NotFoundError('Candidato no encontrado.');
    }
    return candidate;
  }

  private reject(): void {
    if (this.failure) {
      throw this.failure;
    }
  }
}

// The real transport turns a problem response into an AppError, so the double throws the
// same type — otherwise the service's branches on `code` would never be exercised.
export class NotFoundError extends AppError {
  constructor(message: string) {
    super('NOT_FOUND', message);
  }
}

export class ConflictError extends AppError {
  constructor(message: string) {
    super('CONFLICT', message);
  }
}

export interface CandidateTestBed {
  service: CandidateService;
  api: FakeCandidateApi;
}

export function createCandidateTestBed(api = new FakeCandidateApi()): CandidateTestBed {
  return { service: new CandidateService(api), api };
}

/** A candidate service whose list has already loaded, for component tests. */
export async function loadedCandidateService(
  api = new FakeCandidateApi(),
): Promise<CandidateService> {
  const service = new CandidateService(api);
  await service.ensureLoaded();
  return service;
}

function toSummary({
  languages: _languages,
  programs: _programs,
  education: _education,
  experience: _experience,
  skills: _skills,
  documents: _documents,
  ...summary
}: Candidate): CandidateSummary {
  return summary;
}

function clone(candidate: Candidate): Candidate {
  return structuredClone(candidate);
}

function blank(id: string): Candidate {
  const now = new Date().toISOString();
  return {
    id,
    firstName: '',
    lastName: '',
    phone: '',
    email: '',
    location: '',
    province: '',
    country: 'España',
    availability: 'Inmediata',
    status: 'new',
    source: 'Email',
    notes: '',
    receivedAt: '',
    consentAt: '',
    reviewDueAt: '',
    isActive: true,
    createdAt: now,
    updatedAt: now,
    version: 1,
    documentCount: 0,
    primaryDocumentId: null,
    languages: [],
    programs: [],
    education: [],
    experience: [],
    skills: [],
    documents: [],
  };
}
