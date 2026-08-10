import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Candidate documents flow', () => {
  test('uploads a CV and opens it through secure action', async ({ page }) => {
    const suffix = Date.now().toString();

    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', `CV${suffix}`);
    await page.fill('input[name="lastName"]', 'Flow');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);

    const pdfBuffer = Buffer.from('%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF', 'utf-8');
    await page.setInputFiles('input[name="file"]', {
      name: 'candidate-cv.pdf',
      mimeType: 'application/pdf',
      buffer: pdfBuffer,
    });
    await page.click('button:has-text("Subir CV")');

    await expect(page.locator('text=CV subido correctamente.')).toBeVisible();

    const popupPromise = page.waitForEvent('popup');
    await page.click('button:has-text("Abrir seguro")');
    const popup = await popupPromise;
    await expect(popup).toHaveURL(/signed-url-placeholder|blob:|http/);
  });
});
