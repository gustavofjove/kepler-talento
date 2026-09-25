import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string): string => readFileSync(join(repoRoot, relative), 'utf8');
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/\/? .*$/gm, '');

/**
 * Static evidence for the KTL-30 boundary. The HTTP matrix (unauthenticated, each missing
 * permission, existing versus missing ids) runs against a real host and database in
 * `backend/Tests/IntegrationTests/PositionCandidateApiTests.cs`.
 */
describe('KTL-30 position candidate security boundary', () => {
  it('guards every link route with a policy that requires both permissions', () => {
    const endpoints = code('backend/Web/Features/Positions/PositionCandidateEndpoints.cs');
    const routes = endpoints.match(/\.Map(Get|Post|Put|Delete|Patch)\(/g) ?? [];
    expect(routes).toHaveLength(5);
    expect(endpoints.match(/\.MapDelete\(/g)).toHaveLength(1);
    expect(endpoints).not.toMatch(/MapPatch/);
    expect(endpoints.match(/RequireAuthorization\((ReadPolicy|ManagePolicy)\)/g)).toHaveLength(5);

    const program = code('backend/Web/Program.cs');
    for (const [policy, permission] of [
      ['PositionCandidateEndpoints.ReadPolicy', 'Permissions.PositionsRead'],
      ['PositionCandidateEndpoints.ManagePolicy', 'Permissions.PositionsManage'],
    ]) {
      const start = program.indexOf(`options.AddPolicy(${policy}`);
      expect(start).toBeGreaterThan(-1);
      const body = program.slice(start, program.indexOf('));', start));
      expect(body).toContain('actor.IsAuthenticated');
      expect(body).toContain(`actor.HasPermission(${permission})`);
      expect(body).toContain('actor.HasPermission(Permissions.CandidatesRead)');
    }
  });

  it('keeps positions themselves without a delete route', () => {
    expect(code('backend/Web/Features/Positions/PositionEndpoints.cs')).not.toMatch(/MapDelete/);
  });

  it('repeats the combined guard first in every handler', () => {
    for (const [file, guard] of [
      ['ListPositionCandidates.cs', 'PositionGuards.RequireReadCandidates(actor);'],
      ['ListCandidatePositions.cs', 'PositionGuards.RequireReadCandidates(actor);'],
      ['AddPositionCandidate.cs', 'PositionGuards.RequireManageCandidates(actor);'],
      ['ChangePositionCandidateStage.cs', 'PositionGuards.RequireManageCandidates(actor);'],
      ['RemovePositionCandidate.cs', 'PositionGuards.RequireManageCandidates(actor);'],
    ]) {
      const source = code(`backend/Application/Features/Positions/${file}`);
      const handle = source.indexOf('Handle(');
      const guardAt = source.indexOf(guard);
      expect(guardAt).toBeGreaterThan(handle);
      // Nothing touches the repository or validates before the guard.
      expect(source.slice(handle, guardAt)).not.toMatch(/positions\.|throw /);
    }
    const contract = code('backend/Application/Features/Positions/PositionContract.cs');
    expect(contract).toContain('Permissions.PositionsRead, Permissions.CandidatesRead');
    expect(contract).toContain('Permissions.PositionsManage, Permissions.CandidatesRead');
  });

  it('scopes DELETE to the link table and keeps it revoked elsewhere', () => {
    const migrations = 'backend/Infrastructure/Persistence/Migrations';
    const name = readdirSync(join(repoRoot, migrations)).find((item) =>
      item.endsWith('_AddPositionCandidates.cs'),
    );
    expect(name).toBeTruthy();
    const migration = read(`${migrations}/${name}`);
    expect(migration).toContain(
      'GRANT SELECT, INSERT, UPDATE, DELETE ON "OPS_PositionCandidates" TO ktl_runtime',
    );
    expect(migration).toContain('REVOKE TRUNCATE ON "OPS_PositionCandidates" FROM ktl_runtime');
    expect(migration).not.toMatch(/GRANT[^;]*DELETE[^;]*"(OPS_Positions|CND_Candidates)"/);
    expect(migration).toContain('onDelete: ReferentialAction.Restrict');
    expect(migration).not.toContain('ReferentialAction.Cascade');
  });

  it('exposes only search-level candidate fields and records no stage in audit events', () => {
    const contract = code('backend/Application/Features/Positions/PositionContract.cs');
    const fields = (record: string): string => {
      const start = contract.indexOf(`record ${record}(`);
      return contract.slice(start, contract.indexOf(');', start));
    };
    // The same contact columns candidate search shows, behind the same candidates.read.
    expect(fields('PositionCandidateResponse')).not.toMatch(
      /Notes|Documents?|DocumentId|Consent|Review|Location|Source|Description|Requirements/,
    );
    expect(fields('CandidatePositionResponse')).not.toMatch(
      /Email|Phone|Notes|Description|Requirements/,
    );
    const repository = code('backend/Infrastructure/Persistence/PositionRepository.cs');
    expect(repository).toContain(
      'PositionCandidate.AuditSubject(link.PositionId, link.CandidateId)',
    );
    const domain = code('backend/Domain/Positions/PositionCandidate.cs');
    expect(domain).toMatch(
      /AuditSubject\(Guid positionId, Guid candidateId\) =>\s*\$"\{positionId:N\}:\{candidateId:N\}"/,
    );
  });

  it('reaches links only through the API and only with the required permissions', () => {
    const files = [
      'frontend/src/app/features/positions/use-position-candidates.ts',
      'frontend/src/app/features/positions/components/position-candidates-panel.tsx',
      'frontend/src/app/features/positions/components/position-candidate-picker.tsx',
      'frontend/src/app/features/candidates/components/candidate-positions-panel.tsx',
      'frontend/src/app/features/candidates/components/position-picker.tsx',
    ];
    for (const file of files)
      expect(read(file)).not.toMatch(/localStorage|sessionStorage|supabase/i);

    const detail = read('frontend/src/app/features/positions/position-detail-page.tsx');
    expect(detail).toContain('usePositionCandidates(position?.id, canReadCandidates)');
    const candidatePage = read(
      'frontend/src/app/features/candidates/pages/candidate-detail-page.tsx',
    );
    expect(candidatePage).toContain("usePermission('positions.read')");
    expect(candidatePage).toMatch(/canReadPositions \? \(\s*<CandidatePositionsPanel/);
  });
});
