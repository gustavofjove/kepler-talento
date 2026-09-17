import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders, signInAs } from './support/auth';

test.use({ storageState: authFile('rrhh_admin') });

/**
 * KTL-19: a candidate write and a detail read both land in the audit trail naming the acting
 * user, and the auditor sees them in Auditoría. No selector depends on Spanish copy: rows expose
 * their event type and actor kind through data attributes.
 */
test.describe('Audit trail', () => {
  test('records a write and a detail read and shows both to the auditor', async ({
    page,
    browser,
    baseURL,
  }) => {
    const suffix = Date.now().toString();
    const created = await page.request.post('/api/candidates', {
      headers: authorizationHeaders(page),
      data: {
        firstName: `Auditada${suffix}`,
        lastName: 'Trazas',
        phone: '',
        email: `auditada.${suffix}@example.test`,
        location: '',
        province: '',
        country: 'España',
        availability: 'Inmediata',
        status: 'new',
        source: 'Email',
        notes: '',
      },
    });
    expect(created.ok()).toBe(true);
    const candidate = (await created.json()) as { id: string; version: number };
    const compactId = candidate.id.replaceAll('-', '');

    try {
      // The detail read goes through the UI, the way a recruiter opens a record.
      const detail = page.waitForResponse(
        (response) =>
          response.url().endsWith(`/api/candidates/${candidate.id}`) &&
          response.request().method() === 'GET',
      );
      await page.goto(`/app/candidates/${candidate.id}`);
      expect((await detail).ok()).toBe(true);

      const context = await browser.newContext({ baseURL });
      const auditor = await context.newPage();
      try {
        await signInAs(auditor, 'system_admin');
        await auditor.goto('/app/admin/audit');
        await auditor.getByTestId('audit-filter-subject').fill(candidate.id);
        await auditor.getByTestId('audit-apply').click();

        const table = auditor.getByTestId('audit-table');
        const createdRow = table.locator('tr[data-event-type="candidate.created"]');
        const readRow = table.locator('tr[data-event-type="candidate.read"]');
        await expect(createdRow).toHaveCount(1, { timeout: 15_000 });
        await expect(readRow.first()).toBeVisible();

        for (const row of [createdRow, readRow.first()]) {
          await expect(row).toHaveAttribute('data-actor-kind', 'user');
          await expect(row).toContainText(compactId);
          // Resolved through the users endpoint: the auditor holds users.manage.
          await expect(row.getByTestId('audit-actor')).toHaveText('Administrador local');
        }
        // Identifiers and codes only: the candidate's name never reaches the trail.
        await expect(table).not.toContainText(`Auditada${suffix}`);
      } finally {
        await context.close();
      }
    } finally {
      await page.request.put(`/api/candidates/${candidate.id}/active`, {
        headers: authorizationHeaders(page),
        data: { isActive: false, version: candidate.version },
      });
    }
  });

  test('keeps Auditoría out of reach of a role without audit.read', async ({ page }) => {
    await page.goto('/app/admin/audit');
    await expect(page).toHaveURL(/\/app$/);

    const refused = await page.request.get('/api/audit/events', {
      headers: authorizationHeaders(page),
    });
    expect(refused.status()).toBe(403);
  });
});
