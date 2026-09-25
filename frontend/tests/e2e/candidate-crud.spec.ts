import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { CANDIDATE_PAGE_URL, editPanel, savePanel } from './support/candidate-panels';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Candidate CRUD', () => {
  test('creates, edits, and logically deactivates a candidate', async ({ page }) => {
    const uniqueSuffix = Date.now().toString();
    const firstName = `Test${uniqueSuffix}`;
    const lastName = 'Candidato';

    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', firstName);
    await page.fill('input[name="lastName"]', lastName);
    await page.click('button[type="submit"]');

    // KTL-29: creating opens the candidate page, where Datos principales is edited in place.
    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
    await editPanel(page, 'main');
    await page.fill('input[name="lastName"]', `${lastName} Editado`);
    await savePanel(page, 'main');
    await expect(page.locator('.toast')).toBeVisible();
    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
    await expect(page.locator('h1')).toContainText(`${firstName} ${lastName} Editado`);

    // KTL-23: the breadcrumb names the stored record and leads back to the list.
    const breadcrumb = page.getByTestId('breadcrumb');
    await expect(breadcrumb.locator('[aria-current="page"]')).toHaveText(
      `${firstName} ${lastName} Editado`,
    );

    await breadcrumb.getByTestId('breadcrumb-candidates').click();
    await expect(page).toHaveURL(/\/app\/candidates$/);
    await expect(page.getByTestId('breadcrumb')).toHaveCount(0);
    await expect(page.locator(`text=${firstName} ${lastName} Editado`)).toBeVisible();

    // KTL-31: the row opens the candidate. Click a plain cell (the update date), not the name link.
    const row = page.getByTestId('candidate-row').filter({ hasText: firstName });
    await expect(row.locator('a[href^="tel:"]')).toHaveCount(0);
    await row.getByRole('cell').last().click();
    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
    await page.click('button:has-text("Baja lógica")');
    await page.getByTestId('confirm-accept').click();

    await expect(page.getByTestId('candidate-active')).toHaveText('No');

    await page.goto('/app/candidates');
    await expect(page.locator(`text=${firstName} ${lastName} Editado`)).toHaveCount(0);
  });

  test('reverts a logical deactivation with alta lógica', async ({ page }) => {
    const uniqueSuffix = Date.now().toString();
    const firstName = `Alta${uniqueSuffix}`;

    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', firstName);
    await page.fill('input[name="lastName"]', 'Candidato');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);

    await page.click('button:has-text("Baja lógica")');
    await page.getByTestId('confirm-accept').click();
    await expect(page.getByTestId('candidate-active')).toHaveText('No');

    await page.click('button:has-text("Alta lógica")');
    await page.getByTestId('confirm-accept').click();
    await expect(page.getByTestId('candidate-active')).toHaveText('Sí');

    await page.goto('/app/candidates');
    await page.fill('input[name="text"]', firstName);
    await expect(page.locator(`tbody tr:has-text("${firstName}")`)).toHaveCount(1);
  });

  test('blocks empty submission with a validation message', async ({ page }) => {
    await page.goto('/app/candidates/new');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=Nombre y apellidos son obligatorios.')).toBeVisible();
    await expect(page).toHaveURL(/\/app\/candidates\/new$/);
  });
});
