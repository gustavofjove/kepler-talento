import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const read = (relative: string): string => readFileSync(join(process.cwd(), relative), 'utf8');
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/\/? .*$/gm, '');

describe('KTL-15 position security boundary', () => {
  it('has independent shared permissions', () => {
    const backend = read('backend/Application/Abstractions/Identity/ICurrentActor.cs');
    const frontend = read('src/app/shared/models/auth.models.ts');
    for (const permission of ['positions.read', 'positions.manage']) {
      expect(backend).toContain(`"${permission}"`);
      expect(frontend).toContain(`'${permission}'`);
    }
  });

  it('offers four guarded routes and no delete route', () => {
    const endpoints = code('backend/Web/Features/Positions/PositionEndpoints.cs');
    expect(endpoints.match(/MapGet\(/g)).toHaveLength(2);
    expect(endpoints.match(/MapPost\(/g)).toHaveLength(1);
    expect(endpoints.match(/MapPut\(/g)).toHaveLength(1);
    expect(endpoints).not.toMatch(/MapDelete|MapPatch/);
    expect(endpoints).toContain('Permissions.PositionsRead');
    expect(endpoints).toContain('Permissions.PositionsManage');
  });

  it('checks handler permissions before validation and lookup', () => {
    for (const [file, guard] of [
      ['ListPositions.cs', 'PositionGuards.RequireRead(actor);'],
      ['GetPosition.cs', 'PositionGuards.RequireRead(actor);'],
      ['ManagePositions.cs', 'PositionGuards.RequireManage(actor);'],
    ])
      expect(code(`backend/Application/Features/Positions/${file}`).indexOf(guard)).toBeGreaterThan(
        -1,
      );
  });

  it('uses API-only position persistence and candidate permission before live search', () => {
    const files = readdirSync(join(process.cwd(), 'src/app/features/positions'), {
      recursive: true,
    }).filter((name) => String(name).endsWith('.ts') || String(name).endsWith('.tsx'));
    const source = files
      .map((name) => read(join('src/app/features/positions', String(name))))
      .join('\n');
    expect(source).not.toMatch(/localStorage|sessionStorage|supabase/i);
    const detail = read('src/app/features/positions/position-detail-page.tsx');
    expect(detail).toContain("usePermission('candidates.read')");
    expect(detail).toContain('if (!position || !canReadCandidates)');
  });

  it('grants bounded DML and explicitly revokes deletion', () => {
    const name = readdirSync(
      join(process.cwd(), 'backend/Infrastructure/Persistence/Migrations'),
    ).find((item) => item.endsWith('_AddPositionManagement.cs'));
    expect(name).toBeTruthy();
    const migration = read(`backend/Infrastructure/Persistence/Migrations/${name}`);
    expect(migration).toContain('GRANT SELECT, INSERT, UPDATE ON "OPS_Positions" TO ktl_runtime');
    expect(migration).toContain('REVOKE DELETE, TRUNCATE ON "OPS_Positions" FROM ktl_runtime');
  });
});
