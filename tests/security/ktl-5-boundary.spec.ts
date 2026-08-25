import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const read = (relative: string) => readFileSync(join(root, relative), 'utf8');
const serviceBlock = (compose: string, name: string) => {
  const match = compose.match(
    new RegExp(`^  ${name}:\\r?\\n([\\s\\S]*?)(?=^  [a-zA-Z0-9_-]+:\\r?\\n|^networks:)`, 'm'),
  );
  return match?.[0] ?? '';
};

describe('KTL-5 fail-closed security boundary', () => {
  it('keeps production business/reference routes behind the future actor adapter', () => {
    const program = read('backend/Web/Program.cs');
    const endpoints = read('backend/Web/Features/Candidates/ReferenceCandidateEndpoints.cs');
    expect(program).toContain('DevelopmentActor cannot be enabled in Production');
    expect(program).toContain(
      'app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")',
    );
    expect(endpoints).toContain('actor.HasPermission(Permissions.CandidatesRead)');
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
