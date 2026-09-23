import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';
import { signInAs } from './support/auth';
import { ensureSearchCandidate } from './support/seed-candidate';

/**
 * KTL-14: presets are one shared library, curated in Admin › Presets and applied in Búsqueda.
 * Selectors bind to test ids, field names and roles - never to rendered Spanish copy.
 */

test.use({ storageState: authFile('rrhh_admin') });

const PRESETS = '/app/admin/presets';

const uniqueName = (label: string) => `Preset E2E ${label} ${Date.now()}`;

const pickerOption = (page: Page, name: string) =>
  page.locator('select[name="selectedPreset"] option', { hasText: name });

/** Narrows the administration list to one preset and returns its row. */
async function findRow(page: Page, name: string) {
  await page.fill('input[name="presetFilter"]', name);
  const row = page.getByTestId('preset-row');
  await expect(row).toHaveCount(1);
  await expect(row.getByTestId('preset-row-name')).toHaveText(name);
  return row;
}

/** Creates a preset through the administration section and leaves the page on the list. */
async function createPreset(page: Page, name: string): Promise<void> {
  await page.goto(PRESETS);
  await page.getByTestId('preset-new').click();
  await expect(page).toHaveURL(/\/app\/admin\/presets\/new$/);

  await page.getByTestId('preset-name').fill(name);
  await page.fill('input[name="text"]', 'Laura');
  await page.selectOption('select[name="hasCv"]', 'yes');
  await page.getByTestId('preset-save').click();

  await expect(page).toHaveURL(/\/app\/admin\/presets$/);
  await findRow(page, name);
}

/** Deletes a preset from the list, confirming the dialog, and waits until it is gone. */
async function deletePreset(page: Page, name: string): Promise<void> {
  await page.goto(PRESETS);
  await page.fill('input[name="presetFilter"]', name);
  const row = page.getByTestId('preset-row');
  await expect(row).toHaveCount(1);

  await row.getByTestId('preset-delete').click();
  await expect(page.getByTestId('confirm-dialog')).toBeVisible();
  await page.getByTestId('confirm-accept').click();

  await expect(row).toHaveCount(0);
}

test.describe('Shared search presets', () => {
  test.beforeEach(async ({ request }) => {
    await ensureSearchCandidate(request);
  });

  test('an administrator creates, edits and deletes a preset in Admin', async ({ page }) => {
    const name = uniqueName('CRUD');
    await createPreset(page, name);

    // Its criteria are viewed in a dialog opened from beside the name, through the same summary
    // the search page uses, and the dialog closes back to the list.
    const row = await findRow(page, name);
    await row.getByTestId('preset-view').click();
    const dialog = page.getByTestId('criteria-dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByTestId('filters-summary')).toContainText('Laura');
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);

    await row.getByTestId('preset-edit').click();
    await expect(page.getByTestId('preset-name')).toHaveValue(name);
    await expect(page.locator('input[name="text"]')).toHaveValue('Laura');
    const renamed = `${name} editado`;
    await page.getByTestId('preset-name').fill(renamed);
    // KTL-23: the trail shows the stored name, not the draft.
    const breadcrumb = page.getByTestId('breadcrumb');
    await expect(breadcrumb.locator('[aria-current="page"]')).toHaveText(name);
    await page.getByTestId('preset-save').click();
    await expect(page).toHaveURL(/\/app\/admin\/presets$/);
    const renamedRow = await findRow(page, renamed);

    // The breadcrumb leads back to the library without saving.
    await renamedRow.getByTestId('preset-edit').click();
    await expect(breadcrumb.locator('[aria-current="page"]')).toHaveText(renamed);
    await breadcrumb.getByTestId('breadcrumb-presets').click();
    await expect(page).toHaveURL(/\/app\/admin\/presets$/);
    await expect(page.getByTestId('breadcrumb')).toHaveCount(0);

    await deletePreset(page, renamed);
  });

  test('a shared preset is applied in Búsqueda and survives a reload', async ({ page }) => {
    const name = uniqueName('Aplicar');
    await createPreset(page, name);

    await page.goto('/app/search');
    // Management happens in Admin: the search page offers no way to save or name a preset.
    await expect(page.locator('input[name="presetName"]')).toHaveCount(0);
    await expect(pickerOption(page, name)).toHaveCount(1);

    // A full reload with fresh browser state: only the server could still know the preset.
    await page.context().clearCookies();
    await page.reload();
    await expect(pickerOption(page, name)).toHaveCount(1);

    await page.fill('input[name="text"]', 'nadie-existe-xyz');
    await page.locator('form button[type="submit"]').click();
    await expect(page.getByTestId('search-empty')).toBeVisible();

    // Selecting a preset applies it right away; there is no separate load button.
    await page.selectOption('select[name="selectedPreset"]', { label: name });
    await expect(page.getByText('Laura Garcia')).toBeVisible();
    await page.getByTestId('toggle-filters').click();
    await expect(page.locator('input[name="text"]')).toHaveValue('Laura');
    await expect(page.locator('select[name="hasCv"]')).toHaveValue('yes');

    await page.getByTestId('manage-presets-link').click();
    await expect(page).toHaveURL(/\/app\/admin\/presets$/);

    await deletePreset(page, name);
  });

  test('a user without presets.manage can search but cannot reach preset administration', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'readonly');

    await page.goto('/app/search');
    await expect(page.locator('select[name="selectedPreset"]')).toBeVisible();
    await expect(page.getByTestId('manage-presets-link')).toHaveCount(0);

    // The route guard refuses it by URL; hiding the link is not the control.
    await page.goto(PRESETS);
    await expect(page).toHaveURL(/\/app$/);

    await context.close();
  });

  test('retained legacy browser presets are ignored and left untouched', async ({ page }) => {
    await page.goto('/app/search');
    // Written by hand because the application can no longer produce it: the key is gone from
    // the source. What must hold is that its presence changes nothing.
    const retained = JSON.stringify([{ id: 'legacy-1', name: 'Preset Antiguo', filters: {} }]);
    await page.evaluate((value) => localStorage.setItem('rrhh.search.presets.v1', value), retained);
    await page.reload();

    await expect(pickerOption(page, 'Preset Antiguo')).toHaveCount(0);
    // Ignored, not confiscated: it is not this application's data to delete.
    expect(await page.evaluate(() => localStorage.getItem('rrhh.search.presets.v1'))).toBe(
      retained,
    );

    await page.evaluate(() => localStorage.removeItem('rrhh.search.presets.v1'));
  });
});
