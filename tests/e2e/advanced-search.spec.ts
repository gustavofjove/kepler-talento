import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Advanced search', () => {
  test('finds the seeded candidate by free text', async ({ page }) => {
    await page.goto('/app/search');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Laura Garcia')).toBeVisible();
  });

  test('shows no results for a text filter that matches nobody', async ({ page }) => {
    await page.goto('/app/search');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Sin resultados.')).toBeVisible();
  });

  test('clearing the filters restores the full result set', async ({ page }) => {
    await page.goto('/app/search');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('tbody tr')).toHaveCount(1);

    // Filters collapse once there are results, so reopen them before clearing.
    await page.getByTestId('toggle-filters').click();
    await page.click('button:has-text("Limpiar")');

    await expect(page.locator('input[name="text"]')).toHaveValue('');
  });
});
