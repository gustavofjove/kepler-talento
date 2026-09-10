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
    await page.goto('/app/search');

    const presetName = `Preset UX ${Date.now()}`;
    await page.fill('input[name="text"]', 'Laura');
    await page.fill('input[name="presetName"]', presetName);
    await page.click('button:has-text("Guardar actual")');

    await page.selectOption('select[name="selectedPreset"]', { label: presetName });
    await page.click('button:has-text("Eliminar")');
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('confirm-dialog')).toHaveCount(0);
  });
});
