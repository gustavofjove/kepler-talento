import { request, type FullConfig } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { AUDITOR } from './support/auth';
import { ensureSearchCandidate } from './support/seed-candidate';

export const AUTH_DIR = path.join(__dirname, '.auth');

export function authFile(role: string): string {
  return path.join(AUTH_DIR, `${role}.json`);
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  // Vite dev server, never nginx on 4200 (see playwright.config.ts).
  const baseURL = config.projects[0]?.use?.baseURL ?? 'http://127.0.0.1:4300';
  mkdirSync(AUTH_DIR, { recursive: true });

  // Role filenames are metadata consumed by the custom page fixture. Credentials are never
  // serialized: each page signs in through the real development token endpoint.
  for (const role of ['rrhh_admin', 'readonly', 'system_admin'])
    writeFileSync(authFile(role), '{"cookies":[],"origins":[]}');

  // Seed the shared search candidate once, before any worker starts. The specs that need
  // it still call the same helper in `beforeEach` — that keeps each spec honest about what
  // it depends on — but by then the candidate exists, so no two workers can race to create
  // it and leave the suite with two Laura Garcias to disambiguate.
  const bootstrap = await request.newContext({ baseURL });
  const tokenResponse = await bootstrap.post('/api/dev/token', {
    data: {
      subject: 'dev-admin-oid',
      displayName: 'Administrador local',
      email: 'admin@kepler-talento.local',
    },
  });
  const { accessToken } = (await tokenResponse.json()) as { accessToken: string };
  await bootstrap.dispose();
  const api = await request.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` },
  });
  try {
    await ensureSearchCandidate(api);
    // KTL-19: the auditor must exist as system_admin before its first sign-in links to it.
    // A 409 means an earlier run created it already.
    const created = await api.post('/api/admin/users', {
      data: { displayName: AUDITOR.displayName, email: AUDITOR.email, roleName: 'system_admin' },
    });
    if (!created.ok() && created.status() !== 409) {
      throw new Error(`Could not ensure the e2e auditor: ${created.status()}`);
    }
  } finally {
    await api.dispose();
  }
}
