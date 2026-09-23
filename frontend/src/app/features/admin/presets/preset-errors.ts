import { AppError } from '../../../shared/models/error.models';

/**
 * The API's stable preset problem codes, and the message key each one is shown as. A name
 * conflict and a stale version are both 409s, so the status alone cannot tell the user which
 * of the two happened; the code can.
 */
const PRESET_ERROR_KEYS: Record<string, string> = {
  'search_preset.name.conflict': 'presets.errors.nameConflict',
  'search_preset.concurrency.conflict': 'presets.errors.staleVersion',
  'search_preset.not_found': 'presets.errors.notFound',
};

/** The translation key for a known preset refusal, or undefined for anything else. */
export function presetErrorKey(error: unknown): string | undefined {
  return error instanceof AppError && error.backendCode
    ? PRESET_ERROR_KEYS[error.backendCode]
    : undefined;
}

export function isPresetNotFound(error: unknown): boolean {
  return error instanceof AppError && error.code === 'NOT_FOUND';
}
