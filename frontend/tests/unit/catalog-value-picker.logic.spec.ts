import {
  filterOptions,
  normalizeName,
  pickerTestIds,
} from '../../src/app/features/catalogs/components/catalog-value-picker.logic';

describe('catalog value picker logic', () => {
  const options = ['Inglés', 'Francés', 'Alemán', 'Italiano', 'Portugués'];

  it('folds case and accents', () => {
    expect(normalizeName('  Alemán ')).toBe('aleman');
    expect(filterOptions(options, 'INGLES', [])).toEqual(['Inglés']);
    expect(filterOptions(options, 'és', [])).toEqual(['Inglés', 'Francés', 'Portugués']);
  });

  it('excludes values already held, whatever their case or accents', () => {
    expect(filterOptions(options, '', [{ value: 'ingles' }, { value: 'Alemán' }])).toEqual([
      'Francés',
      'Italiano',
      'Portugués',
    ]);
  });

  it('keeps the catalog order rather than sorting', () => {
    expect(filterOptions(['Zeta', 'Alfa', 'Beta'], 'a', [])).toEqual(['Zeta', 'Alfa', 'Beta']);
  });

  it('offers every value not held for an empty or blank query', () => {
    expect(filterOptions(options, '', [])).toEqual(options);
    expect(filterOptions(options, '   ', [{ value: 'Inglés' }])).toEqual(options.slice(1));
  });

  it('offers nothing when nothing matches', () => {
    expect(filterOptions(options, 'xyz', [])).toEqual([]);
  });

  it('derives every test identifier from the prefix', () => {
    expect(pickerTestIds('search-skill')).toEqual({
      root: 'search-skill-picker',
      add: 'search-skill-add',
      input: 'search-skill-input',
      chip: 'search-skill-chip',
      remove: 'search-skill-remove',
      editor: 'search-skill-editor',
      mode: 'search-skill-mode',
      retry: 'search-skill-retry',
    });
  });
});
