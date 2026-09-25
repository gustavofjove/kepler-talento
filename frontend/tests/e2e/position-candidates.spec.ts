import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';
import { createCandidate } from './support/candidate-panels';

/**
 * KTL-30 browser journey: candidates added to a position, restaged on both pages, rejected,
 * removed, and frozen when the position closes. Selectors use names, test ids and hrefs
 * only. Every record carries a `Date.now()` marker, so the global teardown purges the
 * candidates, the positions and the links between them.
 */

const POSITION_URL = /\/app\/positions\/[0-9a-f]{8}-[0-9a-f-]{27}$/;

async function createPosition(page: Page, title: string, requirementText = ''): Promise<string> {
  await page.goto('/app/positions/new');
  await page.getByTestId('position-title').fill(title);
  if (requirementText) await page.locator('input[name="text"]').fill(requirementText);
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(POSITION_URL);
  return page.url().split('/').pop()!;
}

async function closePosition(page: Page, id: string): Promise<void> {
  await page.goto(`/app/positions/${id}/edit`);
  await page.getByTestId('position-status').selectOption('closed');
  await page.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(new RegExp(`/app/positions/${id}$`));
}

const linkRow = (page: Page, candidateId: string) =>
  page
    .getByTestId('position-candidate-row')
    .filter({ has: page.locator(`a[href="/app/candidates/${candidateId}"]`) });

/** A row of «Candidatos que encajan», which renders inside the shared search results block. */
const matchRow = (page: Page, candidateId: string) =>
  page
    .locator('.section-block tr')
    .filter({ has: page.locator(`a[href="/app/candidates/${candidateId}"]`) });

test.describe('Position candidates - manager', () => {
  test.use({ storageState: authFile('rrhh_admin') });

  test('adds, restages, rejects and removes candidates, then freezes them on close', async ({
    page,
  }) => {
    const marker = Date.now();
    const matching = await createCandidate(page, `Encaja${marker}`, 'Ktl30');
    const direct = await createCandidate(page, `Directo${marker}`, 'Ktl30');
    // Requirements that only the first candidate meets.
    const position = await createPosition(page, `E2E Enlaces ${marker}`, `Encaja${marker}`);
    await expect(page.getByTestId('position-candidates-empty')).toBeVisible();

    // 1. Add the match from «Candidatos que encajan».
    await matchRow(page, matching).getByTestId('add-to-position').click();
    await expect(linkRow(page, matching)).toHaveCount(1);
    await expect(matchRow(page, matching).getByTestId('added-to-position')).toBeDisabled();

    // 2. Add a candidate who does not meet the requirements, through the picker.
    await page.getByTestId('position-candidates-add').click();
    await page.getByTestId('position-candidate-picker-search').fill(`Directo${marker}`);
    await page.getByTestId('position-candidate-picker-option').first().click();
    await expect(page.getByTestId('position-candidate-picker')).toHaveCount(0);
    await expect(page.getByTestId('position-candidate-row')).toHaveCount(2);

    // 3. Restage on the position page; the stage survives a reload.
    await linkRow(page, matching).getByTestId('position-candidate-stage').selectOption('interview');
    await expect(linkRow(page, matching).getByTestId('position-candidate-stage')).toHaveValue(
      'interview',
    );
    await page.reload();
    await expect(linkRow(page, matching).getByTestId('position-candidate-stage')).toHaveValue(
      'interview',
    );

    // 4. Reject the direct candidate: the link stays.
    await linkRow(page, direct).getByTestId('position-candidate-stage').selectOption('rejected');
    await expect(linkRow(page, direct).getByTestId('position-candidate-stage')).toHaveValue(
      'rejected',
    );
    await expect(page.getByTestId('position-candidate-row')).toHaveCount(2);

    // 5. Remove the match as a mistaken addition: it can be added again.
    await linkRow(page, matching).getByTestId('remove-from-position').click();
    await page.getByTestId('confirm-accept').click();
    await expect(linkRow(page, matching)).toHaveCount(0);
    await expect(matchRow(page, matching).getByTestId('add-to-position')).toBeEnabled();

    // 6. From the candidate page: restage and add to a second open position.
    const second = await createPosition(page, `E2E Enlaces bis ${marker}`);
    await page.goto(`/app/candidates/${direct}`);
    const panel = page.getByTestId('candidate-positions');
    const firstRow = panel
      .getByTestId('candidate-position-row')
      .filter({ has: page.locator(`a[href="/app/positions/${position}"]`) });
    await expect(firstRow.getByTestId('position-candidate-stage')).toHaveValue('rejected');
    await firstRow.getByTestId('position-candidate-stage').selectOption('shortlisted');
    await expect(firstRow.getByTestId('position-candidate-stage')).toHaveValue('shortlisted');

    await page.getByTestId('candidate-positions-add').click();
    await page.getByTestId('position-picker-search').fill(`E2E Enlaces bis ${marker}`);
    await page.getByTestId('position-picker-option').first().click();
    await expect(page.getByTestId('position-picker')).toHaveCount(0);
    await expect(panel.locator(`a[href="/app/positions/${second}"]`).first()).toBeVisible();

    // 7. Closing freezes the links: read-only on the position, muted on the candidate.
    await closePosition(page, position);
    await expect(page.getByTestId('position-candidates-closed')).toBeVisible();
    await expect(linkRow(page, direct)).toHaveCount(1);
    await expect(page.getByTestId('position-candidate-stage')).toHaveCount(0);
    await expect(page.getByTestId('remove-from-position')).toHaveCount(0);
    await expect(page.getByTestId('add-to-position')).toHaveCount(0);

    await page.goto(`/app/candidates/${direct}`);
    const pastRow = page
      .getByTestId('candidate-position-row')
      .filter({ has: page.locator(`a[href="/app/positions/${position}"]`) });
    await expect(pastRow).toHaveClass(/position-row-past/);
    await expect(pastRow.getByTestId('position-candidate-stage')).toHaveCount(0);
    // Open positions come first.
    await expect(
      page
        .getByTestId('candidate-position-row')
        .first()
        .locator(`a[href="/app/positions/${second}"]`)
        .first(),
    ).toBeVisible();

    await closePosition(page, second);
  });

  test('the picker is keyboard operable and the page fits 390px', async ({ page }) => {
    const marker = Date.now();
    const position = await createPosition(page, `E2E Teclado ${marker}`);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`/app/positions/${position}`);

    const opener = page.getByTestId('position-candidates-add');
    await opener.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('position-candidate-picker-search')).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(page.getByTestId('position-candidate-picker')).toHaveCount(0);
    await expect(opener).toBeFocused();

    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    );
    expect(overflow).toBeLessThanOrEqual(1);
    await closePosition(page, position);
  });
});

test.describe('Position candidates - reader', () => {
  test.use({ storageState: authFile('readonly') });

  test('a reader sees no link controls on a candidate page', async ({ page }) => {
    await page.goto('/app/candidates');
    const first = page.locator('a[href^="/app/candidates/"]').first();
    if ((await first.count()) === 0) test.skip(true, 'No candidate to open.');
    await first.click();
    await expect(page.getByTestId('candidate-positions')).toBeVisible();
    await expect(page.getByTestId('candidate-positions-add')).toHaveCount(0);
    await expect(page.getByTestId('position-candidate-stage')).toHaveCount(0);
    await expect(page.getByTestId('remove-from-position')).toHaveCount(0);
  });
});
