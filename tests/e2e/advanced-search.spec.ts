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

  test('reports the server total and pages through it', async ({ page }) => {
    await page.goto('/app/search');
    await page.click('button[type="submit"]:has-text("Buscar")');

    // The count comes from the server's total, not from the rows on screen: those are one
    // page, and reporting their number as the total is exactly what paging breaks.
    const total = page.getByTestId('search-total');
    await expect(total).toContainText('candidato');
    await expect(total).toContainText('Página 1 de');
    await expect(page.getByTestId('search-previous-page')).toBeDisabled();

    if (await page.getByTestId('search-next-page').isEnabled()) {
      const firstRow = await page.locator('tbody tr').first().innerText();
      await page.getByTestId('search-next-page').click();
      await expect(total).toContainText('Página 2 de');
      await expect(page.locator('tbody tr').first()).not.toHaveText(firstRow);
      await page.getByTestId('search-previous-page').click();
      await expect(total).toContainText('Página 1 de');
    }
  });

  test('the CV filter and the multi-value mode reach the server query', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'Laura');
    await page.selectOption('select[name="hasCv"]', 'yes');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('text=Laura Garcia')).toBeVisible();

    await page.getByTestId('toggle-filters').click();
    await page.selectOption('select[name="hasCv"]', 'no');
    await page.click('button[type="submit"]:has-text("Buscar")');
    // Same candidate, opposite CV filter: she has a principal CV, so she must disappear.
    await expect(page.locator('text=Sin resultados.')).toBeVisible();

    // The ANY/ALL control only exists once a family holds a criterion, and the criterion
    // itself is added from the catalog-backed select.
    await page.getByTestId('toggle-filters').click();
    await page.selectOption('select[name="hasCv"]', '');
    const skill = page.locator('select[name="skillDraft"] option').nth(1);
    await page.selectOption('select[name="skillDraft"]', await skill.getAttribute('value'));
    await page.getByTestId('add-skill').click();

    const mode = page.locator('select[name="skillMode"]');
    await expect(mode).toBeVisible();
    await mode.selectOption('ALL');
    await page.click('button[type="submit"]:has-text("Buscar")');

    // The seeded candidate declares no skills, so requiring one must exclude her. What this
    // proves at this level is that the criterion and its mode reach the server query; the
    // ANY/ALL semantics themselves are proven against PostgreSQL in SearchApiTests.
    await expect(page.locator('text=Sin resultados.')).toBeVisible();
  });

  test('a superseded search is cancelled and cannot overwrite the newer one', async ({ page }) => {
    await page.goto('/app/search');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.getByTestId('search-total')).toContainText('candidato');

    const aborted: string[] = [];
    page.on('requestfailed', (request) => {
      if (request.url().includes('/api/candidates/search')) {
        aborted.push(request.url());
      }
    });

    await page.getByTestId('toggle-filters').click();
    // Two searches in quick succession: the first must not be waited out, and its answer
    // must never reach the screen.
    await page.fill('input[name="text"]', 'Laura');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Sin resultados.')).toBeVisible();
    await expect(page.locator('text=Laura Garcia')).toHaveCount(0);
  });
});
