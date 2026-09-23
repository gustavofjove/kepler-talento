import { readFileSync } from 'node:fs';

describe('KTL-20 preview CSP boundary', () => {
  it('allows blob documents only in frame and object directives', () => {
    const nginx = readFileSync('nginx.conf', 'utf8');
    const policy = nginx.match(/add_header Content-Security-Policy "([^"]+)" always;/)?.[1];
    expect(policy).toBe(
      "default-src 'self'; img-src 'self' data: blob:; font-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-src blob:; object-src blob:; frame-ancestors 'none'",
    );
    expect(policy?.match(/blob:/g)).toHaveLength(3);
    expect(policy).not.toContain('script-src blob:');
    expect(policy).not.toContain('default-src blob:');
  });
});
