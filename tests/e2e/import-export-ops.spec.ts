import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Import/Export operations', () => {
  test('supports dry-run with error download and explicit commit', async ({ page }) => {
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

    const downloadErrors = page.waitForEvent('download');
    await page.click('button:has-text("Descargar errores CSV")');
    await downloadErrors;

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

  test('records export batches in search history modal', async ({ page }) => {
    await page.goto('/app/search');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('text=Laura Garcia')).toBeVisible();

    const download = page.waitForEvent('download');
    await page.click('button:has-text("Exportar CSV")');
    await download;

    await page.getByTestId('open-export-history').click();
    const historyModal = page.getByTestId('export-history-modal');
    const historyRow = historyModal.locator('tbody tr').first();

    await expect(historyModal).toBeVisible();
    await expect(historyRow).toContainText('candidatos.csv');
    await expect(historyRow).toContainText('Completado');

    await page.getByTestId('close-export-history').click();
    await expect(historyModal).toHaveCount(0);
  });
});
