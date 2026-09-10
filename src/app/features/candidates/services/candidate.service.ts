import { signal } from '../../../core/state/signal';
import { AppError, toAppError } from '../../../shared/models/error.models';
import {
  Candidate,
  CandidateDocument,
  CandidateDraft,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateLoadStatus,
  CandidateProgram,
  CandidateSkill,
  CandidateSummary,
} from '../models/candidate.models';
import type { CandidateGateway } from './candidate.api';

export interface CandidateState {
  status: CandidateLoadStatus;
  /** Core fields for every candidate the list endpoint returned. */
  summaries: CandidateSummary[];
  /** Complete aggregates, by identifier, for the candidates that have been opened. */
  aggregates: Record<string, Candidate>;
  /**
   * Identifiers the API answered "not found" for.
   *
   * `find(id)` returning `undefined` means both "not loaded yet" and "no such candidate",
   * and a screen has to tell them apart: one is a spinner, the other is "Candidato no
   * encontrado". This is what separates them.
   */
  missing: string[];
  error?: AppError;
}

/**
 * Candidate records, owned by the API.
 *
 * This is a read-through aggregate cache. `find(id)` and `list()` stay synchronous, so
 * the consuming services and screens keep their existing shape, but they read a load
 * state rather than a browser blob: nothing about a candidate is stored on the device.
 *
 * The gap that leaves is real and is handled rather than papered over. `find(id)`
 * returning `undefined` now means "not loaded yet" as well as "no such candidate", so
 * `status` is exposed alongside the data, `ensureLoaded(id)` is idempotent and shares an
 * in-flight promise per identifier, and the consumer services call it before they read.
 */
export class CandidateService {
  readonly state = signal<CandidateState>({
    status: 'idle',
    summaries: [],
    aggregates: {},
    missing: [],
  });

  private listInFlight: Promise<void> | null = null;
  private readonly aggregatesInFlight = new Map<string, Promise<void>>();

  constructor(private readonly api: CandidateGateway) {}

  get status(): CandidateLoadStatus {
    return this.state().status;
  }

  get error(): AppError | undefined {
    return this.state().error;
  }

  /** Loads the candidate list once. Repeated calls while a load is in flight share it. */
  ensureLoaded(): Promise<void> {
    if (this.state().status === 'loaded') {
      return Promise.resolve();
    }
    this.listInFlight ??= this.loadList().finally(() => {
      this.listInFlight = null;
    });
    return this.listInFlight;
  }

  async reload(): Promise<void> {
    this.listInFlight = null;
    this.state.set({ ...this.state(), status: 'idle' });
    await this.ensureLoaded();
  }

  /**
   * Loads one complete aggregate. Idempotent, and concurrent callers for the same
   * identifier share one request — which is what stops the detail screen's six section
   * components from each issuing their own.
   */
  ensureAggregate(id: string): Promise<void> {
    if (this.state().aggregates[id]) {
      return Promise.resolve();
    }
    const existing = this.aggregatesInFlight.get(id);
    if (existing) {
      return existing;
    }
    const request = this.loadAggregate(id).finally(() => {
      this.aggregatesInFlight.delete(id);
    });
    this.aggregatesInFlight.set(id, request);
    return request;
  }

  /**
   * Loads every listed candidate's aggregate.
   *
   * Only the advanced search uses this, because searching over languages, programs and
   * skills needs the collections the list endpoint deliberately omits. It is a stopgap
   * with a known cost — one request per candidate — and it is superseded by KTL-10, which
   * moves search to the server where it belongs. It is not on any screen's load path.
   */
  async ensureAllAggregates(): Promise<void> {
    await this.ensureLoaded();
    await Promise.all(this.state().summaries.map((summary) => this.ensureAggregate(summary.id)));
  }

  /** A synchronous read of the cache. `undefined` means "not loaded" or "no such candidate". */
  find(id: string): Candidate | undefined {
    return this.state().aggregates[id];
  }

  /**
   * What a screen showing one candidate should render. Separates "still loading" from
   * "genuinely not there", which `find` alone cannot express.
   */
  aggregateStatus(id: string): 'loading' | 'loaded' | 'missing' | 'error' {
    const state = this.state();
    if (state.aggregates[id]) {
      return 'loaded';
    }
    if (state.missing.includes(id)) {
      return 'missing';
    }
    return state.status === 'error' ? 'error' : 'loading';
  }

  list(includeInactive = false): CandidateSummary[] {
    const summaries = this.state().summaries;
    return includeInactive ? summaries : summaries.filter((candidate) => candidate.isActive);
  }

  async create(draft: CandidateDraft): Promise<Candidate> {
    return this.absorb(await this.api.create(draft));
  }

  async update(id: string, draft: CandidateDraft): Promise<Candidate> {
    return this.absorb(await this.api.update(id, draft, this.versionOf(id)));
  }

  async deactivate(id: string): Promise<Candidate> {
    return this.absorb(await this.api.setActive(id, false, this.versionOf(id)));
  }

