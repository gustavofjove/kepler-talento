import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';
import {
  addValue,
  chip,
  chips,
  offered,
  openInput,
  removeValue,
  setLevel,
} from './support/catalog-picker';

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

    // KTL-27: a value is saved at once at the lowest active level, without opening the
    // editor; closing the editor without a choice keeps that level.
    await (await openInput(page, 'candidate-language')).fill('Alem');
    await page.getByRole('listbox').getByRole('option', { name: 'Alemán', exact: true }).click();
    await expect(page.getByTestId('candidate-language-editor')).toHaveCount(0);
    await expect(chips(page, 'candidate-language')).toHaveCount(2);
    await expect(chip(page, 'candidate-language', 'Alemán')).not.toHaveAttribute('data-status');
    // The API lists active values only by default; the lowest is the first by sort order.
    const levels = (await (
      await page.request.get('/api/catalogs/language_level', {
        headers: authorizationHeaders(page),
      })
    ).json()) as { nameEs: string; sortOrder: number }[];
    const lowest = [...levels].sort((a, b) => a.sortOrder - b.sortOrder)[0].nameEs;
    expect((await stored()).find((entry) => entry.language === 'Alemán')?.level).toBe(lowest);
    await chip(page, 'candidate-language', 'Alemán').click();
    await expect(page.getByTestId('candidate-language-editor')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('candidate-language-editor')).toHaveCount(0);
    await expect(chip(page, 'candidate-language', 'Alemán')).toContainText(lowest);
    await removeValue(page, 'candidate-language', 'Alemán');
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

  test('stacks the sections and shares the family rows with search (KTL-27)', async ({ page }) => {
    const suffix = Date.now().toString();
    await createCandidate(page, `Filas${suffix}`, 'Test');
    const editUrl = page.url();

    const families = async () =>
      page
        .getByTestId('candidate-competencies')
        .locator('.catalog-family-row')
        .evaluateAll((rows) => rows.map((row) => row.getAttribute('data-family')));
    const labelWidths = async () =>
      page
        .locator('.catalog-family-row .catalog-picker-label')
        .evaluateAll((labels) => labels.map((label) => label.getBoundingClientRect().width));
    // One full-width section per row: same left edge and width, each below the previous one.
    const expectStacked = async (testIds: string[]) => {
      const boxes = [];
      for (const testId of testIds) {
        boxes.push(
          await page.getByTestId(testId).evaluate((element) => {
            const { x, y, width, height } = element.closest('.panel')!.getBoundingClientRect();
            return { x, y, width, height };
          }),
        );
      }
      for (let index = 1; index < boxes.length; index++) {
        expect(boxes[index].y).toBeGreaterThan(boxes[index - 1].y + boxes[index - 1].height - 1);
        expect(Math.abs(boxes[index].x - boxes[0].x)).toBeLessThanOrEqual(1);
        expect(Math.abs(boxes[index].width - boxes[0].width)).toBeLessThanOrEqual(1);
      }
    };
    const sections = [
      'candidate-competencies',
      'candidate-education',
      'candidate-experience',
      'candidate-notes',
      'candidate-documents',
    ];

    await expect(page.getByTestId('candidate-skill-add')).toBeEnabled();
    expect(await families()).toEqual(['skill', 'language', 'program', 'tag']);
    await expectStacked(sections);
    const candidateWidths = await labelWidths();

    await page.goto('/app/search');
    await expect(page.getByTestId('search-skill-add')).toBeEnabled();
    const searchRows = await page
      .locator('.catalog-family-row')
      .evaluateAll((rows) => rows.map((row) => row.getAttribute('data-family')));
    expect(searchRows).toEqual(['skill', 'language', 'program', 'tag']);
    expect(await labelWidths()).toEqual(candidateWidths);

    await page.goto(editUrl.replace(/\/edit$/, ''));
    await expect(page.getByTestId('candidate-competencies')).toBeVisible();
    expect(await families()).toEqual(['skill', 'language', 'program', 'tag']);
    await expectStacked(sections);

    await page.setViewportSize({ width: 390, height: 844 });
    for (const url of [editUrl, editUrl.replace(/\/edit$/, '')]) {
      await page.goto(url);
      await expect(page.getByTestId('candidate-competencies')).toBeVisible();
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );
      expect(overflow).toBeLessThanOrEqual(1);
    }
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
