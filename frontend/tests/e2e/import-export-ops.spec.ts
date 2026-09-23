import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { ensureSearchCandidate } from './support/seed-candidate';

test.use({ storageState: authFile('rrhh_admin') });

// The import journey moved to candidate-import.spec.ts with KTL-17, when import stopped being a
// browser stub and started creating candidates.
test.describe('Export operations', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('records export batches in search history modal', async ({ page }) => {
    await page.goto('/app/search');
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
