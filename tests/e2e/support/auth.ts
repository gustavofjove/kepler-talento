import type { Page, Response } from '@playwright/test';

type AuthRole = 'rrhh_admin' | 'readonly';

const patchedPages = new WeakSet<Page>();
const pageTokens = new WeakMap<Page, string>();

export function authorizationHeaders(page: Page): { Authorization: string } {
  const token = pageTokens.get(page);
  if (!token) throw new Error('The page has no authenticated development token.');
  return { Authorization: `Bearer ${token}` };
}

export async function signInAs(page: Page, role: AuthRole): Promise<void> {
  const originalGoto = page.goto.bind(page);
  const originalReload = page.reload.bind(page);

  await authenticate(page, role, originalGoto);

  if (patchedPages.has(page)) return;
  patchedPages.add(page);

  page.goto = async (url): Promise<Response | null> => {
    const target = new URL(url, page.url());
    if (target.origin !== new URL(page.url()).origin) return originalGoto(url);
    await navigateInApp(page, `${target.pathname}${target.search}${target.hash}`);
    return null;
  };

  page.reload = async (options): Promise<Response | null> => {
    const target = new URL(page.url());
    const response = await originalReload(options);
    await page.getByTestId('sign-in').waitFor();
    await authenticate(page, role, originalGoto);
    await navigateInApp(page, `${target.pathname}${target.search}${target.hash}`);
    return response;
  };
}

async function authenticate(page: Page, role: AuthRole, goto: Page['goto']): Promise<void> {
  await page.route('**/api/dev/token', async (route) => {
    const request = route.request();
    const identity =
      role === 'rrhh_admin'
        ? {
            subject: 'dev-admin-oid',
            displayName: 'Administrador local',
            email: 'admin@kepler-talento.local',
          }
        : {
            subject: 'e2e-readonly-oid',
            displayName: 'Usuario de solo lectura',
            email: 'e2e-readonly@kepler-talento.local',
          };
    const response = await route.fetch({
      postData: JSON.stringify(identity),
      headers: { ...request.headers(), 'content-type': 'application/json' },
    });
    const body = (await response.json()) as { accessToken: string };
    pageTokens.set(page, body.accessToken);
    await route.fulfill({ response, json: body });
  });
  await goto('/login');
  await page.getByTestId('sign-in').click();
  await page.waitForURL('**/app');
  await page.unroute('**/api/dev/token');
}

async function navigateInApp(page: Page, url: string): Promise<void> {
  await page.evaluate((target) => {
    window.history.pushState({}, '', target);
    window.dispatchEvent(new PopStateEvent('popstate'));
  }, url);
}
