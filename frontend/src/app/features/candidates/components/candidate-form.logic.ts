import {
  type Candidate,
  type CandidateDraft,
  type CandidateStatus,
  EMPTY_CANDIDATE_DRAFT,
} from '../models/candidate.models';
import { CV_DRAFT_FIELDS, type CvDraftFields } from '../models/candidate-draft.models';

/**
 * Fills the draft from a CV suggestion (KTL-32), but only where the field is still empty: a value
 * the user typed is never overwritten. Returns the new draft and the suggestions actually used.
 */
export function applySuggestion(
  draft: CandidateDraft,
  fields: CvDraftFields,
): { draft: CandidateDraft; filled: CvDraftFields } {
  const next = { ...draft };
  const filled: CvDraftFields = {};
  for (const key of CV_DRAFT_FIELDS) {
    const suggestion = fields[key];
    if (!suggestion?.value.trim() || draft[key].trim()) continue;
    next[key] = suggestion.value;
    filled[key] = suggestion;
  }
  return { draft: next, filled };
}

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
