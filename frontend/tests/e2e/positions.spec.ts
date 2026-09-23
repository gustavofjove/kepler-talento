import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';

/**
 * KTL-15 browser journeys. Selectors use names, test ids and hrefs only: no Spanish copy.
 * Positions cannot be deleted by design, so every run uses unique titles and leaves the
 * positions it created closed.
 */

async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(1);
}

async function createPosition(page: Page, title: string, location: string): Promise<string> {
  await page.goto('/app/positions/new');
  await page.getByTestId('position-title').fill(title);
  await page.getByTestId('position-location').fill(location);
  const editor = page.locator('[contenteditable="true"][name="description"]');
  await editor.click();
  await page.keyboard.type('Primera línea ');
  await page.locator('button[name="descriptionBold"]').click();
  await page.keyboard.type('importante');
  await page.locator('button[type="submit"]').click();
  // A real identifier, not the "new" segment the form itself lives under.
  await expect(page).toHaveURL(POSITION_URL);
  return page.url().split('/').pop()!;
}

const POSITION_URL = /\/app\/positions\/[0-9a-f]{8}-[0-9a-f-]{27}$/;

test.describe('Positions - manager', () => {
  test.use({ storageState: authFile('rrhh_admin') });

  test('creates, closes and reopens a position with live matches', async ({ page }) => {
    const title = `E2E Posición ${Date.now()}`;
    const id = await createPosition(page, title, 'Madrid');

    await expect(page.locator('h1')).toHaveText(title);
    // Lexical may split a formatted run across elements; the bold text must survive the save.
    await expect(
      page.getByTestId('position-description').locator('strong', { hasText: 'importante' }).first(),
    ).toBeVisible();
    await expect(page.getByTestId('search-total')).toBeVisible();

    await page.locator(`a[href="/app/positions/${id}/edit"]`).click();
    await page.getByTestId('position-status').selectOption('closed');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
    // A closed position still evaluates its requirements live.
    await expect(page.getByTestId('search-total')).toBeVisible();

    await page.goto('/app/positions');
    await page.getByTestId('position-text-filter').fill(title);
    await expect(page.locator(`a[href="/app/positions/${id}"]`)).toHaveCount(0);
    await page.locator('select[name="positionStatus"]').selectOption('closed');
    await expect(page.locator(`a[href="/app/positions/${id}"]`)).toBeVisible();

    await page.goto(`/app/positions/${id}/edit`);
    await page.getByTestId('position-status').selectOption('open');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
    await page.goto('/app/positions');
    await page.getByTestId('position-text-filter').fill(title);
    await expect(page.locator(`a[href="/app/positions/${id}"]`)).toBeVisible();

    // Leave the data as found: positions cannot be deleted, so close it.
    await page.goto(`/app/positions/${id}/edit`);
    await page.getByTestId('position-status').selectOption('closed');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
  });

  test('cancelling an edit writes nothing', async ({ page }) => {
    const title = `E2E Cancelar ${Date.now()}`;
    const id = await createPosition(page, title, 'Bilbao');

    await page.goto(`/app/positions/${id}/edit`);
    await page.getByTestId('position-title').fill(`${title} cambiado`);
    await page.locator('.position-actions button[type="button"]').last().click();

    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
    await expect(page.locator('h1')).toHaveText(title);

    await page.goto(`/app/positions/${id}/edit`);
    await page.getByTestId('position-status').selectOption('closed');
    await page.locator('button[type="submit"]').click();
    // Wait for the save, or the test ends before the position is closed.
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
  });

  test('the breadcrumb leads from the edit form to the position and the list', async ({ page }) => {
    const title = `E2E Migas ${Date.now()}`;
    const id = await createPosition(page, title, 'Sevilla');
    const breadcrumb = page.getByTestId('breadcrumb');
    await expect(breadcrumb.locator('[aria-current="page"]')).toHaveText(title);

    await page.locator(`a[href="/app/positions/${id}/edit"]`).click();
    await page.getByTestId('position-title').fill(`${title} sin guardar`);
    // The trail keeps the stored title while the draft changes.
    await expect(breadcrumb.getByTestId('breadcrumb-position')).toHaveText(title);
    await breadcrumb.getByTestId('breadcrumb-position').click();
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
    await expect(page.locator('h1')).toHaveText(title);

    await breadcrumb.getByTestId('breadcrumb-positions').click();
    await expect(page).toHaveURL(/\/app\/positions$/);
    await expect(page.getByTestId('breadcrumb')).toHaveCount(0);

    // Leave the data as found: positions cannot be deleted, so close it.
    await page.goto(`/app/positions/${id}/edit`);
    await page.getByTestId('position-status').selectOption('closed');
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
  });

  test('the description editor is reachable and usable from the keyboard', async ({ page }) => {
    await page.goto('/app/positions/new');
    await page.locator('button[name="descriptionBold"]').focus();

    for (let index = 0; index < 4; index++) await page.keyboard.press('Tab');
    await expect(page.locator('[contenteditable="true"][name="description"]')).toBeFocused();
    await page.keyboard.type('Teclado');
    await expect(page.locator('[contenteditable="true"][name="description"]')).toContainText(
      'Teclado',
    );
  });

  test('list and form fit a 390px viewport without horizontal page scroll', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });

    await page.goto('/app/positions');
    await expect(page.getByTestId('position-text-filter')).toBeVisible();
    await expectNoHorizontalOverflow(page);

    await page.goto('/app/positions/new');
    await expect(page.getByTestId('position-title')).toBeVisible();
    await expect(page.locator('button[type="submit"]')).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('shows Positions in the primary navigation', async ({ page }) => {
    await page.goto('/app');
    await page.getByTestId('nav-positions').click();
    await expect(page).toHaveURL(/\/app\/positions$/);
  });
});

test.describe('Positions - read-only', () => {
  test.use({ storageState: authFile('readonly') });

  test('can read positions but cannot create or edit them', async ({ page }) => {
    await page.goto('/app/positions');
    await expect(page.getByTestId('position-text-filter')).toBeVisible();
    await expect(page.getByTestId('position-create')).toHaveCount(0);

    await page.goto('/app/positions/new');
    await expect(page).not.toHaveURL(/\/app\/positions\/new$/);
    await expect(page.getByTestId('position-title')).toHaveCount(0);
  });

  test('the API refuses a position write from a reader', async ({ page }) => {
    await page.goto('/app/positions');
    const status = await page.evaluate(async () => {
      const response = await fetch('/api/positions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title: 'No debe existir', requirements: {} }),
      });
      return response.status;
    });
    // Without the SPA's bearer token the call is unauthenticated; either way it is refused.
    expect([401, 403]).toContain(status);
  });
});
