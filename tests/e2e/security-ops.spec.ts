import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.describe('Security operational flows', () => {
  test('blocks export action for readonly user', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('readonly') });
    const page = await context.newPage();

    await page.goto('/app/search');
    await expect(page.locator('button:has-text("Exportar CSV")')).toBeDisabled();

    await context.close();
  });

  test('uploads and opens candidate CV with secure action as admin', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('rrhh_admin') });
    const page = await context.newPage();
    const suffix = Date.now().toString();

    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', `Doc${suffix}`);
    await page.fill('input[name="lastName"]', 'Secure');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);

    const pdfBuffer = Buffer.from('%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF', 'utf-8');
    await page.setInputFiles('input[name="file"]', {
      name: 'cv.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer,
    });
    await page.click('button:has-text("Subir CV")');

    await expect(page.locator('text=CV subido correctamente.')).toBeVisible();
    await expect(page.locator('button:has-text("Abrir seguro")')).toBeEnabled();

    const popupPromise = page.waitForEvent('popup');
    await page.click('button:has-text("Abrir seguro")');
    const popup = await popupPromise;
    await expect(popup).toHaveURL(/signed-url-placeholder|blob:|http/);

    await context.close();
  });
});
