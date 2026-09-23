import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot } from '../repo-root';

const read = (relative: string): string => readFileSync(join(repoRoot, relative), 'utf8');

describe('KTL-16 administration security boundary', () => {
  it('requires authentication and explicit administration permissions at the route boundary', () => {
    const program = read('backend/Web/Program.cs');
    const endpoints = read('backend/Web/Features/Admin/AdminEndpoints.cs');

    expect(program).toContain('FallbackPolicy');
    expect(program).toContain('RequireAuthenticatedUser()');
    expect(endpoints).toContain('Permissions.UsersManage');
    expect(endpoints).toContain('Permissions.RolesManage');
    expect(endpoints).not.toMatch(/MapDelete|\.MapDelete/);
  });

  it('repeats permission guards before handlers inspect or mutate administration data', () => {
    const users = read('backend/Application/Features/Admin/Users/ManageUsers.cs');
    const roles = read('backend/Application/Features/Admin/Roles/ManageRoles.cs');

    expect(users.match(/AdminGuards\.RequireManageUsers\(actor\)/g)?.length).toBeGreaterThanOrEqual(
      6,
    );
    expect(roles.match(/AdminGuards\.RequireManageRoles\(actor\)/g)?.length).toBeGreaterThanOrEqual(
      6,
    );
  });

  it('keeps the anonymous identity endpoint bounded to non-production environments', () => {
    const program = read('backend/Web/Program.cs');
    const issuer = read('backend/Web/Features/Identity/DevelopmentTokenEndpoints.cs');
    expect(issuer).toContain('.AllowAnonymous()');
    expect(program).toContain('IsDevelopment()');
    expect(program).toContain('IsEnvironment("Testing")');
    expect(program).toContain('MapDevelopmentTokenEndpoints');
  });
});
