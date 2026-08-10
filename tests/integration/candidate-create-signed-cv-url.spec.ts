import { readFileSync } from 'node:fs';
import * as path from 'node:path';

describe('candidate-create-signed-cv-url contract', () => {
  const filePath = path.join(
    process.cwd(),
    'supabase/functions/candidate-create-signed-cv-url/index.ts',
  );
  const source = readFileSync(filePath, 'utf-8');

  it('requires bearer auth and document_id validation', () => {
    expect(source).toMatch(/requireBearerAuth\(request\)/);
    expect(source).toMatch(/document_id_required/);
    expect(source).toMatch(/method_not_allowed/);
  });

  it('returns signed-url payload with request_id and ttl', () => {
    expect(source).toMatch(/signed-url-placeholder/);
    expect(source).toMatch(/expires_in_seconds/);
    expect(source).toMatch(/request_id/);
  });
});
