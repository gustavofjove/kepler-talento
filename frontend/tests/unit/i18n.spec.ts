import { i18n } from '../../src/app/core/i18n/i18n';
import { formatDate, formatNumber } from '../../src/app/core/i18n/format';
import { errorText, TranslatableError } from '../../src/app/core/i18n/translatable-error';
import { catalogLabel } from '../../src/app/features/catalogs/catalog-label';

describe('i18n foundation', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('keeps Spanish active when the browser prefers English', () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('en-US');

    expect(i18n.language).toBe('es');
    expect(i18n.t('candidate.profile.languages.title')).toBe('Idiomas');
  });

  it('interpolates values', () => {
    expect(i18n.t('candidate.profile.yearsCount', { years: 3 })).toBe('3 años');
  });

  it('resolves every CV preview message', () => {
    for (const key of [
      'title',
      'document',
      'loading',
      'unsupported',
      'pending',
      'error',
      'refused',
      'legacyUnavailable',
      'failure',
      'retry',
      'download',
      'viewerLabel',
      'fallback',
    ]) {
      expect(i18n.t(`candidate.profile.preview.${key}`)).not.toContain('candidate.profile.preview');
    }
  });

  // KTL-22: these keys are built at runtime, so a typo would only surface on the screen.
  it('resolves every candidate document state and form status key', () => {
    for (const key of [
      'state.pending',
      'state.available',
      'state.error',
      'state.unavailable',
      'explanation.error',
      'explanation.refused',
      'explanation.legacy',
    ]) {
      expect(i18n.t(`candidate.profile.documents.${key}`)).not.toContain('candidate.profile');
    }
    for (const status of ['new', 'available', 'in_process', 'hired', 'rejected']) {
      expect(i18n.t(`candidate.form.statusOption.${status}`)).not.toContain('candidate.form');
    }
    for (const key of ['titleEdit', 'titleNew', 'saveHint', 'newHint', 'saved']) {
      expect(i18n.t(`candidate.edit.${key}`)).not.toContain('candidate.edit');
    }
    for (const key of [
      'ariaLabel',
      'candidates',
      'positions',
      'admin',
      'presets',
      'edit',
      'newCandidate',
      'newPosition',
      'newPreset',
    ]) {
      expect(i18n.t(`breadcrumb.${key}`)).not.toContain('breadcrumb.');
    }
  });

  // KTL-24: the picker's add labels are reached through the criteria and relation tables.
  it('resolves every catalog picker key', () => {
    for (const kind of ['skill', 'language', 'program', 'tag']) {
      expect(i18n.t(`catalogPicker.add.${kind}`)).toMatch(/^Añadir .+…$/);
    }
    for (const key of ['noResults', 'level', 'pending', 'retry']) {
      expect(i18n.t(`catalogPicker.${key}`)).not.toContain('catalogPicker');
    }
    expect(i18n.t('catalogPicker.remove', { value: 'Inglés' })).toBe('Quitar Inglés');
    expect(i18n.t('search.criteria.mode.label', { label: 'Idiomas' })).toBe(
      'Coincidencia de Idiomas',
    );
  });

  it('fails the test run on a missing key, naming it', () => {
    expect(() => i18n.t('candidate.profile.does.not.exist')).toThrow(
      /candidate\.profile\.does\.not\.exist/,
    );
  });

  describe('TranslatableError', () => {
    it('exposes its key and the Spanish message', () => {
      const error = new TranslatableError('candidate.profile.languages.duplicate');

      expect(error).toBeInstanceOf(Error);
      expect(error.key).toBe('candidate.profile.languages.duplicate');
      expect(error.message).toBe('El candidato ya tiene este idioma registrado.');
    });
  });

  describe('errorText', () => {
    it('resolves a keyed error through the translation function', () => {
      const error = new TranslatableError('candidate.profile.yearsCount', { years: 2 });

      expect(errorText(error, i18n.t)).toBe('2 años');
    });

    it('falls back to the message of a plain error', () => {
      expect(errorText(new Error('Fallo de red'), i18n.t)).toBe('Fallo de red');
    });
  });

  it('labels catalog values with the Spanish name', () => {
    expect(catalogLabel({ nameEs: 'Inglés', nameEn: 'English' })).toBe('Inglés');
  });

  it('formats dates and numbers with the active language', () => {
    expect(
      formatDate('2024-06-01T12:00:00Z', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        timeZone: 'UTC',
      }),
    ).toBe('1 de junio de 2024');
    expect(formatNumber(12345.5)).toBe('12.345,5');
  });
});
