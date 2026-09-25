import { describe, expect, it } from 'vitest';
import { mailtoHref } from '../../src/app/features/candidates/contact-links';

describe('contact links', () => {
  it('builds a mailto href from the email as typed', () => {
    expect(mailtoHref('ana@example.com')).toBe('mailto:ana@example.com');
  });
});
