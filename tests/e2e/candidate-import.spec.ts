import { request as playwrightRequest } from '@playwright/test';
import { expect, test } from './fixtures';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

/**
 * KTL-17: the import now really creates candidates. Upload a file with deliberate row failures,
 * read the report, upload the corrected file, commit, and find the candidates in the list.
 *
 * Scanning goes through the real ClamAV container, so the state waits are generous. No selector
 * depends on Spanish copy: the page exposes state and counts through data-testid and data-state.
 */
test.describe('Candidate import', () => {
  test('rejects broken rows, then commits a corrected file into real candidates', async ({
    page,
    baseURL,
  }) => {
    test.setTimeout(240_000);
    const suffix = Date.now().toString();
    const first = `Importada${suffix}`;
    const header = 'first_name,last_name,email,status,languages';
    const broken = [
      header,
      `${first},Uno,uno.${suffix}@example.test,available,Inglés:B2`,
      `,Dos,dos.${suffix}@example.test,,`,
      `${first},Tres,no-es-un-correo,,`,
    ].join('\n');
    const corrected = [
      header,
      `${first},Uno,uno.${suffix}@example.test,available,Inglés:B2`,
      `${first},Dos,dos.${suffix}@example.test,,`,
    ].join('\n');

    try {
      await runJourney();
    } finally {
      await deactivateImported(baseURL ?? 'http://127.0.0.1:4300', suffix);
    }

    async function runJourney(): Promise<void> {
      await page.goto('/app/admin/import');

      await page.getByTestId('import-file').setInputFiles({
        name: `broken-${suffix}.csv`,
        mimeType: 'text/csv',
        buffer: Buffer.from(broken, 'utf-8'),
      });
      await page.getByTestId('import-validate').click();
      await expect(page.getByTestId('import-state')).toHaveAttribute('data-state', 'validated', {
        timeout: 120_000,
      });
      await expect(page.getByTestId('import-count-rows')).toHaveText('3');
      await expect(page.getByTestId('import-count-rejected')).toHaveText('2');
      await expect(
        page.getByTestId('import-row-report').locator('tr[data-outcome="rejected"]'),
      ).toHaveCount(2);
      await expect(page.getByTestId('import-commit')).toBeDisabled();

      await page.getByTestId('import-file').setInputFiles({
        name: `corrected-${suffix}.csv`,
        mimeType: 'text/csv',
        buffer: Buffer.from(corrected, 'utf-8'),
      });
      await page.getByTestId('import-validate').click();
      await expect(page.getByTestId('import-count-rejected')).toHaveText('0', { timeout: 120_000 });
      await expect(page.getByTestId('import-state')).toHaveAttribute('data-state', 'validated');
      await expect(page.getByTestId('import-commit')).toBeEnabled();

      await page.getByTestId('import-commit').click();
      await expect(page.getByTestId('import-state')).toHaveAttribute('data-state', 'committed', {
        timeout: 120_000,
      });
      await expect(page.getByTestId('import-count-loaded')).toHaveText('2');
      await expect(page.getByTestId('import-history')).toBeVisible();

      await page.goto('/app/candidates');
      await page.fill('input[name="text"]', first);
      await expect(page.locator('tbody tr')).toHaveCount(2, { timeout: 15_000 });
    }
  });
});

/**
 * Restores the shared database to its seed shape. Candidates are never deleted — the API has no
 * such verb — so the imported ones are logically removed, which keeps them out of every other
 * spec's active list.
 */
async function deactivateImported(baseURL: string, suffix: string): Promise<void> {
  const bootstrap = await playwrightRequest.newContext({ baseURL });
  const tokenResponse = await bootstrap.post('/api/dev/token', {
    data: {
      subject: 'dev-admin-oid',
      displayName: 'Administrador local',
      email: 'admin@kepler-talento.local',
    },
  });
  const { accessToken } = (await tokenResponse.json()) as { accessToken: string };
  await bootstrap.dispose();
  const api = await playwrightRequest.newContext({
    baseURL,
    extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` },
  });
  try {
    const response = await api.get('/api/candidates');
    const candidates = (await response.json()) as {
      id: string;
      email: string;
      isActive: boolean;
      version: number;
    }[];
    for (const candidate of candidates.filter(
      (item) => item.isActive && item.email.endsWith(`.${suffix}@example.test`),
    )) {
      await api.put(`/api/candidates/${candidate.id}/active`, {
        data: { isActive: false, version: candidate.version },
      });
    }
  } finally {
    await api.dispose();
  }
}
