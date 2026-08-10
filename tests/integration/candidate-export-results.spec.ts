import { readFileSync } from 'node:fs';
import * as path from 'node:path';

describe('candidate-export-results contract', () => {
  const filePath = path.join(process.cwd(), 'supabase/functions/candidate-export-results/index.ts');
  const source = readFileSync(filePath, 'utf-8');

  it('enforces bearer auth and max row limits', () => {
    expect(source).toMatch(/requireBearerAuth\(request\)/);
    expect(source).toMatch(/max_export_rows_exceeded/);
    expect(source).toMatch(/EDGE_LIMITS\.maxExportRows/);
  });

  it('returns export metadata envelope for authorized requests', () => {
    expect(source).toMatch(/export_id/);
    expect(source).toMatch(/download_url/);
    expect(source).toMatch(/request_id/);
    expect(source).toMatch(/field_set/);
  });
});
