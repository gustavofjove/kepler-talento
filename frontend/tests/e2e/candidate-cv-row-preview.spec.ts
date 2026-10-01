import type { Locator, Page } from '@playwright/test';
import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';

/**
 * KTL-35: the candidate tables' «CV» column — a download for any clean primary CV, and «Ver»,
 * which shows a clean PDF beside the table on wide screens and under its row otherwise.
 */
test.use({ storageState: authFile('rrhh_admin') });

const PDF = Buffer.from(
  '%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF',
  'utf-8',
);

interface Seeded {
  marker: string;
  pdfA: string;
  pdfB: string;
  text: string;
}

async function createCandidate(page: Page, firstName: string, marker: string): Promise<string> {
  const response = await page.request.post('/api/candidates', {
    headers: authorizationHeaders(page),
    data: {
      firstName,
      lastName: marker,
      phone: '+34 600 100 350',
      email: `${firstName.toLowerCase()}.${marker.toLowerCase()}@example.invalid`,
      location: 'Madrid',
      province: 'Madrid',
      country: 'España',
      availability: 'Inmediata',
      status: 'available',
      source: 'e2e',
      notes: '',
      receivedAt: '2026-09-01',
      consentAt: '2026-09-01',
      reviewDueAt: '2027-09-01',
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return ((await response.json()) as { id: string }).id;
}

async function uploadPrimary(
  page: Page,
  candidateId: string,
  file: { name: string; mimeType: string; buffer: Buffer },
): Promise<void> {
  const headers = authorizationHeaders(page);
  const upload = await page.request.post(`/api/candidates/${candidateId}/documents`, {
    headers,
    multipart: { file, documentType: 'CV', isPrimary: 'true' },
  });
  expect(upload.ok(), await upload.text()).toBeTruthy();
  // ClamAV releases the file asynchronously; the column only reflects a clean scan.
  await expect
    .poll(
      async () => {
        const list = await page.request.get(`/api/candidates/${candidateId}/documents`, {
          headers,
        });
        const documents = (await list.json()) as { availabilityState: string }[];
        return documents[0]?.availabilityState;
      },
      { timeout: 30_000 },
    )
    .toBe('Available');
}

/** Two clean PDFs and one clean text CV, all marked for the e2e teardown. */
async function seed(page: Page): Promise<Seeded> {
  const marker = `CvFila${Date.now()}`;
  const pdfA = await createCandidate(page, 'Alba', marker);
  const pdfB = await createCandidate(page, 'Bruno', marker);
  const text = await createCandidate(page, 'Carmen', marker);
  await uploadPrimary(page, pdfA, {
    name: `a-${marker}.pdf`,
    mimeType: 'application/pdf',
    buffer: PDF,
  });
  await uploadPrimary(page, pdfB, {
    name: `b-${marker}.pdf`,
    mimeType: 'application/pdf',
    buffer: PDF,
  });
  await uploadPrimary(page, text, {
    name: `c-${marker}.txt`,
    mimeType: 'text/plain',
    buffer: Buffer.from('Curriculum de prueba en texto plano.', 'utf-8'),
  });
  return { marker, pdfA, pdfB, text };
}

async function openList(page: Page, marker: string): Promise<void> {
  await page.goto('/app/candidates');
  await page.locator('input[name="text"]').fill(marker);
  await expect(page.getByTestId('candidate-row')).toHaveCount(3);
}

const row = (page: Page, firstName: string): Locator =>
  page.getByTestId('candidate-row').filter({ hasText: firstName });

const mainWidth = (page: Page): Promise<number> =>
  page.locator('main').evaluate((element) => element.getBoundingClientRect().width);

/** Content (bytes) requests, as opposed to document list reads. */
function countContentRequests(page: Page): () => number {
  let count = 0;
  page.on('request', (request) => {
    if (/\/documents\/[^/]+\/content/.test(request.url())) count += 1;
  });
  return () => count;
}

test.describe('CV from the candidate tables (KTL-35)', () => {
  test.describe.configure({ timeout: 120_000 });

  test('shows the CV beside the list on wide screens, switches, hides and keeps navigation', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    const seeded = await seed(page);
    await openList(page, seeded.marker);
    const listUrl = page.url();

    // Cells: download + «Ver» for a PDF, download only for the text CV.
    await expect(row(page, 'Alba').getByTestId('row-cv-download')).toBeVisible();
    await expect(row(page, 'Alba').getByTestId('row-cv-toggle')).toHaveText('→');
    await expect(row(page, 'Carmen').getByTestId('row-cv-download')).toBeVisible();
    await expect(row(page, 'Carmen').getByTestId('row-cv-toggle')).toHaveCount(0);

    const contentRequests = countContentRequests(page);
    const toggleA = row(page, 'Alba').getByTestId('row-cv-toggle');
    await toggleA.click();
    const aside = page.locator('.page-split__aside');
    await expect(aside.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/, {
      timeout: 25_000,
    });
    await expect(toggleA).toHaveAttribute('aria-expanded', 'true');
    await expect(toggleA).toHaveText('←');
    await expect(row(page, 'Alba')).toHaveClass(/is-cv-open/);
    expect(page.url()).toBe(listUrl);
    expect(await mainWidth(page)).toBeGreaterThan(1440);
    // Pushed, not covered: the table ends left of the panel.
    const table = (await page.getByTestId('candidate-table').boundingBox())!;
    const panel = (await aside.getByTestId('row-cv-panel').boundingBox())!;
    expect(table.x + table.width).toBeLessThanOrEqual(panel.x);
    expect(contentRequests()).toBe(1);

    // Another CV replaces the first; one viewer on the page.
    await row(page, 'Bruno').getByTestId('row-cv-toggle').click();
    await expect(aside.getByRole('heading', { name: /Bruno/ })).toBeVisible();
    await expect(toggleA).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('row-cv-panel')).toHaveCount(1);

    // Hide from the panel: focus back on the row, page back to its default width.
    await page.getByTestId('row-cv-hide').click();
    await expect(page.getByTestId('row-cv-panel')).toHaveCount(0);
    await expect(row(page, 'Bruno').getByTestId('row-cv-toggle')).toBeFocused();
    expect(await mainWidth(page)).toBeLessThanOrEqual(1440);

    // A row click outside the controls still opens the candidate.
    await row(page, 'Alba').locator('td').nth(2).click();
    await expect(page).toHaveURL(new RegExp(`/app/candidates/${seeded.pdfA}$`));
  });

  test('moves the open CV across the breakpoint without downloading it again', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    const seeded = await seed(page);
    await openList(page, seeded.marker);
    const contentRequests = countContentRequests(page);

    const toggle = row(page, 'Alba').getByTestId('row-cv-toggle');
    await toggle.click();
    await expect(
      page.locator('.page-split__aside').getByTestId('cv-preview-viewer'),
    ).toHaveAttribute('data', /^blob:/, { timeout: 25_000 });

    await page.setViewportSize({ width: 1280, height: 900 });
    const inline = page.getByTestId('row-cv-row');
    await expect(inline.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/);
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(toggle).toHaveText('↑');
    await expect(inline).toBeInViewport();

    await page.setViewportSize({ width: 1920, height: 1080 });
    await expect(page.locator('.page-split__aside').getByTestId('row-cv-panel')).toBeVisible();
    await expect(page.getByTestId('row-cv-row')).toHaveCount(0);
    expect(contentRequests()).toBe(1);
  });

  test('opens under the row on narrower screens and fits a phone', async ({ page }) => {
    await page.setViewportSize({ width: 1366, height: 768 });
    const seeded = await seed(page);
    await openList(page, seeded.marker);

    const toggle = row(page, 'Alba').getByTestId('row-cv-toggle');
    await expect(toggle).toHaveText('↓');
    await toggle.click();
    const next = await row(page, 'Alba').evaluate(
      (element) => element.nextElementSibling?.getAttribute('data-testid') ?? null,
    );
    expect(next).toBe('row-cv-row');
    await expect(page.locator('.page-split__aside')).toBeEmpty();

    await page.setViewportSize({ width: 390, height: 844 });
    await expect(page.getByTestId('row-cv-panel')).toBeVisible();
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(1);
    const content = (await page.locator('.cv-row__content').boundingBox())!;
    expect(content.width).toBeLessThanOrEqual(390);
  });

  test('downloads a CV that cannot be previewed, and the candidate page shows no preview for it', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    const seeded = await seed(page);
    await openList(page, seeded.marker);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      row(page, 'Carmen').getByTestId('row-cv-download').click(),
    ]);
    expect(download.suggestedFilename()).toBe(`c-${seeded.marker}.txt`);
    expect(page.url()).toContain('/app/candidates');

    await page.goto(`/app/candidates/${seeded.text}`);
    await expect(page.getByTestId('candidate-documents')).toBeVisible();
    await expect(page.getByTestId('candidate-cv-preview')).toHaveCount(0);
    await page.goto(`/app/candidates/${seeded.pdfA}`);
    await expect(page.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/, {
      timeout: 25_000,
    });
  });

  test('keeps one CV open across the position page tables', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    const seeded = await seed(page);
    const headers = authorizationHeaders(page);
    const created = await page.request.post('/api/positions', {
      headers,
      data: {
        title: `Posición ${seeded.marker}`,
        description: '',
        location: '',
        requirements: { text: seeded.marker },
      },
    });
    expect(created.ok(), await created.text()).toBeTruthy();
    const position = (await created.json()) as { id: string };
    const linked = await page.request.post(`/api/positions/${position.id}/candidates`, {
      headers,
      data: { candidateId: seeded.pdfA },
    });
    expect(linked.ok(), await linked.text()).toBeTruthy();

    await page.goto(`/app/positions/${position.id}`);
    const linkedRow = page.getByTestId('position-candidate-row').filter({ hasText: 'Alba' });
    const matches = page
      .locator('.position-section')
      .filter({ has: page.getByTestId('search-total') });
    const matchRow = matches.locator('tr.row-link-row').filter({ hasText: 'Alba' });

    await linkedRow.getByTestId('row-cv-toggle').click();
    await expect(linkedRow).toHaveClass(/is-cv-open/);
    await matchRow.getByTestId('row-cv-toggle').click();

    // Same candidate in both tables: only the clicked one is open.
    await expect(matchRow).toHaveClass(/is-cv-open/);
    await expect(linkedRow).not.toHaveClass(/is-cv-open/);
    await expect(linkedRow.getByTestId('row-cv-toggle')).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByTestId('row-cv-panel')).toHaveCount(1);
  });
});
