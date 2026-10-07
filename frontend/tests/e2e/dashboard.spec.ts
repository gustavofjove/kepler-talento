import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';

/**
 * KTL-40 «Inicio»: every figure and row leads to where the user acts on it. The spec seeds,
 * through the API, a position with candidates at several stages, a candidate checked available
 * and a shared preset - each carrying a `Date.now()` marker, so the global teardown purges them.
 * Selectors use test ids, roles and hrefs only.
 */
test.use({ storageState: authFile('rrhh_admin') });

interface Seeded {
  marker: number;
  positionId: string;
  positionTitle: string;
  candidates: { id: string; name: string }[];
  presetId: string;
}

/** Tomorrow (UTC): the latest check date the API accepts, so this check sorts first. */
const tomorrowUtc = () => {
  const day = new Date();
  day.setUTCDate(day.getUTCDate() + 1);
  return day.toISOString().slice(0, 10);
};

/**
 * Signing in already lands on `/app`, and the fixture turns `goto` into an in-app navigation,
 * which would keep the page mounted with what it loaded before seeding. A reload starts it afresh.
 */
async function openFresh(page: Page): Promise<void> {
  await page.reload();
  await expect(page.getByTestId('dashboard-positions')).toBeVisible();
}

async function seed(page: Page): Promise<Seeded> {
  const marker = Date.now();
  const headers = authorizationHeaders(page);
  const post = async <T>(url: string, data: unknown): Promise<T> => {
    const response = await page.request.post(url, { headers, data });
    expect(response.ok(), await response.text()).toBe(true);
    return (await response.json()) as T;
  };
  const put = async <T>(url: string, data: unknown): Promise<T> => {
    const response = await page.request.put(url, { headers, data });
    expect(response.ok(), await response.text()).toBe(true);
    return (await response.json()) as T;
  };

  const candidates: Seeded['candidates'] = [];
  for (const suffix of ['A', 'B', 'C']) {
    const firstName = `Inicio${marker}${suffix}`;
    const created = await post<{ id: string; version: number }>('/api/candidates', {
      firstName,
      lastName: 'Panel',
      phone: '',
      email: `inicio.${marker}.${suffix.toLowerCase()}@example.test`,
      location: '',
      province: '',
      country: 'España',
      source: 'Prueba',
      notes: '',
      receivedAt: '',
      consentAt: '',
      reviewDueAt: '',
    });
    candidates.push({ id: created.id, name: `${firstName} Panel` });
    if (suffix === 'A') {
      await put(`/api/candidates/${created.id}/availability`, {
        state: 'available',
        checkedOn: tomorrowUtc(),
        until: null,
        version: created.version,
      });
    }
  }

  const positionTitle = `E2E Inicio ${marker}`;
  const position = await post<{ id: string }>('/api/positions', {
    title: positionTitle,
    description: '',
    location: '',
    requirements: {},
  });
  for (const [index, stage] of ['new', 'interview', 'rejected'].entries()) {
    const link = await post<{ version: number }>(`/api/positions/${position.id}/candidates`, {
      candidateId: candidates[index].id,
    });
    if (stage !== 'new') {
      await put(`/api/positions/${position.id}/candidates/${candidates[index].id}/stage`, {
        stage,
        version: link.version,
      });
    }
  }

  const preset = await post<{ id: string }>('/api/search-presets', {
    name: `E2E Inicio ${marker}`,
    filters: { text: `Inicio${marker}` },
  });
  await post(`/api/search-presets/${preset.id}/use`, {});

  return { marker, positionId: position.id, positionTitle, candidates, presetId: preset.id };
}

test('leads from every summary to the view behind it', async ({ page }) => {
  const seeded = await seed(page);
  await openFresh(page);

  await expect(page.getByTestId('nav-dashboard')).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId('dashboard-actions')).toBeVisible();

  // Open positions: the newest is ours, with one candidate at new, interview and rejected.
  const row = page.getByTestId('dashboard-position-row').first();
  await expect(row.getByRole('link')).toHaveAttribute(
    'href',
    `/app/positions/${seeded.positionId}`,
  );
  const counts = await Promise.all(
    ['new', 'shortlisted', 'interview', 'hired', 'rejected', 'total'].map((stage) =>
      row.getByTestId(`stage-count-${stage}`).textContent(),
    ),
  );
  expect(counts).toEqual(['1', '0', '1', '0', '1', '3']);

  // Recent panels: the one checked available leads, the last one created leads.
  await expect(
    page.getByTestId('dashboard-recent-available-row').first().getByRole('link'),
  ).toHaveAttribute('href', `/app/candidates/${seeded.candidates[0].id}`);
  await expect(
    page.getByTestId('dashboard-recent-added-row').first().getByRole('link'),
  ).toHaveAttribute('href', `/app/candidates/${seeded.candidates[2].id}`);
  await expect(page.getByTestId('dashboard-recent-added-all')).toHaveAttribute(
    'href',
    '/app/candidates?sort=createdAt',
  );

  // The availability figure opens the filtered, check-date-sorted list.
  await page.getByTestId('kpi-available').click();
  await expect(page).toHaveURL(
    /\/app\/candidates\?availability=available&sort=availabilityCheckedOn$/,
  );

  // The whole position row opens the position.
  await page.goto('/app');
  await page.getByTestId('dashboard-position-row').first().getByTestId('stage-count-hired').click();
  await expect(page).toHaveURL(new RegExp(`/app/positions/${seeded.positionId}$`));
});

test('opens the advanced search with a saved search applied, once', async ({ page }) => {
  const seeded = await seed(page);
  await openFresh(page);

  const presetLink = page.getByTestId('dashboard-preset-row').first().getByRole('link');
  await expect(presetLink).toHaveAttribute('href', `/app/search?preset=${seeded.presetId}`);
  await presetLink.click();

  await expect(page).toHaveURL(/\/app\/search$/);
  await expect(page.locator('select[name="selectedPreset"]')).toHaveValue(seeded.presetId);
  // Applying collapses the filter panel; the results show the preset's filter took effect.
  await expect(page.getByTestId('search-total')).toHaveText(/(^|\D)3(\D|$)/);
  for (const candidate of seeded.candidates) {
    await expect(page.locator(`a[href="/app/candidates/${candidate.id}"]`)).toBeVisible();
  }
});

test('keeps panel headings legible and the page within a phone width', async ({ page }) => {
  await page.goto('/app');
  const heading = page.getByTestId('dashboard-positions').getByRole('heading', { level: 2 });
  await expect(heading).toBeVisible();

  // The heading text must not take the panel's own colour (KTL-40 review: a stray app-bar rule
  // once painted it white on white).
  const { color, background } = await heading.evaluate((element) => ({
    color: getComputedStyle(element).color,
    background: getComputedStyle(element.closest('.panel')!).backgroundColor,
  }));
  expect(color).not.toBe(background);
  expect(color).not.toBe('rgb(255, 255, 255)');

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByTestId('dashboard-saved-searches')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
