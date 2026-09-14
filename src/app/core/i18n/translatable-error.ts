import type { TFunction } from 'i18next';
import { i18n } from './i18n';

/**
 * A validation failure identified by a translation key.
 *
 * `message` is still resolved to Spanish, so callers and tests that read it keep working;
 * components render through `errorText` so the copy follows the active language.
 */
export class TranslatableError extends Error {
  constructor(
    readonly key: string,
    readonly values?: Record<string, unknown>,
  ) {
    super(i18n.t(key, values));
    this.name = 'TranslatableError';
  }
}

export function errorText(err: unknown, t: TFunction): string {
  if (err instanceof TranslatableError) {
    return t(err.key, err.values);
  }
  return (err as Error).message;
}
