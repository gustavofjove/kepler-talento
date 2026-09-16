import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const read = (relative: string): string => readFileSync(join(process.cwd(), relative), 'utf8');

/** Source without comments, so an explanatory remark naming a forbidden construct is not a hit. */
const code = (relative: string): string =>
  read(relative)
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^\s*\/\/.*$/gm, '');

/**
 * Static evidence for the KTL-18 list surface. The runtime refusals (unauthenticated,
 * unauthorized, `includeInactive` without `candidates.delete`, sent with malformed bodies to
 * prove the refusal precedes validation) are proven against PostgreSQL in
 * `backend/Tests/IntegrationTests/SearchApiTests.cs` and `SearchHandlerTests.cs`; this spec
 * pins the shape that makes them hold.
 */
describe('KTL-18 server-side candidate list security boundary', () => {
  it('authorizes read, then the removal permission for includeInactive, before dispatch', () => {
    const endpoints = code('backend/Web/Features/Search/SearchEndpoints.cs');
    const route = endpoints.slice(
      endpoints.indexOf('MapPost("/api/candidates/search"'),
      endpoints.indexOf('.WithName("SearchCandidates")'),
    );

    const readGuard = route.indexOf('Require(actor, Permissions.CandidatesRead)');
    const removal = route.indexOf('Require(actor, Permissions.CandidatesDelete)');
    const dispatch = route.indexOf('sender.Send(');
    expect(readGuard).toBeGreaterThan(-1);
    expect(removal).toBeGreaterThan(readGuard);
    expect(dispatch).toBeGreaterThan(removal);
  });

  it('repeats both guards in the handler before any validation or query', () => {
    const handler = code('backend/Application/Features/Search/SearchCandidates.cs');
    const body = handler.slice(handler.indexOf('public async Task<'));

    const readGuard = body.indexOf('SearchGuards.RequireRead(actor);');
    const removal = body.indexOf('SearchGuards.RequireIncludeRemoved(actor);');
    expect(readGuard).toBeGreaterThan(-1);
    expect(removal).toBeGreaterThan(readGuard);
    for (const later of [
      'TryNormalize',
      'SearchPaging.Validate',
      'SearchSort.Parse',
      'candidates.',
    ]) {
      expect(body.indexOf(later), later).toBeGreaterThan(removal);
    }
    expect(read('backend/Application/Features/Search/SearchContract.cs')).toContain(
      'actor.HasPermission(Permissions.CandidatesDelete)',
    );
  });

  it('turns the sort field into an enum from a closed set and never into query text', () => {
    const contract = code('backend/Application/Features/Search/SearchContract.cs');
    const parse = contract.slice(contract.indexOf('public static SearchSort Parse'));
    expect(parse).toContain('"updatedAt" => SearchSortField.UpdatedAt');
    expect(parse).toContain('"lastName" => SearchSortField.LastName');
    expect(parse).toContain('"status" => SearchSortField.Status');
    expect(parse).toContain('SearchErrors.SortFieldInvalid');

    const query = code('backend/Infrastructure/Persistence/CandidateSearchQuery.cs');
    // No dynamic member access or raw SQL through which caller text could become an identifier.
    expect(query).not.toMatch(/EF\.Property|FromSql|ExecuteSql|SqlQuery|OrderBy\(\s*"/);
    const page = query.slice(query.indexOf('public IQueryable<CandidateSearchItem> Page'));
    expect(page).toContain('.ThenBy(candidate => candidate.Id)');
  });

  it('keeps the list projection minimal', () => {
    const contract = code('backend/Application/Features/Search/SearchContract.cs');
    const item = contract.slice(
      contract.indexOf('record CandidateSearchItem('),
      contract.indexOf(');', contract.indexOf('record CandidateSearchItem(')),
    );
    expect(item).not.toMatch(
      /Notes|Consent|Review|Received|Location|Province|Country|Source|StorageKey/,
    );
  });

  it('ships the sort indexes without touching runtime grants', () => {
    const migrations = readdirSync(
      join(process.cwd(), 'backend/Infrastructure/Persistence/Migrations'),
    )
      .filter((name) => name.endsWith('_Ktl18CandidateSortIndexes.cs'))
      .map((name) => read(`backend/Infrastructure/Persistence/Migrations/${name}`));
    expect(migrations).toHaveLength(1);
    expect(migrations[0]).not.toMatch(/GRANT|REVOKE|DropTable|DeleteData/);
  });

  it('holds no whole-table candidate list in the browser', () => {
    const service = code('src/app/features/candidates/services/candidate.service.ts');
    const api = code('src/app/features/candidates/services/candidate.api.ts');
    const page = code('src/app/features/candidates/pages/candidate-list-page.tsx');
    const logic = code('src/app/features/candidates/pages/candidate-list.logic.ts');

    expect(service).not.toMatch(/summaries|ensureAllAggregates|\blist\(/);
    expect(service).not.toMatch(/localStorage|sessionStorage|indexedDB/);
    expect(api).not.toContain('/candidates?includeInactive');
    expect(page).not.toMatch(/\.sort\(|\.slice\(|filterCandidates|sortCandidates|paginate/);
    expect(logic).not.toMatch(/filterCandidates|sortCandidates|paginate|totalPages/);
  });

  it('keeps the free-text term out of the URL and hides include-inactive without permission', () => {
    const logic = code('src/app/features/candidates/pages/candidate-list.logic.ts');
    const params = logic.slice(
      logic.indexOf('export const LIST_PARAMS'),
      logic.indexOf('} as const;', logic.indexOf('export const LIST_PARAMS')),
    );
    expect(params).not.toMatch(/text|\bq\b/);

    const bar = code('src/app/features/candidates/components/candidate-filters-bar.tsx');
    expect(bar).toMatch(/canIncludeInactive \? \(\s*<label/);
    expect(code('src/app/features/candidates/pages/candidate-list-page.tsx')).toContain(
      "usePermission('candidates.delete')",
    );
  });
});
