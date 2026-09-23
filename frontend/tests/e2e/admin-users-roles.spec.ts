import { expect, test } from './fixtures';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

test.describe('User and role administration', () => {
  test('manages lifecycle and permissions without deleting identity records', async ({ page }) => {
    test.setTimeout(60_000);
    const suffix = Date.now().toString();
    const email = `e2e.identity.${suffix}@example.test`;
    const roleName = `e2e_role_${suffix}`;
    const roleLabel = `E2E role ${suffix}`;

    await page.goto('/app/admin/users');
    await page.getByTestId('user-display-name').fill(`E2E user ${suffix}`);
    await page.getByTestId('user-email').fill(email);
    await page.getByTestId('user-role').selectOption('readonly');
    await page.getByTestId('create-user').click();

    const userRow = page.locator('tbody tr').filter({ hasText: email });
    await expect(userRow).toBeVisible();
    const userRole = userRow.locator('select');
    await userRole.selectOption('rrhh_user');
    await expect(userRole).toHaveValue('rrhh_user');

    await userRow.locator('button').click();
    await page.getByTestId('confirm-accept').click();
    await expect(userRow.locator('span.badge')).toHaveText(/Inactivo/i);

    await userRow.locator('button').click();
    await page.getByTestId('confirm-accept').click();
    await expect(userRow.locator('span.badge')).toHaveText(/Activo/i);

    await page.goto('/app/admin/roles');
    await page.getByTestId('role-name').fill(roleName);
    await page.getByTestId('role-label').fill(roleLabel);
    await page.getByTestId('create-role').click();

    const roleCard = page.locator('article').filter({ hasText: roleName });
    await expect(roleCard).toBeVisible();
    const updatedRoleLabel = `${roleLabel} updated`;
    await page.getByTestId(`role-label-${roleName}`).fill(updatedRoleLabel);
    await page.getByTestId(`role-label-save-${roleName}`).click();
    await expect(roleCard.getByRole('heading')).toHaveText(updatedRoleLabel);
    const writePermission = roleCard.getByRole('checkbox', { name: 'candidates.update' });
    await writePermission.click();
    await expect(writePermission).toBeChecked();

    await roleCard.getByTestId(/role-active-/).click();
    await page.getByTestId('confirm-accept').click();
    await expect(page.getByTestId(`role-status-${roleName}`)).toHaveAttribute(
      'data-status',
      'inactive',
    );

    // Test records remain for audit but finish without active access.
    await page.goto('/app/admin/users');
    const restoredUserRow = page.locator('tbody tr').filter({ hasText: email });
    await restoredUserRow.locator('button').click();
    await page.getByTestId('confirm-accept').click();
    await expect(restoredUserRow.locator('span.badge')).toHaveText(/Inactivo/i);
  });
});
