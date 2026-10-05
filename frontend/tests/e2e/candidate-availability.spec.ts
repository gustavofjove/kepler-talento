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

  await expect(page.getByTestId('candidate-availability-chip')).toHaveAttribute(
    'data-tone',
    'neutral',
  );
  await page.getByTestId('availability-change').click();
  await page.getByTestId('availability-state-available').check();
  const checkedOn = page.getByTestId('availability-checked-on');
  const register = page.getByTestId('availability-save');
  const cancel = page.getByTestId('availability-cancel');
  await expect(register).toHaveText('Registrar');
  const wideDate = await checkedOn.boundingBox();
  const wideRegister = await register.boundingBox();
  const wideCancel = await cancel.boundingBox();
  expect(wideDate && wideRegister && wideCancel).toBeTruthy();
  expect(Math.abs(wideDate!.y - wideRegister!.y)).toBeLessThanOrEqual(1);
  expect(Math.abs(wideRegister!.y - wideCancel!.y)).toBeLessThanOrEqual(1);
  expect(wideDate!.height).toBe(wideRegister!.height);

  await page.setViewportSize({ width: 390, height: 844 });
  const narrowDate = await checkedOn.boundingBox();
  const narrowRegister = await register.boundingBox();
  expect(narrowDate && narrowRegister).toBeTruthy();
  expect(narrowRegister!.y).toBeGreaterThan(narrowDate!.y);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await page.setViewportSize({ width: 1280, height: 720 });

  await page.getByTestId('availability-state-unavailable').check();
  await page.setViewportSize({ width: 1366, height: 768 });
  const until = await page.getByTestId('availability-until').boundingBox();
  const unavailableDate = await checkedOn.boundingBox();
  const unavailableRegister = await register.boundingBox();
  const unavailableCancel = await cancel.boundingBox();
  expect(until && unavailableDate && unavailableRegister && unavailableCancel).toBeTruthy();
  expect(Math.abs(until!.y - unavailableDate!.y)).toBeLessThanOrEqual(1);
  expect(Math.abs(unavailableDate!.y - unavailableRegister!.y)).toBeLessThanOrEqual(1);
  expect(Math.abs(unavailableRegister!.y - unavailableCancel!.y)).toBeLessThanOrEqual(1);
  expect(until!.height).toBe(unavailableRegister!.height);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByTestId('availability-until')).toBeVisible();
  await expect(register).toBeVisible();
  await expect(cancel).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.getByTestId('availability-until').fill('2027-01-15');
  await page.getByTestId('availability-save').click();
  await expect(page.getByTestId('candidate-availability-chip')).toHaveAttribute(
    'data-tone',
    'danger',
  );
  await expect(page.getByTestId('candidate-availability-until')).toBeVisible();
  await page.setViewportSize({ width: 1366, height: 768 });
  const valueBox = await page.getByTestId('candidate-availability-line').boundingBox();
  const metaBox = await page.getByTestId('candidate-availability-meta').boundingBox();
  const actionsBox = await page.locator('.candidate-availability__actions').boundingBox();
  expect(valueBox && metaBox && actionsBox).toBeTruthy();
  expect(Math.abs(metaBox!.y - valueBox!.y)).toBeLessThan(12);
  expect(metaBox!.x).toBeGreaterThan(valueBox!.x + valueBox!.width);
  expect(
    actionsBox!.y - Math.max(metaBox!.y + metaBox!.height, valueBox!.y + valueBox!.height),
  ).toBeGreaterThanOrEqual(10);
  const weights = await page.locator('.candidate-availability__summary').evaluate((summary) => {
    const status = summary.querySelector('.badge')!;
    const metadata = summary.querySelector('.candidate-availability__meta')!;
    return [getComputedStyle(status).fontWeight, getComputedStyle(metadata).fontWeight];
  });
  expect(weights).toEqual(['600', '400']);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByTestId('candidate-availability-meta')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
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
  await expect(page.getByTestId('candidate-availability-chip')).toHaveAttribute(
    'data-tone',
    'danger',
  );
  await expect(page.getByTestId('candidate-availability-until')).toBeVisible();
  await expect(page.getByTestId('candidate-availability-lapsed-hint')).toBeVisible();
  await expect(page.getByTestId('availability-reconfirm')).toHaveCount(0);
  await page.goto('/app/search');
  await page.getByTestId('availability-checked-from').fill('2026-03-01');
  await expect(page.getByTestId('availability-checked-from')).toHaveValue('2026-03-01');
  await page.getByRole('button', { name: 'Buscar' }).click();
  await expect(page.getByTestId('search-total')).toBeVisible();
});
