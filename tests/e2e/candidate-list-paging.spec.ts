import type { Page } from './fixtures';
import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders, signInAs } from './support/auth';

test.use({ storageState: authFile('rrhh_admin') });

const STATUSES = ['new', 'available', 'in_process', 'hired', 'rejected'];

/**
 * Creates `count` candidates sharing a unique first name, so the text filter isolates them
 * from whatever else the shared database holds. Created one after another, so the default
 * update-time ordering puts the last one first.
 */
async function seed(page: Page, marker: string, count: number): Promise<void> {
  for (let index = 0; index < count; index += 1) {
    const response = await page.request.post('/api/candidates', {
      headers: authorizationHeaders(page),
      data: {
        firstName: marker,
        lastName: `Apellido${String(index).padStart(2, '0')}`,
        phone: '',
        email: '',
        location: '',
        province: '',
        country: 'España',
        availability: 'Inmediata',
        status: STATUSES[index % STATUSES.length],
        source: 'Email',
        notes: '',
        receivedAt: '2026-09-16',
        consentAt: '',
        reviewDueAt: '',
      },
    });
    expect(response.ok()).toBe(true);
  }
}

const rows = (page: Page) => page.getByTestId('candidate-row');

async function filterByText(page: Page, marker: string, expectedRows: number): Promise<void> {
  await page.fill('input[name="text"]', marker);
  await expect(rows(page)).toHaveCount(expectedRows);
}

test.describe('Candidate list paged by the server', () => {
  // One seeded set shared by the tests below, so they run in order in one worker.
  test.describe.configure({ mode: 'serial' });
  const marker = `Paginado${Date.now()}`;
  let seeded = false;

  test.beforeEach(async ({ page }) => {
    if (!seeded) {
      await seed(page, marker, 27);
      seeded = true;
    }
  });

  test('pages through the matching set with the default page size of 25', async ({ page }) => {
    await page.goto('/app/candidates');
    await filterByText(page, marker, 25);
    await expect(page.getByTestId('candidate-list-total')).toContainText('27');

    await page.locator('button[name="nextPage"]').click();

    await expect(rows(page)).toHaveCount(2);
    await expect(page).toHaveURL(/[?&]page=2(&|$)/);
    // Nothing from page one reappears on page two.
    await expect(rows(page).filter({ hasText: 'Apellido26' })).toHaveCount(0);
  });

  test('sorts the whole matching set by each field', async ({ page }) => {
    await page.goto('/app/candidates?pageSize=100');
    await filterByText(page, marker, 27);

    // Default: most recently updated first.
    await expect(rows(page).first()).toContainText('Apellido26');

    await page.getByTestId('candidate-sort-lastName').click();
    await expect(page).toHaveURL(/sort=lastName/);
    await expect(rows(page).first()).toContainText('Apellido00');
    await page.getByTestId('candidate-sort-lastName').click();
    await expect(page).toHaveURL(/dir=desc/);
    await expect(rows(page).first()).toContainText('Apellido26');

    await page.getByTestId('candidate-sort-status').click();
    await expect(page).toHaveURL(/sort=status/);
    await expect(page.locator('th[aria-sort="ascending"]')).toHaveCount(1);
    await expect(rows(page)).toHaveCount(27);

    await page.getByTestId('candidate-sort-updatedAt').click();
    // Back to the default sort, which the URL omits.
    await expect(page).not.toHaveURL(/sort=/);
    await expect(rows(page).first()).toContainText('Apellido26');
  });

  test('filters on the server', async ({ page }) => {
    await page.goto('/app/candidates?pageSize=100');
    await filterByText(page, marker, 27);

    await page.locator('select[name="status"]').selectOption('hired');

    // Indices 3, 8, 13, 18 and 23 are "hired".
    await expect(rows(page)).toHaveCount(5);
    await expect(page).toHaveURL(/status=hired/);
  });

  test('reopens a copied list URL to the same view', async ({ page, context }) => {
    await page.goto('/app/candidates');
    await page.locator('select[name="status"]').selectOption('available');
    await page.getByTestId('candidate-sort-lastName').click();
    await page.locator('select[name="pageSize"]').selectOption('50');
    await expect(page).toHaveURL(/pageSize=50/);
    const copied = page.url();

    // A separate page that signs in on its own, as a colleague opening the link would.
    const reopened = await context.newPage();
    await signInAs(reopened, 'rrhh_admin');
    await reopened.goto(copied);

    await expect(reopened.locator('select[name="status"]')).toHaveValue('available');
    await expect(reopened.locator('select[name="pageSize"]')).toHaveValue('50');
    await expect(
      reopened.locator('th[aria-sort="ascending"]').getByTestId('candidate-sort-lastName'),
    ).toBeVisible();
    // The free-text filter is personal data and never travels in the URL.
    expect(copied).not.toContain(marker);
  });
});
