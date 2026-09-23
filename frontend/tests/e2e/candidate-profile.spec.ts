import { expect, test } from './fixtures';
import { authFile } from './global-setup';

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
  test('adds a language, a skill, and rejects a duplicate language', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Perfil${suffix}`, 'Test');

    const languagesPanel = page.getByTestId('candidate-languages');
    await languagesPanel.locator('select[name="language"]').selectOption('Inglés');
    await languagesPanel.locator('select[name="level"]').selectOption('B2');
    await languagesPanel.locator('button:has-text("Añadir idioma")').click();
    await expect(languagesPanel.locator('span.badge:has-text("Inglés")')).toBeVisible();

    await languagesPanel.locator('select[name="language"]').selectOption('Inglés');
    await languagesPanel.locator('select[name="level"]').selectOption('C1');
    await languagesPanel.locator('button:has-text("Añadir idioma")').click();
    await expect(languagesPanel.locator('text=ya tiene este idioma')).toBeVisible();

    const skillsPanel = page.getByTestId('candidate-skills');
    await skillsPanel.locator('select[name="skill"]').selectOption('Compras');
    await skillsPanel.locator('select[name="level"]').selectOption('Medio');
    await skillsPanel.locator('button:has-text("Añadir habilidad")').click();
    await expect(skillsPanel.locator('span.badge:has-text("Compras")')).toBeVisible();

    // A section change followed by a core save on the same page must not conflict.
    await page.fill('input[name="lastName"]', 'Test Editado');
    await page.click('form:has(input[name="firstName"]) button[type="submit"]');
    await expect(page.locator('.toast')).toBeVisible();
    await expect(page).toHaveURL(/\/edit$/);

    // The detail page shows what was added, read-only, even for an administrator.
    await page.getByTestId('candidate-edit-view').click();
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
    const detailSkills = page.getByTestId('candidate-skills');
    await expect(detailSkills.locator('span.badge')).toHaveCount(1);
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
