import { expect, test } from './fixtures';
import { signInAs } from './support/auth';
import { ensureSearchCandidate } from './support/seed-candidate';

test.describe('Export results flow', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('allows export for admin users with permission', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'rrhh_admin');

    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('text=Laura Garcia')).toBeVisible();

    const download = page.waitForEvent('download');
    await page.click('button:has-text("Exportar CSV")');
    await download;

    await context.close();
  });

  test('blocks export for readonly users', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'readonly');

    await page.goto('/app/search');
    await expect(page.locator('button:has-text("Exportar CSV")')).toBeDisabled();

    await context.close();
  });
});
