import { expect, test } from '@playwright/test';
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
  await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
}

test.describe('Candidate profile enrichment', () => {
  test('adds a language, a skill, and rejects a duplicate language', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Perfil${suffix}`, 'Test');

    const languagesPanel = page.locator('rrhh-candidate-languages');
    await languagesPanel.locator('select[name="language"]').selectOption('Ingles');
    await languagesPanel.locator('select[name="level"]').selectOption('B2');
    await languagesPanel.locator('button:has-text("Anadir idioma")').click();
    await expect(languagesPanel.locator('span.badge:has-text("Ingles")')).toBeVisible();

    await languagesPanel.locator('select[name="language"]').selectOption('Ingles');
    await languagesPanel.locator('select[name="level"]').selectOption('C1');
    await languagesPanel.locator('button:has-text("Anadir idioma")').click();
    await expect(languagesPanel.locator('text=ya tiene este idioma')).toBeVisible();

    const skillsPanel = page.locator('rrhh-candidate-skills');
    await skillsPanel.locator('select[name="skill"]').selectOption('Compras');
    await skillsPanel.locator('select[name="level"]').selectOption('Medio');
    await skillsPanel.locator('button:has-text("Anadir habilidad")').click();
    await expect(skillsPanel.locator('span.badge:has-text("Compras")')).toBeVisible();
  });

  test('rejects experience with an end date before the start date', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Exp${suffix}`, 'Test');

    const experiencePanel = page.locator('rrhh-candidate-experience');
    await experiencePanel.locator('input[name="company"]').fill('Acme');
    await experiencePanel.locator('input[name="position"]').fill('Analista');
    await experiencePanel.locator('select[name="sector"]').selectOption('Servicios');
    await experiencePanel.locator('input[name="startDate"]').fill('2024-06-01');
    await experiencePanel.locator('input[name="endDate"]').fill('2024-01-01');
    await experiencePanel.locator('button:has-text("Anadir experiencia")').click();

    await expect(experiencePanel.locator('text=no puede ser anterior')).toBeVisible();
  });

  test('requires a degree before adding education', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Edu${suffix}`, 'Test');

    const educationPanel = page.locator('rrhh-candidate-education');
    await educationPanel.locator('select[name="educationType"]').selectOption('Grado');
    await educationPanel.locator('select[name="status"]').selectOption('Finalizada');
    await educationPanel.locator('button:has-text("Anadir formacion")').click();

    await expect(educationPanel.locator('text=titulacion es obligatoria')).toBeVisible();
  });
});
