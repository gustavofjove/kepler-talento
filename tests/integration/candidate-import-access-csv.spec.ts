import { readFileSync } from 'node:fs';
import * as path from 'node:path';

describe('candidate-import-access-csv contract', () => {
  const filePath = path.join(
    process.cwd(),
    'supabase/functions/candidate-import-access-csv/index.ts',
  );
  const source = readFileSync(filePath, 'utf-8');

  it('requires auth and validates source_name plus max file count', () => {
    expect(source).toMatch(/requireBearerAuth\(request\)/);
    expect(source).toMatch(/source_name_required/);
    expect(source).toMatch(/max_import_files_exceeded/);
    expect(source).toMatch(/EDGE_LIMITS\.maxImportFiles/);
  });

  it('supports dry-run and commit style statuses with idempotency context', () => {
    expect(source).toMatch(/body\.dry_run \? 'validated' : 'loaded'/);
    expect(source).toMatch(/idempotency_key/);
    expect(source).toMatch(/batch_id/);
    expect(source).toMatch(/request_id/);
  });
});
