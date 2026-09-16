import { expect, test } from './fixtures';
import { signInAs } from './support/auth';

test.describe('Secure access', () => {
  test('redirects an unauthenticated user from a protected route to /login', async ({ page }) => {
    await page.goto('/app/candidates');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('lets an rrhh_admin log in and reach the protected app shell', async ({ page }) => {
    await signInAs(page, 'rrhh_admin');
    await expect(page).toHaveURL(/\/app$/);
  });

  test('blocks a readonly user from the candidate creation route', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'readonly');

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app$/);
    await context.close();
  });

  test('allows an rrhh_admin to reach the candidate creation route', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'rrhh_admin');

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app\/candidates\/new$/);
    await context.close();
  });

  test('does not expose the removed application-owned MFA route', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'rrhh_admin');

    await page.goto('/mfa');
    await expect(page).toHaveURL(/\/app$/);

    await context.close();
  });

  test('does not restore the removed MFA flow for an unauthenticated caller', async ({ page }) => {
    await page.goto('/mfa');
    await expect(page).toHaveURL(/\/login$/);
  });
});
