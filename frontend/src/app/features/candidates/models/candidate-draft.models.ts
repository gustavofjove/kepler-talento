/** The create-form fields a CV draft may suggest (KTL-32). */
export const CV_DRAFT_FIELDS = [
  'firstName',
  'lastName',
  'email',
  'phone',
  'location',
  'province',
] as const;

export type CvDraftField = (typeof CV_DRAFT_FIELDS)[number];

export type CvDraftConfidence = 'high' | 'low';

export interface CvDraftSuggestionValue {
  value: string;
  confidence: CvDraftConfidence;
}

/** Only the fields the CV yielded; an absent key means "no suggestion". */
export type CvDraftFields = Partial<Record<CvDraftField, CvDraftSuggestionValue>>;

export type CvDraftOutcome = 'cv_draft.extracted' | 'cv_draft.no_text';

export interface CvDraftResponse {
  draftId: string;
  outcome: CvDraftOutcome;
  fields: CvDraftFields;
}

/**
 * A draft handed to the create form. `nonce` changes with every pick, so picking the same CV
 * twice still applies it.
 */
export interface CvDraftSuggestion {
  nonce: number;
  fields: CvDraftFields;
}
