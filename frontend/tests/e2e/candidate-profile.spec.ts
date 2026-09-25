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

  test('shows the CV beside the sections on wide screens and after them otherwise (KTL-28)', async ({
    page,
  }) => {
    test.setTimeout(90_000);
    await page.setViewportSize({ width: 1920, height: 1080 });

    // Without a preview the shell keeps its default cap.
    await page.goto('/app/candidates');
    const mainWidth = await page
      .locator('main')
      .evaluate((element) => element.getBoundingClientRect().width);
    expect(mainWidth).toBeLessThanOrEqual(1440);

    await createCandidate(page, `Vista${Date.now()}`, 'Test');
    const editUrl = page.url();
    const detailUrl = editUrl.replace(/\/edit$/, '');
    const documents = page.getByTestId('candidate-documents');
    await documents.getByTestId('document-file').setInputFiles({
      name: 'vista-cv.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from(
        '%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF',
        'utf-8',
      ),
    });
    await documents.getByTestId('document-upload').click();
    // The preview follows the scan and loads the PDF once it is clean.
    await expect(page.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/, {
      timeout: 25_000,
    });

    const box = async (locator: import('@playwright/test').Locator) =>
      (await locator.boundingBox())!;
    const mainData = () => page.locator('.page-split__main > :first-child');
    const preview = page.getByTestId('candidate-cv-preview');

    for (const url of [editUrl, detailUrl]) {
      await page.goto(url);
      await expect(page.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/);
      await page.evaluate(() => window.scrollTo(0, 0));
      const data = await box(mainData());
      const side = await box(preview);
      expect(side.x).toBeGreaterThanOrEqual(data.x + data.width);
      expect(side.y).toBeLessThan(data.y + data.height);

      // Sticky: still fully visible below the header once the sections scroll to Documentos.
      await page.getByTestId('candidate-documents').scrollIntoViewIfNeeded();
      const header = await box(page.locator('.shell header'));
      const stuck = await box(preview);
      expect(stuck.y).toBeGreaterThanOrEqual(header.y + header.height - 1);
      expect(stuck.y + stuck.height).toBeLessThanOrEqual(1080 + 1);
    }

    // The main data form keeps two columns beside the preview, and a picker opens on screen.
    await page.goto(editUrl);
    await expect(preview).toBeVisible();
    const first = await box(page.locator('input[name="firstName"]'));
    const last = await box(page.locator('input[name="lastName"]'));
    expect(Math.abs(first.y - last.y)).toBeLessThanOrEqual(1);
    await (await openInput(page, 'candidate-skill')).fill('a');
    const listbox = await box(page.getByRole('listbox'));
    expect(listbox.x).toBeGreaterThanOrEqual(0);
    expect(listbox.x + listbox.width).toBeLessThanOrEqual(1920);
    await page.keyboard.press('Escape');

    // At 1366×768 the content area is under the threshold: the preview follows Documentos.
    await page.setViewportSize({ width: 1366, height: 768 });
    for (const url of [editUrl, detailUrl]) {
      await page.goto(url);
      await expect(preview).toBeVisible();
      const last = await box(page.getByTestId('candidate-documents'));
      expect((await box(preview)).y).toBeGreaterThanOrEqual(last.y + last.height);
    }

    await page.setViewportSize({ width: 390, height: 844 });
    for (const url of [editUrl, detailUrl]) {
      await page.goto(url);
      await expect(preview).toBeVisible();
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
