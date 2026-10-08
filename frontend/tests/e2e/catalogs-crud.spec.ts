import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import {
  CANDIDATE_PAGE_URL,
  createCandidate,
  editPanel,
  savePanel,
} from './support/candidate-panels';
import { addValue, chip, offered } from './support/catalog-picker';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Catalog management CRUD', () => {
  test('offers the tag family in the shared catalog editor', async ({ page }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'tag');
    await expect(page.locator('select[name="family"]')).toHaveValue('tag');
    await expect(page.locator('tbody tr').first()).toBeVisible();
    expect(await page.locator('tbody tr').count()).toBeGreaterThanOrEqual(3);
  });

  test('creates, edits, reorders, deactivates, and reactivates a catalog value through the API', async ({
    page,
  }) => {
    const suffix = Date.now().toString();
    const initialName = `IdiomaQA${suffix}`;
    const editedName = `IdiomaQAEdit${suffix}`;

    await page.goto('/app/catalogs');

    await page.selectOption('select[name="family"]', 'language');
    // KTL-41: the add form is hidden until «Nuevo» opens it.
    await page.getByTestId('catalog-new').click();
    await page.fill('input[name="newNameEs"]', initialName);
    await page.fill('input[name="newCode"]', `qa_${suffix}`);
    await page.click('button:has-text("Añadir")');

    const row = page.locator(`tbody tr:has-text("${initialName}")`);
    await expect(row).toBeVisible();

    // KTL-41: the row's pencil opens the inline editor; a row click does nothing.
    await row.locator('td').first().click();
    await expect(page.locator('input[name="editNameEs"]')).toHaveCount(0);
    await row.getByTestId('catalog-edit').click();
    await page.locator('input[name="editNameEs"]').fill(editedName);
    await page.locator('button:has-text("Guardar")').click();

    const editedRow = page.locator(`tbody tr:has-text("${editedName}")`);
    await expect(editedRow).toBeVisible();

    await editedRow.getByTestId('catalog-move-down').click();
    await expect(editedRow).toBeVisible();

    // Deactivation is the only retirement path, and it is confirmed.
    await editedRow.getByTestId('catalog-toggle-active').click();
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.getByTestId('confirm-accept').click();
    await expect(editedRow.locator('span.badge')).toContainText('Inactivo');

    // The value is retired, never deleted: it survives a reload.
    await page.reload();
    await page.selectOption('select[name="family"]', 'language');
    await expect(page.locator(`tbody tr:has-text("${editedName}")`)).toBeVisible();

    // A deactivated value is not offered on a candidate form, whatever is typed.
    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', `Cat${suffix}`);
    await page.fill('input[name="lastName"]', 'Test');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
    const candidateUrl = page.url();
    await editPanel(page, 'competencies');
    await expect(page.getByTestId('candidate-language-add')).toBeEnabled();
    await expect(await offered(page, 'candidate-language', 'Ingl')).toHaveCount(1);
    await expect(await offered(page, 'candidate-language', editedName)).toHaveCount(0);

    // Reactivating puts it back in its family position.
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');
    await page
      .locator(`tbody tr:has-text("${editedName}")`)
      .getByTestId('catalog-toggle-active')
      .click();
    await expect(
      page.locator(`tbody tr:has-text("${editedName}")`).locator('span.badge'),
    ).toContainText('Activo');

    // ...and the candidate picker offers it again by typing its name.
    await page.goto(candidateUrl);
    await editPanel(page, 'competencies');
    await expect(page.getByTestId('candidate-language-add')).toBeEnabled();
    const back = await offered(page, 'candidate-language', editedName.toLowerCase());
    await expect(back).toHaveCount(1);
    await expect(back).toHaveText(editedName);

    // Leave no test candidate behind: retire it through the product's logical path.
    await page.goto(candidateUrl);
    await page.locator('button.button.danger').click();
    await page.getByTestId('confirm-accept').click();
  });

  test('edit mode offers only save, cancel and the colour, and locks the rest', async ({
    page,
  }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');

    const rows = page.locator('tbody tr');
    const firstRow = rows.nth(0);
    const originalText = await firstRow.locator('td').nth(2).innerText();
    const heightBefore = (await firstRow.boundingBox())?.height;

    await firstRow.getByTestId('catalog-edit').click();

    // Save, cancel and, for a family with colours (KTL-41), the colour circle.
    await expect(firstRow.getByRole('button')).toHaveCount(3);
    await expect(firstRow.getByTestId('catalog-color-trigger')).toBeVisible();
    await expect(firstRow.getByTestId('catalog-edit-save')).toBeVisible();
    await expect(firstRow.getByTestId('catalog-edit-cancel')).toBeVisible();
    for (const button of await rows.nth(1).getByRole('button').all()) {
      await expect(button).toBeDisabled();
    }
    await expect(page.getByTestId('catalog-new')).toBeDisabled();
    // Entering edit mode must not change the row height.
    expect((await firstRow.boundingBox())?.height).toBe(heightBefore);

    await firstRow.locator('input[name="editNameEs"]').fill('Descartado');
    await firstRow.getByTestId('catalog-edit-cancel').click();

    await expect(firstRow.locator('td').nth(2)).toHaveText(originalText);
    await expect(rows.nth(1).getByRole('button').first()).toBeEnabled();
  });

  test('rejects a duplicate name with the Spanish message', async ({ page }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');

    // Differs from the seeded "Inglés" only by case and accent.
    await page.getByTestId('catalog-new').click();
    await page.fill('input[name="newNameEs"]', 'ingles');
    await page.click('button:has-text("Añadir")');

    await expect(page.locator('text=Ya existe un valor con ese nombre.')).toBeVisible();
    await expect(page.locator('tbody tr:has-text("Inglés")')).toHaveCount(1);
  });

  test('gives a tag a colour that its chips carry on the candidate page (KTL-41)', async ({
    page,
  }) => {
    const suffix = Date.now().toString();
    const tag = `ColorTag${suffix}`;
    const chooseColor = async (color: string) => {
      const dialog = page.getByTestId('catalog-color-dialog');
      await expect(dialog).toBeVisible();
      await dialog.locator(`[data-testid="catalog-color-option"][data-value="${color}"]`).click();
      await expect(dialog).toHaveCount(0);
    };

    // Create the tag in a colour chosen in the add form.
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'tag');
    await page.getByTestId('catalog-new').click();
    await page.fill('input[name="newNameEs"]', tag);
    await page.getByTestId('new-catalog-color-trigger').click();
    await chooseColor('teal');
    await page.locator('form button[type="submit"]').click();
    const row = page.getByTestId('catalog-row').filter({ hasText: tag });
    await expect(row.getByTestId('catalog-color')).toHaveAttribute('data-catalog-color', 'teal');
    // The form closes after «Añadir»; reopened, it starts at the default colour again.
    await expect(page.getByTestId('catalog-create-form')).toHaveCount(0);
    await page.getByTestId('catalog-new').click();
    await expect(page.getByTestId('new-catalog-color-trigger')).toHaveAttribute(
      'data-catalog-color',
      'orange',
    );
    await page.getByTestId('catalog-create-cancel').click();

    // Recolour it in the row editor; nothing changes until the row is saved.
    await row.getByTestId('catalog-edit').click();
    // While edited, the name is an input, so the row is found by its editor instead.
    const editing = page
      .getByTestId('catalog-row')
      .filter({ has: page.getByTestId('catalog-edit-save') });
    await editing.getByTestId('catalog-color-trigger').click();
    await chooseColor('violet');
    await expect(editing.getByTestId('catalog-color-trigger')).toHaveAttribute(
      'data-catalog-color',
      'violet',
    );
    await editing.getByTestId('catalog-edit-save').click();
    await expect(row.getByTestId('catalog-color')).toHaveAttribute('data-catalog-color', 'violet');

    // A family without chips offers no colour.
    await page.selectOption('select[name="family"]', 'sector');
    await page.getByTestId('catalog-new').click();
    await expect(page.getByTestId('catalog-color')).toHaveCount(0);
    await expect(page.getByTestId('new-catalog-color-trigger')).toHaveCount(0);

    // The candidate's chip carries the colour, while editing and once saved.
    const candidateId = await createCandidate(page, `Color${suffix}`);
    await editPanel(page, 'competencies');
    await addValue(page, 'candidate-tag', tag);
    await expect(chip(page, 'candidate-tag', tag)).toHaveAttribute('data-catalog-color', 'violet');
    await savePanel(page, 'competencies');
    await expect(chip(page, 'candidate-tag', tag)).toHaveAttribute('data-catalog-color', 'violet');

    // Leave no test candidate behind: retire it through the product's logical path.
    await page.goto(`/app/candidates/${candidateId}`);
    await page.locator('button.button.danger').click();
    await page.getByTestId('confirm-accept').click();
  });

  test('offers no physical delete action', async ({ page }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');

    await expect(page.locator('tbody button:has-text("Eliminar")')).toHaveCount(0);
  });
});
