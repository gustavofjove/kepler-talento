import { describe, expect, it } from 'vitest';
import { displayPhone, mailtoHref } from '../../src/app/features/candidates/contact-links';

describe('contact links', () => {
  it('builds a mailto href from the email as typed', () => {
    expect(mailtoHref('ana@example.com')).toBe('mailto:ana@example.com');
  });

  describe('displayPhone (KTL-34)', () => {
    it.each([
      ['+34 600 000 001', '600 000 001'],
      ['+34600000001', '600000001'],
      ['0034 600 000 001', '600 000 001'],
      ['(+34) 915.234.567', '915.234.567'],
      ['(34) 91 523 45 67', '91 523 45 67'],
      ['+34-600-000-001', '600-000-001'],
      ['  +34 600 000 001 ', '600 000 001'],
    ])('drops the Spanish prefix from %s', (stored, shown) => {
      expect(displayPhone(stored)).toBe(shown);
    });

    it.each([
      ['600 000 001'],
      ['+33 6 12 34 56 78'],
      ['+351 912 345 678'],
      ['+34 600'],
      ['+34 600 000 001 ext. 22'],
      ['+3460000000123'],
      [''],
    ])('shows %s unchanged', (stored) => {
      expect(displayPhone(stored)).toBe(stored);
    });
  });
});
