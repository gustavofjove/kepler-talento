import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';
import { ensureSearchCandidate } from './support/seed-candidate';

/**
 * KTL-31: every record table fits a 390px viewport. Wide content scrolls inside the table's own
 * container, never the page. Selectors use test ids, names and roles only: no Spanish copy.
 */

test.use({ storageState: authFile('rrhh_admin') });

async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(1);
}

test.describe('Record tables at 390px', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('Candidatos, Catálogos and Usuarios scroll inside their own container', async ({
    page,
    request,
  }) => {
    await ensureSearchCandidate(request);

    await page.goto('/app/candidates');
    await expect(page.getByTestId('candidate-row').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);

    await page.goto('/app/catalogs');
    await expect(page.getByTestId('catalog-row').first()).toBeVisible();
    await expect(page.getByTestId('catalog-move-up').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);

    await page.goto('/app/admin/users');
    await expect(page.getByTestId('users-table').locator('tbody tr').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('Presets, with their line of criteria chips, fit without page scroll', async ({ page }) => {
    const name = `Preset E2E Estrecho ${Date.now()}`;
    await page.goto('/app/admin/presets/new');
    await page.getByTestId('preset-name').fill(name);
    await page.fill('input[name="text"]', 'Laura');
    await page.getByTestId('preset-save').click();
    await expect(page).toHaveURL(/\/app\/admin\/presets$/);

    await page.fill('input[name="presetFilter"]', name);
    const row = page.getByTestId('preset-row');
    await expect(row).toHaveCount(1);
    await expect(row.getByTestId('preset-criteria').locator('.chip').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);

    // Leave nothing behind (the teardown would also purge it by its Date.now() marker).
    await row.getByTestId('preset-delete').click();
    await page.getByTestId('confirm-accept').click();
    await expect(row).toHaveCount(0);
  });
});
