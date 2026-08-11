export type CandidateStatus = 'new' | 'available' | 'in_process' | 'hired' | 'rejected';

export interface Candidate {
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
}

export type CandidateDraft = Omit<
  Candidate,
  | 'id'
  | 'createdAt'
  | 'updatedAt'
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
