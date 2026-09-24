import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { offered } from './support/catalog-picker';

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
    await page.fill('input[name="newNameEs"]', initialName);
    await page.fill('input[name="newCode"]', `qa_${suffix}`);
    await page.click('button:has-text("Añadir")');

    const row = page.locator(`tbody tr:has-text("${initialName}")`);
    await expect(row).toBeVisible();

    await row.locator('button:has-text("Editar")').click();
    await page.locator('input[name="editNameEs"]').fill(editedName);
    await page.locator('button:has-text("Guardar")').click();

    const editedRow = page.locator(`tbody tr:has-text("${editedName}")`);
    await expect(editedRow).toBeVisible();

    await editedRow.locator('button:has-text("Bajar")').click();
    await expect(editedRow).toBeVisible();

    // Deactivation is the only retirement path, and it is confirmed.
    await editedRow.locator('button:has-text("Desactivar")').click();
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
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+\/edit$/);
    const candidateEditUrl = page.url();
    await expect(page.getByTestId('candidate-language-add')).toBeEnabled();
    await expect(await offered(page, 'candidate-language', 'Ingl')).toHaveCount(1);
    await expect(await offered(page, 'candidate-language', editedName)).toHaveCount(0);

    // Reactivating puts it back in its family position.
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');
    await page
      .locator(`tbody tr:has-text("${editedName}")`)
      .locator('button:has-text("Activar")')
      .click();
    await expect(
      page.locator(`tbody tr:has-text("${editedName}")`).locator('span.badge'),
    ).toContainText('Activo');

    // ...and the candidate picker offers it again by typing its name.
    await page.goto(candidateEditUrl);
    await expect(page.getByTestId('candidate-language-add')).toBeEnabled();
    const back = await offered(page, 'candidate-language', editedName.toLowerCase());
    await expect(back).toHaveCount(1);
    await expect(back).toHaveText(editedName);

    // Leave no test candidate behind: retire it through the product's logical path.
    await page.goto(candidateEditUrl.replace(/\/edit$/, ''));
    await page.locator('button.button.danger').click();
    await page.getByTestId('confirm-accept').click();
  });

  test('edit mode offers only save and cancel and locks the other rows', async ({ page }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');

    const rows = page.locator('tbody tr');
    const firstRow = rows.nth(0);
    const originalText = await firstRow.locator('td').nth(2).innerText();
    const heightBefore = (await firstRow.boundingBox())?.height;

    await firstRow.getByTestId('catalog-edit').click();

    await expect(firstRow.getByRole('button')).toHaveCount(2);
    await expect(firstRow.getByTestId('catalog-edit-save')).toBeVisible();
    await expect(firstRow.getByTestId('catalog-edit-cancel')).toBeVisible();
    for (const button of await rows.nth(1).getByRole('button').all()) {
      await expect(button).toBeDisabled();
    }
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
    await page.fill('input[name="newNameEs"]', 'ingles');
    await page.click('button:has-text("Añadir")');

    await expect(page.locator('text=Ya existe un valor con ese nombre.')).toBeVisible();
    await expect(page.locator('tbody tr:has-text("Inglés")')).toHaveCount(1);
  });

  test('offers no physical delete action', async ({ page }) => {
    await page.goto('/app/catalogs');
    await page.selectOption('select[name="family"]', 'language');

    await expect(page.locator('tbody button:has-text("Eliminar")')).toHaveCount(0);
  });
});
