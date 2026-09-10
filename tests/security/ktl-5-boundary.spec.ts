import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const read = (relative: string) => readFileSync(join(root, relative), 'utf8');

/** Every TypeScript source file under a directory, recursively. */
const sourceFiles = (directory: string): string[] =>
  readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) {
      return sourceFiles(path);
    }
    return /\.tsx?$/.test(entry) ? [path] : [];
  });
const serviceBlock = (compose: string, name: string) => {
  const match = compose.match(
    new RegExp(`^  ${name}:\\r?\\n([\\s\\S]*?)(?=^  [a-zA-Z0-9_-]+:\\r?\\n|^networks:)`, 'm'),
  );
  return match?.[0] ?? '';
};

describe('KTL-5 fail-closed security boundary', () => {
  it('keeps production business routes behind the future actor adapter', () => {
    const program = read('backend/Web/Program.cs');
    const endpoints = read('backend/Web/Features/Candidates/CandidateEndpoints.cs');
    expect(program).toContain('DevelopmentActor cannot be enabled in Production');
    expect(program).toContain(
      'app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")',
    );
    // Each of the four capabilities is enforced at the route, before dispatch.
    for (const permission of [
      'Permissions.CandidatesRead',
      'Permissions.CandidatesCreate',
      'Permissions.CandidatesUpdate',
      'Permissions.CandidatesDelete',
    ]) {
      expect(endpoints).toContain(permission);
    }
  });

  // KTL-8 retired the KTL-5 template slice: its purpose, proving the read path, is served
  // by the real read slice, and a second, less-guarded route to candidate personal data
  // must not survive.
  it('leaves no reference candidate slice behind', () => {
    for (const path of [
      'backend/Web/Features/Candidates/ReferenceCandidateEndpoints.cs',
      'backend/Application/Features/Candidates/GetReferenceCandidate.cs',
      'backend/Application/Abstractions/Persistence/ICandidateReader.cs',
      'backend/Infrastructure/Persistence/CandidateReader.cs',
      'src/app/features/reference/reference-candidate.service.ts',
    ]) {
      expect(existsSync(join(root, path))).toBe(false);
    }
    expect(read('backend/Web/Program.cs')).not.toContain('MapReferenceCandidateEndpoints');
  });

  // The candidate table used to sit in every browser's localStorage in clear. Nothing may
  // read or write that key again; the only permitted mention is the eviction that removes
  // it from browsers that still hold it.
  it('never touches the superseded candidate storage key outside the eviction', () => {
    const offenders = sourceFiles(join(root, 'src')).filter(
      (file) =>
        readFileSync(file, 'utf8').includes('rrhh-candidates') &&
        !file.endsWith('evict-legacy-storage.ts'),
    );
    expect(offenders).toEqual([]);
  });

  it('ships least-privilege runtime grants with the migration', () => {
    const migrationDirectory = join(root, 'backend/Infrastructure/Persistence/Migrations');
    const migrationName = readdirSync(migrationDirectory).find((name) =>
      name.endsWith('_InitialInfrastructure.cs'),
    );
    expect(migrationName).toBeTruthy();
    const migration = read(`backend/Infrastructure/Persistence/Migrations/${migrationName}`);
    expect(migration).toContain('REVOKE CREATE ON SCHEMA public FROM ktl_runtime');
    expect(migration).toContain('GRANT SELECT, INSERT, UPDATE, DELETE');
    expect(migration).not.toContain('BYPASSRLS');
  });

  it('keeps unsafe and unscanned documents unavailable without path disclosure', () => {
    const download = read('backend/Infrastructure/Documents/DocumentDownloadService.cs');
    const storage = read('backend/Infrastructure/Documents/FileSystemDocumentStorage.cs');
    const errors = read('backend/Web/Errors/GlobalExceptionHandler.cs');
    expect(download).toContain('DocumentScanState.Clean');
    expect(storage).toContain('ResolveContained');
    expect(errors).not.toMatch(/StackTrace|ConnectionString|StorageKey/);
  });

  it('authorizes document upload and download separately and exposes no storage field', () => {
    const endpoints = read('backend/Web/Features/Documents/DocumentEndpoints.cs');
    const contract = read('backend/Application/Features/Documents/DocumentContract.cs');
    expect(endpoints).toContain('Permissions.DocumentsUpload');
    expect(endpoints).toContain('Permissions.DocumentsDownload');
    expect(endpoints).toContain('return Results.NotFound()');
    expect(contract).not.toMatch(/public sealed record CandidateDocumentResponse[\s\S]*StorageKey/);
    expect(contract).not.toMatch(
      /public sealed record CandidateDocumentResponse[\s\S]*ScannerSignature/,
    );
  });

  it('does not expose PostgreSQL, ClamAV, or document storage through Nginx', () => {
    const compose = read('docker-compose.yml');
    const nginx = read('nginx.conf');
    expect(compose).toContain('internal: true');
    expect(serviceBlock(compose, 'clamav')).not.toContain('\n    ports:');
    expect(serviceBlock(compose, 'postgres')).not.toContain('\n    ports:');
    expect(serviceBlock(compose, 'nginx')).not.toContain('documents:/var/lib/kepler-talento');
    expect(nginx).not.toMatch(/location\s+[^\n]*(documents|quarantine|available)/i);
  });

  it('keeps structured logs and the generated contract free of deferred auth and sensitive payloads', () => {
    const program = read('backend/Web/Program.cs');
    const worker = read('backend/Infrastructure/Operations/DurableOperationWorker.cs');
    const scanner = read('backend/Infrastructure/Documents/ScanOperationHandler.cs');
    const loggingSource = `${program}\n${worker}\n${scanner}`;
    expect(program).toContain('settings.EnableJWTBearerAuth = false');
    expect(program).toContain('CorrelationMiddleware.HeaderName');
    expect(program).toContain('ProxyTrust:KnownNetworks');
    expect(loggingSource).not.toMatch(
      /Log(?:Information|Warning|Error|Debug)[^;]*(Request\.Body|OriginalFileName|StorageKey|ConnectionString|Password|Document\.Content)/s,
    );
  });
});
