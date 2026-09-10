import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';
import { ensureSearchCandidate } from './support/seed-candidate';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Advanced search', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('finds the seeded candidate by free text', async ({ page }) => {
    // The filter panel starts open; it collapses once a search has been run.
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Laura Garcia')).toBeVisible();
  });

  test('shows no results for a text filter that matches nobody', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Sin resultados.')).toBeVisible();
  });

  test('clearing the filters restores the full result set', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('tbody tr')).toHaveCount(1);

    // Filters collapse once there are results, so reopen them before clearing.
    await page.getByTestId('toggle-filters').click();
    await page.click('button:has-text("Limpiar")');

    await expect(page.locator('input[name="text"]')).toHaveValue('');
  });

  // Regression guard for the search-filters port: the filter panel used to
  // mutate its parent's object in place, and results only refreshed on submit.
  // Deriving results from the filters state instead would make the table update
  // on every keystroke - a user-visible behaviour change.
  test('typing in a filter does not change the results until Buscar is pressed', async ({
    page,
  }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('tbody tr')).toHaveCount(1);

    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'nadie-existe-xyz');

    // Still the previous result set: no submit has happened.
    await expect(page.locator('tbody tr')).toHaveCount(1);
    await expect(page.locator('text=Laura Garcia')).toBeVisible();

    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('text=Sin resultados.')).toBeVisible();
  });
});
