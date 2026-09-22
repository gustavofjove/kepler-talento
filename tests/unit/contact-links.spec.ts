import { describe, expect, it } from 'vitest';
import { mailtoHref, telHref } from '../../src/app/features/candidates/contact-links';

describe('contact links', () => {
  it('builds a mailto href from the email as typed', () => {
    expect(mailtoHref('ana@example.com')).toBe('mailto:ana@example.com');
  });

  it('strips separators from the phone and keeps a leading plus', () => {
    expect(telHref('+34 (600) 12-34-56')).toBe('tel:+34600123456');
  });
});
