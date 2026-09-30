import { candidateLocation } from '../../src/app/features/candidates/candidate-location';

describe('candidateLocation (KTL-34)', () => {
  it('shows the province in parentheses after the location', () => {
    expect(candidateLocation({ location: 'Alcobendas', province: 'Madrid' })).toBe(
      'Alcobendas (Madrid)',
    );
  });

  it('shows a lone part without parentheses', () => {
    expect(candidateLocation({ location: 'Alcobendas', province: '' })).toBe('Alcobendas');
    expect(candidateLocation({ location: '', province: 'Madrid' })).toBe('Madrid');
  });

  it('shows nothing when both parts are empty or blank', () => {
    expect(candidateLocation({ location: ' ', province: undefined })).toBe('');
  });
});
