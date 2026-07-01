import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.describe('Secure access', () => {
  test('redirects an unauthenticated user from a protected route to /login', async ({ page }) => {
    await page.goto('/app/candidates');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('lets an rrhh_admin log in and reach the protected app shell', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#email', 'rrhh.admin@example.com');
    await page.fill('#password', 'local-demo');
    await page.selectOption('#role', 'rrhh_admin');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/app$/);
  });

  test('blocks a readonly user from the candidate creation route', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('readonly') });
    const page = await context.newPage();

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app$/);
    await context.close();
  });

  test('allows an rrhh_admin to reach the candidate creation route', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('rrhh_admin') });
    const page = await context.newPage();

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app\/candidates\/new$/);
    await context.close();
  });

  test('lets an authenticated user reach the MFA verification route and continue', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('rrhh_admin') });
    const page = await context.newPage();

    await page.goto('/mfa');
    await expect(page.locator('h1')).toHaveText('Verificacion MFA');
    await page.click('a:has-text("Continuar")');
    await expect(page).toHaveURL(/\/app$/);

    await context.close();
  });

  test('redirects an unauthenticated user away from the MFA route', async ({ page }) => {
    await page.goto('/mfa');
    await expect(page).toHaveURL(/\/login$/);
  });
});
