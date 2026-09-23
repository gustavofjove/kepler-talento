import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string): string => readFileSync(join(repoRoot, relative), 'utf8');

/** Source without comments, so an explanatory remark naming a forbidden construct is not a hit. */
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/.*$/gm, '');

const importFeature = 'backend/Application/Features/Import';
const handlers = [
  'UploadImportFile.cs',
  'ValidateImportBatch.cs',
  'CommitImportBatch.cs',
  'ListImportBatches.cs',
  'GetImportBatch.cs',
  'GetImportRowReport.cs',
];

describe('KTL-17 candidate import security boundary', () => {
  it('guards the whole import route group with the import permission, before binding', () => {
    const endpoints = read('backend/Web/Features/Import/ImportEndpoints.cs');
    const program = read('backend/Web/Program.cs');

    expect(endpoints).toContain('MapGroup("/api/import/batches")');
    expect(endpoints).toContain('.RequireAuthorization(Permissions.CandidatesImport)');
    expect(program).toContain('options.AddPolicy(Permissions.CandidatesImport');
    expect(program).toContain('FallbackPolicy');
    expect(program).toContain('app.MapImportEndpoints()');
    // No routing constraint that would answer before authorization.
    expect(code('backend/Web/Features/Import/ImportEndpoints.cs')).not.toMatch(/\.Accepts</);
    expect(endpoints).not.toMatch(/MapDelete/);
  });

  it('repeats the permission guard first in every import handler', () => {
    for (const file of handlers) {
      const source = read(`${importFeature}/${file}`);
      const handle = source.indexOf('public async Task<');
      expect(handle, file).toBeGreaterThan(-1);
      const body = source.slice(handle);
      const guard = body.indexOf('ImportGuards.RequireImport(actor);');
      expect(guard, file).toBeGreaterThan(-1);
      // Nothing touches a batch, a file or storage before the guard.
      for (const earlier of ['batches.', 'storage.', 'importStorage.', 'operations.']) {
        const use = body.indexOf(earlier);
        expect(use === -1 || use > guard, `${file}: ${earlier} before the guard`).toBe(true);
      }
    }
    const guards = read(`${importFeature}/ImportGuards.cs`);
    expect(guards).toContain('Permissions.CandidatesImport');
    expect(guards).toContain('!actor.IsAuthenticated');
  });

  it('exposes no storage key, path or row value in any import response contract', () => {
    const contract = read(`${importFeature}/ImportContract.cs`);
    const responses = contract.slice(0, contract.indexOf('public static class ImportOperations'));

    expect(responses).not.toMatch(/StorageKey\b.*[,)]\s*$/m);
    expect(responses).not.toMatch(/\bstring\s+StorageKey\b/);
    expect(responses).not.toMatch(/\bPath\b/);
    const rowOutcome = contract.slice(
      contract.indexOf('record ImportRowOutcomeResponse'),
      contract.indexOf('record ImportRowReportResponse'),
    );
    expect(rowOutcome).not.toMatch(/Value|CandidateId|FirstName|Email/);
  });

  it('keeps row outcomes free of a value column and write-once at the database', () => {
    const migrations = readdirSync(join(repoRoot, 'backend/Infrastructure/Persistence/Migrations'))
      .filter((name) => name.endsWith('_AddImportBatches.cs'))
      .map((name) => read(`backend/Infrastructure/Persistence/Migrations/${name}`));
    expect(migrations).toHaveLength(1);
    const migration = migrations[0];
    const outcomes = migration.slice(
      migration.indexOf('name: "ADM_ImportRowOutcomes"'),
      migration.indexOf('constraints:', migration.indexOf('name: "ADM_ImportRowOutcomes"')),
    );

    expect(outcomes).not.toMatch(/Value\s*=/);
    expect(migration).toContain('GRANT SELECT, INSERT ON "ADM_ImportRowOutcomes" TO ktl_runtime;');
    expect(migration).toContain(
      'REVOKE UPDATE, DELETE, TRUNCATE ON "ADM_ImportRowOutcomes" FROM ktl_runtime;',
    );
    expect(migration).toContain('REVOKE DELETE, TRUNCATE ON "ADM_ImportBatches" FROM ktl_runtime;');
    expect(migration).not.toMatch(/GRANT[^;]*DELETE[^;]*ADM_Import/);
  });

  it('never lets a production project reach the operator migration tool', () => {
    for (const project of [
      'backend/Web/Web.csproj',
      'backend/Application/Application.csproj',
      'backend/Infrastructure/Infrastructure.csproj',
    ]) {
      expect(read(project)).not.toMatch(/Tools[\\/]/);
    }
  });

  it('keeps no import data in browser storage and evicts the stub history', () => {
    const service = code('frontend/src/app/features/admin/import/import.service.ts');
    const eviction = read('frontend/src/app/core/storage/evict-legacy-storage.ts');

    expect(service).not.toMatch(/localStorage|sessionStorage|indexedDB|supabase/i);
    expect(service).not.toMatch(/parseCsv|Math\.random|MAX_IMPORT_ROWS/);
    expect(eviction).toContain("'rrhh.import.batches.v1'");
  });
});
