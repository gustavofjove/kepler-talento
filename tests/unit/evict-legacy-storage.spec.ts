import { evictSupersededStorage } from '../../src/app/core/storage/evict-legacy-storage';

describe('superseded browser storage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  // Ceasing to write the key is not the same as removing it: every browser that ran an
  // earlier build still holds a full clear-text copy of the candidate table, which is
  // exactly the exposure the API cutover exists to close.
  it('removes the candidate table an earlier build left in the browser', () => {
    localStorage.setItem(
      'rrhh-candidates',
      JSON.stringify([{ id: 'demo-1', firstName: 'Laura', email: 'laura.garcia@example.com' }]),
    );

    evictSupersededStorage();

    expect(localStorage.getItem('rrhh-candidates')).toBeNull();
  });

  it('leaves other keys alone', () => {
    localStorage.setItem('rrhh-search-presets', '[]');

    evictSupersededStorage();

    expect(localStorage.getItem('rrhh-search-presets')).toBe('[]');
  });

  it('is safe to run when the key was never there', () => {
    expect(() => evictSupersededStorage()).not.toThrow();
  });

  // The point is that the stale personal data goes even when storage is denied or the
  // backend is unreachable — so a throwing accessor must not stop the application booting.
  it('starts anyway when the browser denies storage access', () => {
    const removeItem = vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('storage denegado');
    });

    expect(() => evictSupersededStorage()).not.toThrow();

    removeItem.mockRestore();
  });
});
