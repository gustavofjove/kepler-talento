import {
  type Candidate,
  type CandidateDraft,
  type CandidateStatus,
  EMPTY_CANDIDATE_DRAFT,
} from '../models/candidate.models';

/**
 * Pure equivalent of the old `@Input set candidate(...)`. Extracted so it can be
 * unit-tested directly instead of through a component harness.
 */
export function toDraft(value?: Candidate): CandidateDraft {
  if (!value) {
    return structuredClone(EMPTY_CANDIDATE_DRAFT);
  }
  const {
    id: _id,
    createdAt: _createdAt,
    updatedAt: _updatedAt,
    // The concurrency token and the derived document counters are the server's, never
    // the form's: a write carries the version the service holds, not one a form round-tripped.
    version: _version,
    documentCount: _documentCount,
    primaryDocumentId: _primaryDocumentId,
    languages: _languages,
    programs: _programs,
    education: _education,
    experience: _experience,
    skills: _skills,
    documents: _documents,
    ...draft
  } = value;
  return { ...draft, status: draft.status as CandidateStatus };
}
