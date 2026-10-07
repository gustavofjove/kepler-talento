import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string): string => readFileSync(join(repoRoot, relative), 'utf8');
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/\/? .*$/gm, '');

const dashboardSources = (): string =>
  readdirSync(join(repoRoot, 'frontend/src/app/features/dashboard'), { recursive: true })
    .filter((name) => /\.tsx?$/.test(String(name)))
    .map((name) => code(join('frontend/src/app/features/dashboard', String(name))))
    .join('\n');

/**
 * KTL-40 «Inicio»: per-stage counts name no one, the page asks only for what the actor may see
 * through existing endpoints, and the demo dataset can only reach a local development stack.
 * The behaviour behind these checks is exercised in tests/unit/dashboard-page.spec.tsx and the
 * backend PositionApiTests.
 */
describe('KTL-40 home page security boundary', () => {
  it('carries only integers in the stage counts, never a candidate identity', () => {
    const contract = code('backend/Application/Features/Positions/PositionContract.cs');
    const record = /record PositionStageCountsResponse\(([^)]*)\)/.exec(contract)?.[1] ?? '';
    expect(record.split(',').map((field) => field.trim().split(' ')[0])).toEqual([
      'int',
      'int',
      'int',
      'int',
      'int',
    ]);
    const summary = code('backend/Application/Abstractions/Persistence/IPositionRepository.cs');
    expect(/record PositionStageCounts\(([^)]*)\)/.exec(summary)?.[1]).not.toMatch(/Guid|string/);
  });

  it('counts links without reading candidates and keeps the read guard first', () => {
    const repository = code('backend/Infrastructure/Persistence/PositionRepository.cs');
    const list = repository.slice(
      repository.indexOf('public async Task<PositionPage> ListAsync'),
      repository.indexOf('public Task<Position?> FindAsync'),
    );
    expect(list).toContain('dbContext.PositionCandidates');
    expect(list).not.toContain('dbContext.Candidates');
    const handler = code('backend/Application/Features/Positions/ListPositions.cs');
    expect(handler.indexOf('PositionGuards.RequireRead(actor);')).toBeGreaterThan(-1);
    expect(handler.indexOf('PositionGuards.RequireRead(actor);')).toBeLessThan(
      handler.indexOf('positions.ListAsync'),
    );
  });

  it('adds only an index: no grant, table or permission change', () => {
    const name = readdirSync(join(repoRoot, 'backend/Infrastructure/Persistence/Migrations')).find(
      (item) => item.endsWith('_CandidateCreatedAtSortIndex.cs'),
    );
    expect(name).toBeTruthy();
    const migration = code(`backend/Infrastructure/Persistence/Migrations/${name}`);
    expect(migration).toContain('CreateIndex(');
    expect(migration).not.toMatch(/GRANT|REVOKE|CreateTable|AddColumn|Sql\(/i);
  });

  it('sends each panel request only behind the permission that governs it', () => {
    const page = code('frontend/src/app/features/dashboard/dashboard-page.tsx');
    for (const loader of ['recentAvailable', 'recentAdded', 'unavailable', 'withoutCv']) {
      expect(page).toContain(`usePanelData(loaders.${loader}, canReadCandidates)`);
    }
    expect(page).toContain('usePanelData(loaders.everyone, canReadCandidates && canSeeRemoved)');
    expect(page).toContain('usePanelData(loaders.positions, canReadPositions)');
    expect(page).toMatch(
      /if \(!canReadCandidates\) return;\s*\n?\s*.*searchPresetsService\.load\(\)/,
    );
    const hook = code('frontend/src/app/features/dashboard/use-panel-data.ts');
    expect(hook).toMatch(/if \(!enabled\) return;/);
  });

  it('reaches data only through the existing API services and shows no contact data', () => {
    const source = dashboardSources();
    expect(source).not.toMatch(/localStorage|sessionStorage|supabase|fetch\(|\/api\//i);
    expect(source).not.toMatch(/\.email\b|\.phone\b/);
    expect(source).not.toMatch(/console\.(log|info|warn|error)/);
  });

  it('puts only a preset id in the search link, never its filters', () => {
    const logic = code('frontend/src/app/features/search/pages/advanced-search.logic.ts');
    expect(logic).toContain('new URLSearchParams({ [PRESET_PARAM]: presetId })');
    const panel = code('frontend/src/app/features/dashboard/components/saved-searches-panel.tsx');
    expect(panel).toContain('presetSearchHref(preset.id)');
    expect(panel).not.toMatch(/preset\.filters/);
  });

  it('keeps the demo dataset local, API-only and out of every deployment path', () => {
    const script = code('scripts/seed-demo-data.js');
    expect(script.indexOf('lib.assertLoopback(')).toBeGreaterThan(-1);
    expect(script.indexOf('lib.assertLoopback(')).toBeLessThan(script.indexOf('await signIn()'));
    expect(script).toContain("'/api/dev/token'");
    expect(script).not.toMatch(/psql|ConnectionStrings|POSTGRES_|child_process|field-keys/);
    for (const file of [
      'docker-compose.yml',
      'backend/Dockerfile',
      'backend/docker-entrypoint.sh',
      'frontend/Dockerfile',
    ]) {
      let content = '';
      try {
        content = read(file);
      } catch {
        continue;
      }
      expect(content).not.toContain('seed-demo-data');
    }
  });
});
