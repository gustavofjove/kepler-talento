import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Catalog management CRUD', () => {
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

    // A deactivated value is no longer offered on a candidate form.
    await page.goto('/app/candidates/new');
    await page.fill('input[name="firstName"]', `Cat${suffix}`);
    await page.fill('input[name="lastName"]', 'Test');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/\/app\/candidates\/[\w-]+$/);
    const languageSelect = page
      .getByTestId('candidate-languages')
      .locator('select[name="language"]');
    await expect(languageSelect.locator('option', { hasText: 'Inglés' }).first()).toBeAttached();
    await expect(languageSelect.locator(`option:text-is("${editedName}")`)).toHaveCount(0);

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
