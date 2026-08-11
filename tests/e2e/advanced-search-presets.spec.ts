import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Advanced search presets', () => {
  test('saves, loads, and deletes a preset', async ({ page }) => {
    await page.goto('/app/search');
    await page.getByTestId('toggle-filters').click();

    await page.fill('input[name="text"]', 'Laura');
    await page.selectOption('select[name="hasCv"]', 'yes');
    await page.fill('input[name="presetName"]', 'Preset E2E Laura');
    await page.click('button:has-text("Guardar actual")');

    const presetOption = page.locator('select[name="selectedPreset"] option', {
      hasText: 'Preset E2E Laura',
    });
    await expect(presetOption).toHaveCount(1);

    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('text=Sin resultados.')).toBeVisible();

    // Selecting a preset applies it right away; there is no separate load button.
    await page.selectOption('select[name="selectedPreset"]', { label: 'Preset E2E Laura' });

    await expect(page.locator('text=Laura Garcia')).toBeVisible();
    await page.getByTestId('toggle-filters').click();
    await expect(page.locator('input[name="text"]')).toHaveValue('Laura');
    await expect(page.locator('select[name="hasCv"]')).toHaveValue('yes');

    await page.click('button:has-text("Eliminar")');
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.getByTestId('confirm-accept').click();

    await expect(presetOption).toHaveCount(0);
  });
});
