import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';
import { ensureSearchCandidate } from './support/seed-candidate';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('UX accessibility flows', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('keyboard users can skip to main content from shell', async ({ page }) => {
    await page.goto('/app/candidates');
    await page.keyboard.press('Tab');
    const skipLink = page.locator('a.skip-link');
    await expect(skipLink).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page.locator('#main-content')).toBeFocused();
  });

  test('high-risk actions show custom confirmation dialog and can be cancelled by keyboard', async ({
    page,
  }) => {
    // Deleting a preset is the high-risk action: since KTL-14 it lives in Admin › Presets,
    // not on the search page.
    const presetName = `Preset UX ${Date.now()}`;
    await page.goto('/app/admin/presets/new');
    await page.getByTestId('preset-name').fill(presetName);
    await page.fill('input[name="text"]', 'Laura');
    await page.getByTestId('preset-save').click();
    await expect(page).toHaveURL(/\/app\/admin\/presets$/);

    await page.fill('input[name="presetFilter"]', presetName);
    const row = page.getByTestId('preset-row');
    await expect(row).toHaveCount(1);

    await row.getByTestId('preset-delete').click();
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('confirm-dialog')).toHaveCount(0);
    // Cancelled means nothing happened: the preset is still listed.
    await expect(row).toHaveCount(1);

    // Leave the shared library as it was found.
    await row.getByTestId('preset-delete').click();
    await page.getByTestId('confirm-accept').click();
    await expect(row).toHaveCount(0);
  });
});
