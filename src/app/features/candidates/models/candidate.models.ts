export type CandidateStatus = 'new' | 'available' | 'in_process' | 'hired' | 'rejected';

export type CandidateLoadStatus = 'idle' | 'loading' | 'loaded' | 'error';

/** The closed set of fields the list and search may be ordered by (KTL-18). */
export type CandidateSortField = 'updatedAt' | 'lastName' | 'status';
export type CandidateSortDirection = 'asc' | 'desc';
export type HasCvFilter = '' | 'yes' | 'no';

/**
 * One page request for the candidate list. Filtering, sorting and paging happen in
 * PostgreSQL; the browser only states what it wants.
 *
 * `sortField` is typed as a string on purpose: a value read from a URL is sent as-is so an
 * unknown field is refused by the API rather than silently replaced here.
 */
export interface CandidateListQuery {
  page: number;
  pageSize: number;
  sortField: string;
  sortDirection: CandidateSortDirection;
  text: string;
  status: CandidateStatus | '';
  hasCv: HasCvFilter;
  includeInactive: boolean;
}

/**
 * A list row: the minimal search projection. No notes, consent, retention, location or
 * source — the list never rendered them, so they no longer cross the network for it.
 */
export interface CandidateListItem {
  candidateId: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string;
  status: CandidateStatus;
  hasPrimaryCv: boolean;
  primaryCvDocumentId?: string | null;
  updatedAt: string;
  isActive: boolean;
}

/** One page plus the server's count. Page navigation reads `totalCount`, never `items.length`. */
export interface CandidateListPage {
  items: CandidateListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/**
 * A candidate as the list endpoint returns it: core fields, no collections.
 *
 * `version` is the concurrency token. Every write carries the one that came with the
 * record it read, so a stale editor is refused rather than silently overwriting a
 * concurrent change.
 */
export interface CandidateSummary {
  id: string;
  firstName: string;
  lastName: string;
  phone: string;
  email: string;
  location: string;
  province: string;
  country: string;
  availability: string;
  status: CandidateStatus;
  source: string;
  notes: string;
  receivedAt: string;
  consentAt: string;
  reviewDueAt: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  version: number;
  /** How many documents the candidate has, so the list can show and filter "tiene CV". */
  documentCount: number;
  /**
   * The candidate's principal CV, or null. An identifier, not personal data — it is what
   * lets the list and the search results offer "Abrir CV" without loading the whole
   * document collection.
   */
  primaryDocumentId: string | null;
}

/** The complete aggregate, as the detail endpoint returns it. */
export interface Candidate extends CandidateSummary {
  languages: CandidateLanguage[];
  programs: CandidateProgram[];
  education: CandidateEducation[];
  experience: CandidateExperience[];
  skills: CandidateSkill[];
  documents: CandidateDocument[];
}

export interface CandidateLanguage {
  id: string;
  language: string;
  level: string;
  certification?: string;
  notes?: string;
}

export interface CandidateProgram {
  id: string;
  program: string;
  level: string;
  yearsExperience?: number;
  notes?: string;
}

export interface CandidateEducation {
  id: string;
  educationType: string;
  degree: string;
  specialty?: string;
  institution: string;
  status: string;
  endYear?: number;
  notes?: string;
}

export interface CandidateExperience {
  id: string;
  company: string;
  position: string;
  sector: string;
  functions?: string;
  startDate?: string;
  endDate?: string;
  yearsExperience?: number;
  isCurrent: boolean;
  notes?: string;
}

export interface CandidateSkill {
  id: string;
  skill: string;
  level: string;
  notes?: string;
}

export interface CandidateDocument {
  id: string;
  documentType: string;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
  isPrimary: boolean;
  uploadedAt: string;
  scanState?: 'PendingScan' | 'Clean' | 'Infected' | 'Rejected' | 'ScanFailed';
  availabilityState?: 'Pending' | 'Available' | 'Refused' | 'Error' | 'LegacyUnavailable';
  failureCode?: string;
}

export type CandidateDraft = Omit<
  Candidate,
  | 'id'
  | 'createdAt'
  | 'updatedAt'
  | 'version'
  | 'documentCount'
  | 'primaryDocumentId'
  | 'languages'
  | 'programs'
  | 'education'
  | 'experience'
  | 'skills'
  | 'documents'
>;

export const EMPTY_CANDIDATE_DRAFT: CandidateDraft = {
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
  receivedAt: new Date().toISOString().slice(0, 10),
  consentAt: '',
  reviewDueAt: '',
  isActive: true,
};
