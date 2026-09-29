import type { ApiTransport } from '../../../core/http/api-transport';
import { TranslatableError } from '../../../core/i18n/translatable-error';
import { AppError } from '../../../shared/models/error.models';
import {
  CV_DRAFT_FIELDS,
  type CvDraftFields,
  type CvDraftResponse,
} from '../models/candidate-draft.models';

const MAX_CV_SIZE_BYTES = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = new Set(['pdf', 'docx']);

/** Scanning runs inside the request, so allow for the scanner's own timeout. */
const DRAFT_TIMEOUT_MS = 90_000;

/** API refusal codes the create page explains in its own words. */
const REFUSAL_KEYS: Record<string, string> = {
  'cv_draft.format.unsupported': 'candidate.cvDraft.error.format',
  'cv_draft.content.rejected': 'candidate.cvDraft.error.unreadable',
  'cv_draft.rejected': 'candidate.cvDraft.error.unreadable',
  'cv_draft.unreadable': 'candidate.cvDraft.error.unreadable',
  'cv_draft.too_complex': 'candidate.cvDraft.error.tooComplex',
  'cv_draft.size.exceeded': 'candidate.cvDraft.error.size',
  'cv_draft.file.empty': 'candidate.cvDraft.error.empty',
  'cv_draft.file.missing': 'candidate.cvDraft.error.empty',
  'cv_draft.scanner_unavailable': 'candidate.cvDraft.error.scanner',
  'cv_draft.busy': 'candidate.cvDraft.error.busy',
};

/**
 * Suggested values for the create form from a CV (KTL-32). The API scans the file, reads it and
 * forgets it; nothing is kept here either. Client checks are only a courtesy.
 */
export class CandidateDraftService {
  constructor(private readonly transport: ApiTransport) {}

  async extract(file: File, signal?: AbortSignal): Promise<CvDraftResponse> {
    const extension = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!ALLOWED_EXTENSIONS.has(extension)) {
      throw new TranslatableError('candidate.cvDraft.error.format');
    }
    if (file.size === 0) throw new TranslatableError('candidate.cvDraft.error.empty');
    if (file.size > MAX_CV_SIZE_BYTES) throw new TranslatableError('candidate.cvDraft.error.size');

    const form = new FormData();
    form.append('file', file);
    try {
      const response = await this.transport.request<CvDraftResponse>(
        '/candidates/draft-from-document',
        { method: 'POST', body: form, timeoutMs: DRAFT_TIMEOUT_MS, signal },
      );
      return { ...response, fields: knownFields(response.fields) };
    } catch (error) {
      throw toRefusal(error);
    }
  }
}

/** Drops anything outside the six form fields, whatever the API sends. */
function knownFields(fields: CvDraftFields | undefined): CvDraftFields {
  const known: CvDraftFields = {};
  for (const key of CV_DRAFT_FIELDS) {
    const field = fields?.[key];
    if (field && typeof field.value === 'string' && field.value.trim()) {
      known[key] = {
        value: field.value.trim(),
        confidence: field.confidence === 'high' ? 'high' : 'low',
      };
    }
  }
  return known;
}

function toRefusal(error: unknown): unknown {
  if (!(error instanceof AppError)) return error;
  if (error.code === 'TIMEOUT') return new TranslatableError('candidate.cvDraft.error.timeout');
  const issues = (error.details as { errors?: { code?: string }[] } | undefined)?.errors;
  const code = (Array.isArray(issues) ? issues[0]?.code : undefined) ?? error.backendCode;
  const key = code ? REFUSAL_KEYS[code] : undefined;
  return key ? new TranslatableError(key) : error;
}
