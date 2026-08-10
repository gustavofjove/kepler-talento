import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.describe('Export results flow', () => {
  test('allows export for admin users with permission', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('rrhh_admin') });
    const page = await context.newPage();

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
    const context = await browser.newContext({ baseURL, storageState: authFile('readonly') });
    const page = await context.newPage();

    await page.goto('/app/search');
    await expect(page.locator('button:has-text("Exportar CSV")')).toBeDisabled();

    await context.close();
  });
});
