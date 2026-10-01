import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';

test.use({ storageState: authFile('rrhh_admin') });

test('records, reconfirms, amends and undoes an availability check', async ({ page }) => {
  const marker = Date.now();
  const headers = authorizationHeaders(page);
  const created = await page.request.post('/api/candidates', {
    headers,
    data: {
      firstName: `Disponibilidad${marker}`,
      lastName: 'Prueba',
      phone: '',
      email: `availability.${marker}@example.test`,
      location: '',
      province: '',
      country: 'España',
      source: 'Prueba',
      notes: '',
      receivedAt: '',
      consentAt: '',
      reviewDueAt: '',
    },
  });
  expect(created.ok(), await created.text()).toBe(true);
  const candidate = (await created.json()) as { id: string; version: number };
  await page.goto(`/app/candidates/${candidate.id}`);

  await expect(page.getByTestId('candidate-availability-line')).toContainText('Sin comprobar');
  await page.getByTestId('availability-change').click();
  await page.getByTestId('availability-state-unavailable').check();
  await page.getByTestId('availability-until').fill('2027-01-15');
  await page.getByTestId('availability-save').click();
  await expect(page.getByTestId('candidate-availability-line')).toContainText('No disponible');
  await expect(page.getByTestId('availability-reconfirm')).toBeVisible();

  await page.getByTestId('availability-reconfirm').click();
  await expect(page.getByTestId('candidate-availability-status')).toContainText('registrada');
  await page.getByTestId('availability-undo').click();
  await expect(page.getByTestId('candidate-availability-status')).toContainText('restaurada');

  await page.getByTestId('availability-change').click();
  await page.getByTestId('availability-checked-on').fill('2026-03-12');
  await page.getByTestId('availability-until').fill('2027-01-15');
  await page.getByTestId('availability-save').click();
  await expect(page.getByTestId('availability-form')).toHaveCount(0);
  const read = await page.request.get(`/api/candidates/${candidate.id}`, { headers });
  expect(
    ((await read.json()) as { availability: { checkedOn: string } }).availability.checkedOn,
  ).toBe('2026-03-12');

  await page.goto('/app/candidates');
  await page.locator('select[name="availability"]').selectOption('unavailable');
  await expect(page).toHaveURL(/availability=unavailable/);
  await page.getByTestId('candidate-sort-availabilityCheckedOn').click();
  await expect(page).toHaveURL(/sort=availabilityCheckedOn/);
});

test('marks an expired until date and searches from a check date', async ({ page }) => {
  const marker = Date.now();
  const headers = authorizationHeaders(page);
  const created = await page.request.post('/api/candidates', {
    headers,
    data: {
      firstName: `Vencido${marker}`,
      lastName: 'Prueba',
      phone: '',
      email: `lapsed.${marker}@example.test`,
      location: '',
      province: '',
      country: 'España',
      source: 'Prueba',
      notes: '',
      receivedAt: '',
      consentAt: '',
      reviewDueAt: '',
    },
  });
  expect(created.ok(), await created.text()).toBe(true);
  const candidate = (await created.json()) as { id: string; version: number };
  const checked = await page.request.put(`/api/candidates/${candidate.id}/availability`, {
    headers,
    data: {
      state: 'unavailable',
      checkedOn: '2026-03-01',
      until: '2026-03-02',
      version: candidate.version,
    },
  });
  expect(checked.ok(), await checked.text()).toBe(true);

  await page.goto(`/app/candidates/${candidate.id}`);
  await expect(page.getByTestId('candidate-availability-line')).toContainText('vencido');
  await expect(page.getByTestId('availability-reconfirm')).toHaveCount(0);
  await page.goto('/app/search');
  await page.getByTestId('availability-checked-from').fill('2026-03-01');
  await expect(page.getByTestId('availability-checked-from')).toHaveValue('2026-03-01');
  await page.getByRole('button', { name: 'Buscar' }).click();
  await expect(page.getByTestId('search-total')).toBeVisible();
});
