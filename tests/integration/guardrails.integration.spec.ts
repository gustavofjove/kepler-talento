import { readFileSync } from 'node:fs';
import * as path from 'node:path';
import {
  canDeactivateAdmin,
  isCatalogValueInUse,
} from '../../supabase/functions/_shared/guardrails';

describe('Guardrails integration', () => {
  it('blocks deactivating self or last active admin', () => {
    const onlyAdmin = [
      { id: 'a1', role: 'rrhh_admin', isActive: true },
      { id: 'u2', role: 'readonly', isActive: true },
    ];

    expect(canDeactivateAdmin('a1', 'a1', onlyAdmin)).toBe(false);
    expect(canDeactivateAdmin('a1', 'u2', onlyAdmin)).toBe(false);

    const twoAdmins = [
      { id: 'a1', role: 'rrhh_admin', isActive: true },
      { id: 'a2', role: 'system_admin', isActive: true },
    ];
    expect(canDeactivateAdmin('a1', 'a2', twoAdmins)).toBe(true);
  });

  it('detects catalog values in use using case-insensitive comparison', () => {
    expect(isCatalogValueInUse('Inglés', ['francés', 'INGLÉS'])).toBe(true);
    expect(isCatalogValueInUse('Alemán', ['francés', 'inglés'])).toBe(false);
  });

  it('contains SQL guardrail triggers in latest migration', () => {
    const migrationPath = path.join(
      process.cwd(),
      'supabase/migrations/009_admin_catalog_guardrails.sql',
    );
    const sql = readFileSync(migrationPath, 'utf-8');

    expect(sql).toMatch(/prevent_last_active_admin_mutation/i);
    expect(sql).toMatch(/trg_profiles_last_admin_guardrail/i);
    expect(sql).toMatch(/prevent_catalog_value_deactivation_when_in_use/i);
  });
});
