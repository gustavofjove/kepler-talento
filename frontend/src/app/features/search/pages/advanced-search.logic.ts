/**
 * The advanced search's address parameter naming a preset to apply on load (KTL-40). Only the
 * preset's id ever enters the address: its filters, which can hold a name or an e-mail, never do.
 */
export const PRESET_PARAM = 'preset';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Whether a value can be a preset id at all, so a malformed one never reaches the API. */
export function isPresetId(value: string): boolean {
  return GUID.test(value);
}

/** The link that opens the advanced search with one shared preset applied. */
export function presetSearchHref(presetId: string): string {
  return `/app/search?${new URLSearchParams({ [PRESET_PARAM]: presetId }).toString()}`;
}
