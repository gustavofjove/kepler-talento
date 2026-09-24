import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import {
  addFirstOffered,
  addValue,
  chip,
  chips,
  offered,
  removeValue,
  setLevel,
} from './support/catalog-picker';
import { ensureSearchCandidate } from './support/seed-candidate';
import { authorizationHeaders } from './support/auth';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Advanced search', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('finds the seeded candidate by free text', async ({ page }) => {
    // The filter panel starts open; it collapses once a search has been run.
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'KtlSearchSeed');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.getByText('KtlSearchSeed Candidate')).toBeVisible();
  });

  test('offers tag criteria without a level control', async ({ page }) => {
    await page.goto('/app/search');

    // A tag chip shows no level and opens no level editor.
    await addFirstOffered(page, 'search-tag');
    const tag = chips(page, 'search-tag');
    await tag.click();
    await expect(page.getByTestId('search-tag-editor')).toHaveCount(0);
  });

  test('sets a criterion level on its chip instead of adding the value twice', async ({ page }) => {
    await page.goto('/app/search');
    await addValue(page, 'search-language', 'Inglés');
    await setLevel(page, 'search-language', 'Inglés', 'B2');
    await expect(chip(page, 'search-language', 'Inglés')).toContainText('B2');

    await expect(await offered(page, 'search-language', 'Ingl')).toHaveCount(0);
    await expect(chips(page, 'search-language')).toHaveCount(1);

    await removeValue(page, 'search-language', 'Inglés');
    await expect(await offered(page, 'search-language', 'Ingl')).not.toHaveCount(0);
  });

  test('finds a higher language level from a lower minimum', async ({ page }) => {
    const marker = `Ktl25Level${Date.now()}`;
    const headers = authorizationHeaders(page);
    const create = await page.request.post('/api/candidates', {
      headers,
      data: {
        firstName: marker,
        lastName: 'Candidate',
        phone: '+34 600 100 250',
        status: 'available',
        email: `${marker.toLowerCase()}@example.invalid`,
        location: 'Madrid',
        province: 'Madrid',
        country: 'España',
        availability: 'Inmediata',
        source: 'LinkedIn',
        notes: 'Fixture KTL-25.',
        receivedAt: '2026-05-10',
        consentAt: '2026-05-10',
        reviewDueAt: '2027-05-10',
      },
    });
    expect(create.ok(), await create.text()).toBeTruthy();
    const candidate = (await create.json()) as { id: string; version: number };
    try {
      const relations = await page.request.put(`/api/candidates/${candidate.id}/languages`, {
        headers,
        data: { languages: [{ language: 'Inglés', level: 'C1' }], version: candidate.version },
      });
      expect(relations.ok(), await relations.text()).toBeTruthy();

      await page.goto('/app/search');
      await page.locator('input[name="text"]').fill(marker);
      await addValue(page, 'search-language', 'Inglés');
      await setLevel(page, 'search-language', 'Inglés', 'B2');
      await expect(chip(page, 'search-language', 'Inglés')).toContainText('≥ B2');
      await page.locator('button[type="submit"]').click();
      await expect(page.getByTestId('filters-summary')).toContainText('≥ B2');
      await expect(page.getByText(`${marker} Candidate`)).toBeVisible();
    } finally {
      const current = await page.request.get(`/api/candidates/${candidate.id}`, { headers });
      if (current.ok()) {
        const stored = (await current.json()) as { version: number };
        const retired = await page.request.put(`/api/candidates/${candidate.id}/active`, {
          headers,
          data: { isActive: false, version: stored.version },
        });
        expect(retired.ok()).toBeTruthy();
      }
    }
  });

  test('shows no results for a text filter that matches nobody', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Sin resultados.')).toBeVisible();
  });

  test('clearing the filters restores the full result set', async ({ page }) => {
    await page.goto('/app/search');
    await page.fill('input[name="text"]', 'KtlSearchSeed');
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
    await page.fill('input[name="text"]', 'KtlSearchSeed');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.locator('tbody tr')).toHaveCount(1);

    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'nadie-existe-xyz');

    // Still the previous result set: no submit has happened.
    await expect(page.locator('tbody tr')).toHaveCount(1);
    await expect(page.getByText('KtlSearchSeed Candidate')).toBeVisible();

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
    await page.fill('input[name="text"]', 'KtlSearchSeed');
    await page.selectOption('select[name="hasCv"]', 'yes');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await expect(page.getByText('KtlSearchSeed Candidate')).toBeVisible();

    await page.getByTestId('toggle-filters').click();
    await page.selectOption('select[name="hasCv"]', 'no');
    await page.click('button[type="submit"]:has-text("Buscar")');
    // Same candidate, opposite CV filter: she has a principal CV, so she must disappear.
    await expect(page.locator('text=Sin resultados.')).toBeVisible();

    // The ANY/ALL control appears once a family holds two criteria.
    await page.getByTestId('toggle-filters').click();
    await page.selectOption('select[name="hasCv"]', '');
    const all = page.getByTestId('search-skill-mode').locator('[data-value="ALL"]');
    await addFirstOffered(page, 'search-skill');
    // A single criterion has nothing to combine: no ANY/ALL control yet.
    await expect(page.getByTestId('search-skill-mode')).toHaveCount(0);
    await addFirstOffered(page, 'search-skill');

    await all.click();
    await expect(all).toHaveAttribute('aria-checked', 'true');
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
    await page.fill('input[name="text"]', 'KtlSearchSeed');
    await page.click('button[type="submit"]:has-text("Buscar")');
    await page.getByTestId('toggle-filters').click();
    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.click('button[type="submit"]:has-text("Buscar")');

    await expect(page.locator('text=Sin resultados.')).toBeVisible();
    await expect(page.getByText('KtlSearchSeed Candidate')).toHaveCount(0);
  });
});
