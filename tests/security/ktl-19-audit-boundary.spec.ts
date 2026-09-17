import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const read = (relative: string): string => readFileSync(join(process.cwd(), relative), 'utf8');

/** Source without comments, so an explanatory remark naming a forbidden construct is not a hit. */
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/\/?.*$/gm, '');

const migration = (): string => {
  const names = readdirSync(
    join(process.cwd(), 'backend/Infrastructure/Persistence/Migrations'),
  ).filter((name) => name.endsWith('_AddAuditActor.cs'));
  expect(names).toHaveLength(1);
  return read(`backend/Infrastructure/Persistence/Migrations/${names[0]}`);
};

/**
 * Static evidence for the KTL-19 audit surface. The runtime refusals — 401 without a token, 403
 * without `audit.read` (including for a caller holding every other permission), each sent with a
 * malformed filter to prove the refusal precedes validation — and the `ktl_runtime` grants are
 * proven against PostgreSQL in `backend/Tests/IntegrationTests/AuditApiTests.cs`. This spec pins
 * the shape that makes them hold.
 */
describe('KTL-19 audit trail security boundary', () => {
  it('guards the audit group with the audit.read policy, which runs before binding', () => {
    const endpoints = code('backend/Web/Features/Audit/AuditEndpoints.cs');
    expect(endpoints).toContain('MapGroup("/api/audit")');
    expect(endpoints).toContain('.RequireAuthorization(Permissions.AuditRead)');
    // Read-only: no verb that could change or remove an event.
    expect(endpoints).not.toMatch(/Map(Post|Put|Patch|Delete)\(/);

    const program = code('backend/Web/Program.cs');
    const policy = program.slice(program.indexOf('options.AddPolicy(Permissions.AuditRead'));
    expect(policy).toMatch(
      /actor\.IsAuthenticated\s*&& actor\.HasPermission\(Permissions\.AuditRead\)/,
    );
    expect(program).toContain('app.MapAuditEndpoints();');
  });

  it('repeats the guard first in the handler, before any filter is parsed or queried', () => {
    const handler = code('backend/Application/Features/Audit/ListAuditEvents.cs');
    const body = handler.slice(handler.indexOf('public async Task<AuditPageResponse> Handle'));
    const guard = body.indexOf('AuditGuards.RequireRead(actor);');
    expect(guard).toBeGreaterThan(-1);
    for (const later of ['TryParseDate', 'TryParseActor', 'audits.ListAsync']) {
      expect(body.indexOf(later), later).toBeGreaterThan(guard);
    }
    const guards = code('backend/Application/Features/Audit/AuditGuards.cs');
    expect(guards).toContain(
      '!actor.IsAuthenticated || !actor.HasPermission(Permissions.AuditRead)',
    );
  });

  it('answers with identifiers and codes only', () => {
    const contract = code('backend/Application/Features/Audit/AuditContract.cs');
    const record = contract.slice(
      contract.indexOf('record AuditEventResponse('),
      contract.indexOf(')', contract.indexOf('record AuditEventResponse(')),
    );
    expect(record).not.toMatch(
      /Name|Email|Subject\b|ExternalKey|FirstName|LastName|Notes|StorageKey|FileName/,
    );

    const entity = code('backend/Domain/Auditing/AuditEvent.cs');
    expect(entity).not.toMatch(/Email|DisplayName|ExternalKey|ExternalSubject/);
  });

  it('stores no external key or identity on any audit write path', () => {
    for (const file of [
      'backend/Infrastructure/Persistence/CandidateRepository.cs',
      'backend/Infrastructure/Persistence/CatalogRepository.cs',
      'backend/Infrastructure/Persistence/DocumentRepository.cs',
      'backend/Infrastructure/Import/ImportCommitHandler.cs',
      'backend/Application/Features/Documents/ManageCandidateDocuments.cs',
      'backend/Application/Features/Documents/UploadCandidateDocument.cs',
    ]) {
      expect(code(file), file).not.toContain('ExternalKey');
    }
  });

  it('makes the table append-only to the runtime role in the same migration as the actor', () => {
    const source = migration();
    const up = source.slice(
      source.indexOf('protected override void Up'),
      source.indexOf('protected override void Down'),
    );
    const down = source.slice(source.indexOf('protected override void Down'));

    expect(up).toContain('"ActorUserId"');
    expect(up).toContain('REVOKE UPDATE, DELETE, TRUNCATE ON "AUD_Events" FROM ktl_runtime');
    expect(up).not.toMatch(/GRANT [^;]*"AUD_Events"/);
    expect(down).toContain('GRANT UPDATE, DELETE ON "AUD_Events" TO ktl_runtime');
  });

  it('grants audit.read to system_admin only', () => {
    const up = migration();
    expect(up).toMatch(/WHERE "Name" = 'system_admin'/);
    expect(up).not.toMatch(/'rrhh_admin'|'rrhh_user'|'manager_reader'|'readonly'/);
  });

  it('exposes no application path that updates or removes an audit event', () => {
    const repository = code('backend/Infrastructure/Persistence/AuditRepository.cs');
    expect(repository).not.toMatch(/Remove|Update|ExecuteDelete|ExecuteUpdate|Attach/);
    const port = code('backend/Application/Abstractions/Persistence/IAuditRepository.cs');
    expect(port).not.toMatch(/Delete|Remove|Update/);
  });

  it('keeps the Auditoría screen behind its permission and out of browser storage', () => {
    const app = code('src/app/app.tsx');
    expect(app).toMatch(/permission="audit\.read"[\s\S]{0,80}path: 'admin\/audit'/);
    const service = code('src/app/features/admin/audit/audit.service.ts');
    expect(service).not.toMatch(/localStorage|sessionStorage|indexedDB/);
    const page = code('src/app/features/admin/audit/audit-page.tsx');
    expect(page).toContain("usePermission('users.manage')");
  });
});
