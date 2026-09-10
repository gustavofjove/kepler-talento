import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';
import { ensureSearchCandidate } from './support/seed-candidate';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Advanced search presets', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('saves, loads, and deletes a preset', async ({ page }) => {
    // The filter panel starts open; it collapses once a search has been run.
    await page.goto('/app/search');

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

  test('a saved preset survives a reload because it lives on the server', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.fill('input[name="presetName"]', 'Preset E2E Persistente');
    await page.click('button:has-text("Guardar actual")');
    await expect(
      page.locator('select[name="selectedPreset"] option', { hasText: 'Preset E2E Persistente' }),
    ).toHaveCount(1);

    // A full reload with fresh browser state: local storage could not answer this, and that
    // is the point of the cutover.
    await page.context().clearCookies();
    await page.reload();

    const restored = page.locator('select[name="selectedPreset"] option', {
      hasText: 'Preset E2E Persistente',
    });
    await expect(restored).toHaveCount(1);

    await page.selectOption('select[name="selectedPreset"]', { label: 'Preset E2E Persistente' });
    // Applying is a server round trip that fills the name field with the applied preset's
    // name; renaming before it lands would be overwritten by it.
    await expect(page.locator('input[name="presetName"]')).toHaveValue('Preset E2E Persistente');
    await page.fill('input[name="presetName"]', 'Preset E2E Renombrado');
    await page.click('button:has-text("Guardar actual")');
    await expect(
      page.locator('select[name="selectedPreset"] option', { hasText: 'Preset E2E Renombrado' }),
    ).toHaveCount(1);
    await expect(restored).toHaveCount(0);

    await page.click('button:has-text("Eliminar")');
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.getByTestId('confirm-accept').click();
    await expect(
      page.locator('select[name="selectedPreset"] option', { hasText: 'Preset E2E Renombrado' }),
    ).toHaveCount(0);
  });

  test('retained legacy browser presets are ignored and left untouched', async ({ page }) => {
    await page.goto('/app/search');
    // Written by hand because the application can no longer produce it: the key is gone from
    // the source. What must hold is that its presence changes nothing.
    const retained = JSON.stringify([{ id: 'legacy-1', name: 'Preset Antiguo', filters: {} }]);
    await page.evaluate((value) => localStorage.setItem('rrhh.search.presets.v1', value), retained);
    await page.reload();

    await expect(
      page.locator('select[name="selectedPreset"] option', { hasText: 'Preset Antiguo' }),
    ).toHaveCount(0);
    // Ignored, not confiscated: it is not this application's data to delete.
    expect(await page.evaluate(() => localStorage.getItem('rrhh.search.presets.v1'))).toBe(
      retained,
    );

    await page.evaluate(() => localStorage.removeItem('rrhh.search.presets.v1'));
  });
});
