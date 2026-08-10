import { expect, test } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('Catalog management CRUD', () => {
  test('creates, edits, toggles, reorders, and deletes a catalog value', async ({ page }) => {
    const suffix = Date.now().toString();
    const initialName = `IdiomaQA${suffix}`;
    const editedName = `IdiomaQAEdit${suffix}`;

    await page.goto('/app/catalogs');

    await page.selectOption('select[name="family"]', 'language');
    await page.fill('input[name="newNameEs"]', initialName);
    await page.fill('input[name="newCode"]', `qa_${suffix}`);
    await page.click('button:has-text("Anadir")');

    const row = page.locator(`tbody tr:has-text("${initialName}")`);
    await expect(row).toBeVisible();

    await row.locator('button:has-text("Editar")').click();
    await page.locator('input[name="editNameEs"]').fill(editedName);
    await page.locator('button:has-text("Guardar")').click();

    const editedRow = page.locator(`tbody tr:has-text("${editedName}")`);
    await expect(editedRow).toBeVisible();

    await editedRow.locator('button:has-text("Desactivar")').click();
    await expect(editedRow.locator('span.badge')).toContainText('Inactivo');

    await editedRow.locator('button:has-text("Activar")').click();
    await expect(editedRow.locator('span.badge')).toContainText('Activo');

    await editedRow.locator('button:has-text("Bajar")').click();
    await expect(editedRow).toBeVisible();

    await editedRow.locator('button:has-text("Eliminar")').click();
    await expect(page.getByTestId('confirm-dialog')).toBeVisible();
    await page.getByTestId('confirm-accept').click();
    await expect(page.locator(`tbody tr:has-text("${editedName}")`)).toHaveCount(0);
  });
});
