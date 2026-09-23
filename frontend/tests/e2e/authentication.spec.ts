import { expect, test } from './fixtures';
import { signInAs } from './support/auth';

test.describe('Authentication', () => {
  test('evicts superseded identity administration storage on first load', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('rrhh-demo-profile', 'legacy-profile');
      localStorage.setItem('rrhh-admin-users', 'legacy-users');
      localStorage.setItem('rrhh-admin-roles', 'legacy-roles');
    });

    await signInAs(page, 'rrhh_admin');

    const values = await page.evaluate(() => ({
      profile: localStorage.getItem('rrhh-demo-profile'),
      users: localStorage.getItem('rrhh-admin-users'),
      roles: localStorage.getItem('rrhh-admin-roles'),
    }));
    expect(values).toEqual({ profile: null, users: null, roles: null });
  });

  test('signs in through the development issuer and signs out of the protected shell', async ({
    page,
  }) => {
    await signInAs(page, 'rrhh_admin');
    await expect(page).toHaveURL(/\/app$/);

    await page.getByTestId('sign-out').click();
    await expect(page).toHaveURL(/\/login$/);

    const signedOutPage = await page.context().newPage();
    await signedOutPage.goto('/app/candidates');
    await expect(signedOutPage).toHaveURL(/\/login$/);
  });
});
