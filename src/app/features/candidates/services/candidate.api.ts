import type { ApiTransport } from '../../../core/http/api-transport';
import type {
  Candidate,
  CandidateDraft,
  CandidateEducation,
  CandidateExperience,
  CandidateLanguage,
  CandidateProgram,
  CandidateListPage,
  CandidateListQuery,
  CandidateSkill,
} from '../models/candidate.models';

/**
 * The candidate contract the service depends on.
 *
 * There is deliberately no delete call: a candidate is removed logically, through
 * `setActive(id, false, version)`. Tests substitute this interface rather than the
 * network, so a service test says what the service does with a response instead of
 * restating how `fetch` works.
 *
 * Every write takes the version the caller last read and answers with the complete
 * aggregate, so the cache can replace its entry from the response rather than compute a
 * guess about what the server did.
 */
export interface CandidateGateway {
  listPage(query: CandidateListQuery, signal?: AbortSignal): Promise<CandidateListPage>;
  get(id: string): Promise<Candidate>;
  create(draft: CandidateDraft): Promise<Candidate>;
  update(id: string, draft: CandidateDraft, version: number): Promise<Candidate>;
  setActive(id: string, isActive: boolean, version: number): Promise<Candidate>;
  setLanguages(id: string, languages: CandidateLanguage[], version: number): Promise<Candidate>;
  setPrograms(id: string, programs: CandidateProgram[], version: number): Promise<Candidate>;
  setEducation(id: string, education: CandidateEducation[], version: number): Promise<Candidate>;
  setExperience(id: string, experience: CandidateExperience[], version: number): Promise<Candidate>;
  setSkills(id: string, skills: CandidateSkill[], version: number): Promise<Candidate>;
}

/** The HTTP implementation, over the shared API transport. */
export class CandidateApi implements CandidateGateway {
  constructor(private readonly transport: ApiTransport) {}

  /**
   * One page of the candidate list, through the search endpoint (KTL-18). The list is a
   * search with preset criteria, so it shares search's envelope, projection and ordering
   * rules. It is a POST for the same reason search is: the text filter is personal data.
   */
  listPage(query: CandidateListQuery, signal?: AbortSignal): Promise<CandidateListPage> {
    return this.transport.request<CandidateListPage>('/candidates/search', {
      method: 'POST',
      body: JSON.stringify({
        filters: {
          text: query.text,
          statusValues: query.status ? [query.status] : [],
          hasCv: query.hasCv,
        },
        page: query.page,
        pageSize: query.pageSize,
        sortField: query.sortField,
        sortDirection: query.sortDirection,
        includeInactive: query.includeInactive,
      }),
      signal,
    });
  }

  get(id: string): Promise<Candidate> {
    return this.transport.request<Candidate>(`/candidates/${encodeURIComponent(id)}`);
  }

  create(draft: CandidateDraft): Promise<Candidate> {
    return this.transport.request<Candidate>('/candidates', {
      method: 'POST',
      body: JSON.stringify(draft),
    });
  }

  update(id: string, draft: CandidateDraft, version: number): Promise<Candidate> {
    return this.transport.request<Candidate>(`/candidates/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify({ ...draft, version }),
    });
  }

  setActive(id: string, isActive: boolean, version: number): Promise<Candidate> {
    return this.transport.request<Candidate>(`/candidates/${encodeURIComponent(id)}/active`, {
      method: 'PUT',
      body: JSON.stringify({ isActive, version }),
    });
  }

  setLanguages(id: string, languages: CandidateLanguage[], version: number): Promise<Candidate> {
    return this.collection(id, 'languages', { languages, version });
  }

  setPrograms(id: string, programs: CandidateProgram[], version: number): Promise<Candidate> {
    return this.collection(id, 'programs', { programs, version });
  }

  setEducation(id: string, education: CandidateEducation[], version: number): Promise<Candidate> {
    return this.collection(id, 'education', { education, version });
  }

  setExperience(
    id: string,
    experience: CandidateExperience[],
    version: number,
  ): Promise<Candidate> {
    return this.collection(id, 'experience', { experience, version });
  }

  setSkills(id: string, skills: CandidateSkill[], version: number): Promise<Candidate> {
    return this.collection(id, 'skills', { skills, version });
  }

  /**
   * Every collection is written as a complete set against the candidate's version: a
   * single relation has no token of its own, so two editors submitting individual
   * additions could interleave into a collection neither intended.
   */
  private collection(id: string, segment: string, body: unknown): Promise<Candidate> {
    return this.transport.request<Candidate>(`/candidates/${encodeURIComponent(id)}/${segment}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    });
  }
}
