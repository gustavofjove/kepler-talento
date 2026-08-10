import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Import access CSV flow', () => {
  test('validates dry-run errors and then commits valid data', async ({ page }) => {
    await page.goto('/app/admin/import');
    const commitButton = page.getByRole('button', { name: 'Confirmar commit', exact: true });

    const invalidCsv = Buffer.from('first_name,last_name,email\nAna,,bad-email\n', 'utf-8');
    await page.setInputFiles('input[type="file"]', {
      name: 'invalid.csv',
      mimeType: 'text/csv',
      buffer: invalidCsv,
    });

    await page.click('button:has-text("Validar (dry run)")');
    await expect(page.locator('text=Modo: Dry run')).toBeVisible();
    await expect(page.locator('text=Filas con error: 1')).toBeVisible();
    await expect(commitButton).toBeDisabled();

    const validCsv = Buffer.from(
      'first_name,last_name,email\nAna,Perez,ana@example.com\n',
      'utf-8',
    );
    await page.setInputFiles('input[type="file"]', {
      name: 'valid.csv',
      mimeType: 'text/csv',
      buffer: validCsv,
    });

    await page.click('button:has-text("Validar (dry run)")');
    await expect(page.locator('text=Filas con error: 0')).toBeVisible();
    await expect(commitButton).toBeEnabled();

    await commitButton.click();
    await expect(page.locator('text=Modo: Carga')).toBeVisible();
    await expect(page.locator('text=Filas cargadas: 1')).toBeVisible();
  });
});
