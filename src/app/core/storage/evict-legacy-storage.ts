/**
 * Browser storage keys that held personal data before their feature moved to the API.
 *
 * `rrhh-candidates` held the entire candidate table — identity, contact details,
 * location, consent and retention metadata, status, source and notes — as one JSON blob,
 * in clear, per device.
 */
const SUPERSEDED_KEYS = ['rrhh-candidates'];

/**
 * Removes superseded personal-data keys from the browser.
 *
 * Ceasing to write a key is not the same as removing it. Every browser that ran an
 * earlier build still holds a full clear-text copy of the candidate table, and leaving it
 * there would preserve exactly the exposure the API cutover exists to close.
 *
 * Called at application bootstrap, before and independently of any API call, so the stale
 * data goes even when the backend is unreachable. Wrapped so a storage-denied browser
 * (private mode, blocked site data) still starts.
 */
export function evictSupersededStorage(): void {
  for (const key of SUPERSEDED_KEYS) {
    try {
      localStorage.removeItem(key);
    } catch {
      // Storage is unavailable, so there is nothing stored to remove either.
    }
  }
}
