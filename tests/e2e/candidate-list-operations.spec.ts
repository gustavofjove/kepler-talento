import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Candidate list operations', () => {
  test('filters, sorts, and applies bulk logical deactivation', async ({ page }) => {
    const suffix = Date.now().toString();
    const createCandidate = async (firstName: string, lastName: string) => {
      await page.goto('/app/candidates/new');
      await page.fill('input[name="firstName"]', firstName);
      await page.fill('input[name="lastName"]', lastName);
      await page.click('button[type="submit"]');
      await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
    };

    await createCandidate(`Filtro${suffix}`, 'Uno');
    await createCandidate(`Filtro${suffix}`, 'Dos');

    await page.goto('/app/candidates');
    await page.fill('input[name="text"]', `Filtro${suffix}`);
    await expect(page.locator('tbody tr')).toHaveCount(2);

    await page.click('button:has-text("Nombre")');
    await expect(page.locator('tbody tr').first()).toContainText(`Filtro${suffix}`);

    const rowOne = page
      .locator(`tbody tr:has-text("Filtro${suffix} Uno") input[type="checkbox"]`)
      .first();
    const rowTwo = page
      .locator(`tbody tr:has-text("Filtro${suffix} Dos") input[type="checkbox"]`)
      .first();
    await rowOne.check();
    await rowTwo.check();

    await page.click('button:has-text("Baja logica masiva")');
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.getByTestId('confirm-accept').click();

    await expect(page.locator('text=Baja logica aplicada a 2 candidato(s).')).toBeVisible();

    await page.click('button:has-text("Limpiar")');
    await expect(page.locator('input[name="text"]')).toHaveValue('');
    await expect(page.locator(`text=Filtro${suffix} Uno`)).toHaveCount(0);
    await expect(page.locator(`text=Filtro${suffix} Dos`)).toHaveCount(0);
  });
});
