import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (path: string) => readFileSync(join(repoRoot, path), 'utf8');

/** Runtime 401/403 cases for valid/malformed requests and existing/missing ids live in CandidateApiTests. */
describe('KTL-36 availability boundary', () => {
  it('authorizes the availability route before validation or dispatch', () => {
    const routes = read('backend/Web/Features/Candidates/CandidateEndpoints.cs');
    const route = routes.slice(routes.indexOf('MapPut("/{id:guid}/availability"'));
    const guard = route.indexOf('Require(actor, Permissions.CandidatesUpdate)');
    const dispatch = route.indexOf('sender.Send(');
    expect(guard).toBeGreaterThan(-1);
    expect(dispatch).toBeGreaterThan(guard);
  });

  it('repeats the permission guard before lookup in the handler', () => {
    const handler = read('backend/Application/Features/Candidates/RecordCandidateAvailability.cs');
    const body = handler.slice(handler.indexOf('public async Task<CandidateResponse> Handle'));
    const guard = body.indexOf('CandidateGuards.RequireUpdate(actor)');
    expect(guard).toBeGreaterThan(-1);
    expect(body.indexOf('candidates.FindCoreAsync', guard)).toBeGreaterThan(guard);
  });

  it('returns only the checker display name and keeps database grants unchanged', () => {
    const contract = read('backend/Application/Features/Candidates/CandidateContract.cs');
    const response = contract.slice(contract.indexOf('record CandidateAvailabilityResponse('));
    expect(response.slice(0, response.indexOf(');'))).toContain('CheckedByDisplayName');
    expect(response.slice(0, response.indexOf(');'))).not.toContain('UserId');

    const migration = read(
      'backend/Infrastructure/Persistence/Migrations/20261001093011_ReplaceCandidateStatusWithAvailability.cs',
    );
    expect(migration).not.toMatch(/GRANT|REVOKE/i);
    const grants = read(
      'backend/Infrastructure/Persistence/Migrations/20260910113327_RevokeCandidateDelete.cs',
    );
    expect(grants).toContain('REVOKE DELETE, TRUNCATE ON "CND_Candidates" FROM ktl_runtime');
  });
});
