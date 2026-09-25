import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';
import { createCandidate, editPanel, finishPanel, savePanel } from './support/candidate-panels';
import { addValue, chip, chips, removeValue } from './support/catalog-picker';

test.use({ storageState: authFile('rrhh_admin') });

/**
 * The candidate journeys that KTL-8 moved onto the API, exercised through the same-origin
 * `/api` route rather than against browser storage.
 *
 * The create / edit / logical delete / restore path is covered by `candidate-crud.spec.ts`
 * and keeps passing unchanged, which is the point: the user-visible behaviour did not
 * move. What is new here is the relation and document collections, which are now
 * server-owned, and the conflict a stale editor now gets.
 */
test.describe('Candidate API cutover', () => {
  // Since KTL-29 creation opens the candidate page, where each section is a panel edited in
  // place and saved on its own.
  const create = createCandidate;

  test('adds a relation that survives a reload', async ({ page }) => {
    const firstName = `Rel${Date.now()}`;
    await create(page, firstName);

    await editPanel(page, 'competencies');
    await addValue(page, 'candidate-language', 'Inglés', 'B1');
    await savePanel(page, 'competencies');

    // A full reload proves the relation is stored server-side: nothing about this
    // candidate survives in the browser.
    await page.reload();
    await expect(chips(page, 'candidate-language')).toHaveCount(1);
    await expect(chip(page, 'candidate-language', 'Inglés')).toContainText('B1');
  });

  test('removes a relation', async ({ page }) => {
    const firstName = `Del${Date.now()}`;
    await create(page, firstName);

    await editPanel(page, 'competencies');
    await addValue(page, 'candidate-language', 'Inglés', 'B1');
    await savePanel(page, 'competencies');

    await editPanel(page, 'competencies');
    await removeValue(page, 'candidate-language', 'Inglés');
    await savePanel(page, 'competencies');

    await page.reload();
    await expect(page.getByTestId('candidate-languages')).toBeVisible();
    await expect(chips(page, 'candidate-language')).toHaveCount(0);
  });

  test('records document metadata that survives a reload', async ({ page }) => {
    const firstName = `Doc${Date.now()}`;
    await create(page, firstName);

    await editPanel(page, 'documents');
    const documents = page.getByTestId('candidate-documents');
    await documents.locator('input[type="file"]').setInputFiles({
      name: 'cv-prueba.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('%PDF-1.4 prueba'),
    });
    await documents.locator('button:has-text("Subir CV")').click();

    await expect(documents.locator('text=cv-prueba.pdf')).toBeVisible();
    await finishPanel(page, 'documents');

    await page.reload();
    await expect(
      page.getByTestId('candidate-documents').locator('text=cv-prueba.pdf'),
    ).toBeVisible();
  });

  test('surfaces a concurrent-edit conflict in Spanish', async ({ page }) => {
    const firstName = `Conf${Date.now()}`;
    const id = await create(page, firstName);

    // A real conflict, not a simulated one. The form loads and caches the version it
    // read; a write that lands in between — standing in for another user — advances the
    // stored version, so the form's submit carries a token the server has moved past.
    await page.goto(`/app/candidates/${id}`);
    await editPanel(page, 'main');
    await expect(page.locator('input[name="firstName"]')).toHaveValue(firstName);

    const headers = authorizationHeaders(page);
    const loaded = await (await page.request.get(`/api/candidates/${id}`, { headers })).json();
    const interloper = await page.request.put(`/api/candidates/${id}`, {
      headers,
      data: { ...loaded, lastName: 'Candidato Adelantado', version: loaded.version },
    });
    expect(interloper.ok()).toBeTruthy();

    await page.fill('input[name="lastName"]', 'Candidato Conflictivo');
    await page.getByTestId('candidate-panel-main-save').click();

    await expect(page.locator('text=El candidato ha cambiado desde que se cargó')).toBeVisible();
    // The panel stays in edit mode with the user's draft rather than closing on stale data.
    await expect(page.getByTestId('candidate-panel-main-save')).toBeVisible();
    await expect(page.locator('input[name="lastName"]')).toHaveValue('Candidato Conflictivo');
    // And the other write is what survived; the stale submit overwrote nothing.
    const stored = await (await page.request.get(`/api/candidates/${id}`, { headers })).json();
    expect(stored.lastName).toBe('Candidato Adelantado');
  });

  test('no candidate data is left in browser storage', async ({ page }) => {
    const firstName = `Storage${Date.now()}`;
    await create(page, firstName);

    const stored = await page.evaluate(() => ({
      superseded: localStorage.getItem('rrhh-candidates'),
      keys: Object.keys(localStorage),
      dump: JSON.stringify(localStorage),
    }));

    // The superseded key is gone, and the candidate's name appears nowhere in storage.
    expect(stored.superseded).toBeNull();
    expect(stored.keys).not.toContain('rrhh-candidates');
    expect(stored.dump).not.toContain(firstName);
  });
});