  async reactivate(id: string): Promise<Candidate> {
    return this.absorb(await this.api.setActive(id, true, this.versionOf(id)));
  }

  deactivateMany(ids: string[]): Promise<number> {
    return this.setActiveMany(ids, false);
  }

  reactivateMany(ids: string[]): Promise<number> {
    return this.setActiveMany(ids, true);
  }

  async setLanguages(id: string, languages: CandidateLanguage[]): Promise<Candidate> {
    return this.absorb(await this.api.setLanguages(id, languages, this.versionOf(id)));
  }

  async setPrograms(id: string, programs: CandidateProgram[]): Promise<Candidate> {
    return this.absorb(await this.api.setPrograms(id, programs, this.versionOf(id)));
  }

  async setEducation(id: string, education: CandidateEducation[]): Promise<Candidate> {
    return this.absorb(await this.api.setEducation(id, education, this.versionOf(id)));
  }

  async setExperience(id: string, experience: CandidateExperience[]): Promise<Candidate> {
    return this.absorb(await this.api.setExperience(id, experience, this.versionOf(id)));
  }

  async setSkills(id: string, skills: CandidateSkill[]): Promise<Candidate> {
    return this.absorb(await this.api.setSkills(id, skills, this.versionOf(id)));
  }

  async setDocuments(id: string, documents: CandidateDocument[]): Promise<Candidate> {
    return this.absorb(await this.api.setDocuments(id, documents, this.versionOf(id)));
  }

  async addDocument(id: string, document: CandidateDocument): Promise<Candidate> {
    const candidate = this.require(id);
    // At most one primary document: adding a primary one demotes the current holder in
    // the same submitted set, so the server never sees two.
    const existing = document.isPrimary
      ? candidate.documents.map((item) => ({ ...item, isPrimary: false }))
      : candidate.documents;
    return this.setDocuments(id, [...existing, document]);
  }

  /**
   * Sequential per-candidate calls, counting the ones that actually changed — today's
   * contract. A refusal on one candidate leaves the rest applied, and the count the UI
   * reports is the count that really happened.
   */
  private async setActiveMany(ids: string[], isActive: boolean): Promise<number> {
    let changed = 0;
    for (const id of ids) {
      const before = this.summaryOf(id);
      if (!before || before.isActive === isActive) {
        continue;
      }
      const after = await this.api.setActive(id, isActive, before.version);
      this.absorb(after);
      if (after.isActive === isActive) {
        changed += 1;
      }
    }
    return changed;
  }

  /** Replaces the cached summary and aggregate from a response, never from a local guess. */
  private absorb(candidate: Candidate): Candidate {
    const current = this.state();
    const summary = toSummary(candidate);
    const summaries = current.summaries.some((item) => item.id === candidate.id)
      ? current.summaries.map((item) => (item.id === candidate.id ? summary : item))
      : [...current.summaries, summary];
    this.state.set({
      ...current,
      summaries,
      aggregates: { ...current.aggregates, [candidate.id]: candidate },
    });
    return candidate;
  }

  /**
   * The version a write must carry. The aggregate is preferred because a screen that
   * opened a candidate holds the newer token; the summary is the fallback for the list
   * screen's bulk actions, which never load aggregates.
   */
  private versionOf(id: string): number {
    const candidate = this.state().aggregates[id] ?? this.summaryOf(id);
    if (!candidate) {
      throw new AppError('NOT_FOUND', 'Candidato no encontrado.');
    }
    return candidate.version;
  }

  private summaryOf(id: string): CandidateSummary | undefined {
    return this.state().summaries.find((candidate) => candidate.id === id);
  }

  private require(id: string): Candidate {
    const candidate = this.find(id);
    if (!candidate) {
      throw new AppError('NOT_FOUND', 'Candidato no encontrado.');
    }
    return candidate;
  }

  private async loadList(): Promise<void> {
    this.state.set({ ...this.state(), status: 'loading', error: undefined });
    try {
      // Inactive candidates are fetched too: the list screen offers "incluir inactivos"
      // as a filter over what it already holds, and a removed candidate must stay
      // reachable so it can be restored.
      const summaries = await this.api.list(true);
      this.state.set({ ...this.state(), status: 'loaded', summaries, error: undefined });
    } catch (error) {
      // No local fallback. A stale private copy of personal data is worse than a visible
      // failure, and this service exists to stop keeping one.
      this.state.set({
        status: 'error',
        summaries: [],
        aggregates: {},
        missing: [],
        error: toAppError(error),
      });
    }
  }

  private async loadAggregate(id: string): Promise<void> {
    try {
      this.absorb(await this.api.get(id));
    } catch (error) {
      const appError = toAppError(error);
      if (appError.code === 'NOT_FOUND') {
        // A candidate that genuinely does not exist is not a failure of the screen. It is
        // recorded as missing so the caller can render "no encontrado" instead of a
        // spinner that never resolves.
        const current = this.state();
        this.state.set({ ...current, missing: [...current.missing, id] });
        return;
      }
      this.state.set({ ...this.state(), status: 'error', error: appError });
    }
  }
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
