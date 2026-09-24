import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';
import { addValue, chip, chips, offered, openInput, setLevel } from './support/catalog-picker';

test.use({ storageState: authFile('rrhh_admin') });

async function createCandidate(
  page: import('@playwright/test').Page,
  firstName: string,
  lastName: string,
) {
  await page.goto('/app/candidates/new');
  await page.fill('input[name="firstName"]', firstName);
  await page.fill('input[name="lastName"]', lastName);
  await page.click('button[type="submit"]');
  // KTL-22: the sections are edited on the edit page, where creation continues.
  await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+\/edit$/);
}

test.describe('Candidate profile enrichment', () => {
  test('adds a language and a skill, changes a level in place, and never duplicates', async ({
    page,
  }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Perfil${suffix}`, 'Test');
    const id = page.url().split('/').at(-2)!;
    const stored = async () =>
      (
        await (
          await page.request.get(`/api/candidates/${id}`, { headers: authorizationHeaders(page) })
        ).json()
      ).languages as { id: string; language: string; level: string }[];

    await addValue(page, 'candidate-language', 'Inglés', 'B2');
    await expect(chip(page, 'candidate-language', 'Inglés')).toContainText('B2');
    // A value the candidate already has is not offered, so it cannot be added twice.
    await expect(await offered(page, 'candidate-language', 'Ingl')).toHaveCount(0);

    // Abandoning the level choice saves nothing.
    await (await openInput(page, 'candidate-language')).fill('Alem');
    await page.getByRole('listbox').getByRole('option', { name: 'Alemán', exact: true }).click();
    await expect(page.getByTestId('candidate-language-editor')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('candidate-language-editor')).toHaveCount(0);
    await expect(chips(page, 'candidate-language')).toHaveCount(1);

    // The level changes in place: same entry, new level, nothing added or removed.
    const [before] = await stored();
    await setLevel(page, 'candidate-language', 'Inglés', 'C1');
    await expect(chip(page, 'candidate-language', 'Inglés')).toContainText('C1');
    await expect(chip(page, 'candidate-language', 'Inglés')).not.toHaveAttribute('data-status');
    expect(await stored()).toEqual([expect.objectContaining({ id: before.id, level: 'C1' })]);

    await addValue(page, 'candidate-skill', 'Compras', 'Medio');

    // A section change followed by a core save on the same page must not conflict.
    await page.fill('input[name="lastName"]', 'Test Editado');
    await page.click('form:has(input[name="firstName"]) button[type="submit"]');
    await expect(page.locator('.toast')).toBeVisible();
    await expect(page).toHaveURL(/\/edit$/);

    // The detail page shows what was added, read-only, even for an administrator.
    await page.getByTestId('candidate-edit-view').click();
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
    await expect(chips(page, 'candidate-skill')).toHaveCount(1);
    await expect(chip(page, 'candidate-language', 'Inglés')).toContainText('C1');
    for (const testId of [
      'candidate-languages',
      'candidate-programs',
      'candidate-education',
      'candidate-experience',
      'candidate-skills',
      'candidate-tags',
      'candidate-notes',
      'candidate-documents',
    ]) {
      const section = page.getByTestId(testId);
      await expect(section.locator('form')).toHaveCount(0);
      await expect(section.locator('button')).toHaveCount(0);
      await expect(section.getByRole('combobox')).toHaveCount(0);
    }
    await expect(page.getByTestId('document-file')).toHaveCount(0);
  });

  test('rejects experience with an end date before the start date', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Exp${suffix}`, 'Test');

    const experiencePanel = page.getByTestId('candidate-experience');
    await experiencePanel.locator('input[name="company"]').fill('Acme');
    await experiencePanel.locator('input[name="position"]').fill('Analista');
    await experiencePanel.locator('select[name="sector"]').selectOption('Servicios');
    await experiencePanel.locator('input[name="startDate"]').fill('2024-06-01');
    await experiencePanel.locator('input[name="endDate"]').fill('2024-01-01');
    await experiencePanel.locator('button:has-text("Añadir experiencia")').click();

    await expect(experiencePanel.locator('text=no puede ser anterior')).toBeVisible();
  });

  test('requires a degree before adding education', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Edu${suffix}`, 'Test');

    const educationPanel = page.getByTestId('candidate-education');
    await educationPanel.locator('select[name="educationType"]').selectOption('Grado');
    await educationPanel.locator('select[name="status"]').selectOption('Finalizada');
    await educationPanel.locator('button:has-text("Añadir formación")').click();

    await expect(educationPanel.locator('text=titulación es obligatoria')).toBeVisible();
  });
});
