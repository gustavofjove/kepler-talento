import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

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

    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
    await expect(page.locator('h1')).toContainText(`${firstName} ${lastName}`);

    await page.click('a:has-text("Editar")');
    await page.fill('input[name="lastName"]', `${lastName} Editado`);
    await page.click('button[type="submit"]');

    await expect(page.locator('h1')).toContainText(`${firstName} ${lastName} Editado`);

    await page.goto('/app/candidates');
    await expect(page.locator(`text=${firstName} ${lastName} Editado`)).toBeVisible();

    await page.click(`tr:has-text("${firstName}") >> text=Abrir`);
    await page.click('button:has-text("Baja logica")');

    await expect(page.locator('p:has-text("Activo:")')).toContainText('No');

    await page.goto('/app/candidates');
    await expect(page.locator(`text=${firstName} ${lastName} Editado`)).toHaveCount(0);
  });

  test('blocks empty submission with a validation message', async ({ page }) => {
    await page.goto('/app/candidates/new');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=Nombre y apellidos son obligatorios.')).toBeVisible();
    await expect(page).toHaveURL(/\/app\/candidates\/new$/);
  });
});
