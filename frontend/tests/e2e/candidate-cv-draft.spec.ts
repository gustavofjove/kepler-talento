import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { CANDIDATE_PAGE_URL } from './support/candidate-panels';
import { syntheticCvPdf } from './support/synthetic-cv';

test.use({ storageState: authFile('rrhh_admin') });

/**
 * KTL-32: «Nuevo candidato» pre-filled from a CV. The CV is a fictitious person built in memory;
 * its e-mail carries the Date.now() marker, so the teardown purges the saved candidate.
 */
test.describe('Candidate draft from a CV', () => {
  const cv = (marker: string) =>
    syntheticCvPdf([
      { text: 'Anselmo Quintana Robles', size: 24 },
      { text: 'Desarrollador backend', size: 12 },
      { text: `anselmo.quintana.${marker}@example.test`, size: 10 },
      { text: '+34 611 987 654', size: 10 },
      { text: 'Calle Inventada 1, 48001 Bilbao', size: 10 },
      { text: 'Experiencia', size: 14 },
    ]);

  const pick = (page: import('@playwright/test').Page, marker: string) =>
    page.getByTestId('cv-draft-file').setInputFiles({
      name: `cv-${marker}.pdf`,
      mimeType: 'application/pdf',
      buffer: cv(marker),
    });

  test('fills the empty fields, marks them, and saves only on «Guardar»', async ({ page }) => {
    const marker = Date.now().toString();
    await page.goto('/app/candidates/new');

    await pick(page, marker);

    await expect(page.locator('input[name="firstName"]')).toHaveValue('Anselmo', {
      timeout: 90_000,
    });
    await expect(page.locator('input[name="lastName"]')).toHaveValue('Quintana Robles');
    await expect(page.locator('input[name="email"]')).toHaveValue(
      `anselmo.quintana.${marker}@example.test`,
    );
    await expect(page.locator('input[name="phone"]')).toHaveValue('611 98 76 54');
    await expect(page.locator('input[name="location"]')).toHaveValue('Bilbao');
    await expect(page.locator('input[name="province"]')).toHaveValue('Bizkaia');
    await expect(page.getByTestId('firstName-suggested')).toBeVisible();
    await expect(page.getByTestId('cv-draft-status')).not.toBeEmpty();
    // Nothing is saved yet: still on the create page.
    await expect(page).toHaveURL(/\/app\/candidates\/new$/);

    await page.locator('input[name="lastName"]').fill('Quintana Editado');
    await expect(page.getByTestId('lastName-suggested')).toHaveCount(0);
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
    await expect(page.locator('h1')).toContainText('Anselmo Quintana Editado');
    await expect(page.getByTestId('candidate-documents')).not.toContainText(`cv-${marker}.pdf`);
  });

  test('keeps a value typed before the CV was picked', async ({ page }) => {
    const marker = Date.now().toString();
    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', `Tecleado${marker}`);

    await pick(page, marker);

    await expect(page.locator('input[name="lastName"]')).toHaveValue('Quintana Robles', {
      timeout: 90_000,
    });
    await expect(page.locator('input[name="firstName"]')).toHaveValue(`Tecleado${marker}`);
    await expect(page.getByTestId('firstName-suggested')).toHaveCount(0);
  });
});
