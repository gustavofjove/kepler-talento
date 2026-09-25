import { expect, test } from './fixtures';
import { authFile } from './global-setup';
import { CANDIDATE_PAGE_URL, editPanel, finishPanel, savePanel } from './support/candidate-panels';
import { addValue, chip } from './support/catalog-picker';

test.use({ storageState: authFile('rrhh_admin') });

test('creates and assigns a tag, manages a note, and filters by the tag', async ({ page }) => {
  const suffix = Date.now().toString();
  const tag = `KtlTag${suffix}`;
  const firstName = `KtlTags${suffix}`;
  const note = `Private follow-up ${suffix}`;
  const editedNote = `${note} edited`;

  await page.goto('/app/catalogs');
  await page.selectOption('select[name="family"]', 'tag');
  await page.fill('input[name="newNameEs"]', tag);
  await page.fill('input[name="newCode"]', `KTL_TAG_${suffix}`);
  await page
    .locator('form')
    .filter({ has: page.locator('input[name="newNameEs"]') })
    .locator('button[type="submit"]')
    .click();
  await expect(page.locator('tbody tr', { hasText: tag })).toBeVisible();

  await page.goto('/app/candidates/new');
  await page.fill('input[name="firstName"]', firstName);
  await page.fill('input[name="lastName"]', 'Candidate');
  await page
    .locator('form')
    .filter({ has: page.locator('input[name="firstName"]') })
    .locator('button[type="submit"]')
    .click();
  // KTL-29: creation opens the candidate page; tags and notes are edited in their panels.
  await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
  const candidateUrl = page.url();
  await editPanel(page, 'competencies');
  await addValue(page, 'candidate-tag', tag);
  await savePanel(page, 'competencies');

  await editPanel(page, 'notes');
  await page.getByTestId('candidate-note-body').fill(note);
  await page.getByTestId('candidate-notes').locator('form button[type="submit"]').click();
  const noteRow = page.locator('article.candidate-note', { hasText: note });
  await expect(noteRow).toBeVisible();
  await noteRow.locator('button').first().click();
  await noteRow.locator('textarea[name="noteEditBody"]').fill(editedNote);
  await noteRow.locator('button').first().click();
  await expect(noteRow).toContainText(editedNote);
  await noteRow.locator('button').last().click();
  await page.getByTestId('confirm-accept').click();
  await expect(noteRow).toHaveCount(0);
  await finishPanel(page, 'notes');

  // Read-only, the page shows the tag but offers no way to change tags or notes.
  await page.goto(candidateUrl);
  await expect(chip(page, 'candidate-tag', tag)).toBeVisible();
  await expect(page.getByTestId('candidate-tag-input')).toHaveCount(0);
  await expect(page.getByTestId('candidate-note-body')).toHaveCount(0);
  await expect(page.getByTestId('candidate-tags').locator('button')).toHaveCount(0);

  await page.goto('/app/search');
  await addValue(page, 'search-tag', tag);
  await page.locator('form button[type="submit"]').click();
  await expect(page.getByText(`${firstName} Candidate`)).toBeVisible();

  // Restore the shared environment through the product's logical-retirement paths.
  await page.goto(candidateUrl);
  await page.locator('button.button.danger').click();
  await page.getByTestId('confirm-accept').click();
  await page.goto('/app/catalogs');
  await page.selectOption('select[name="family"]', 'tag');
  const catalogRow = page.locator('tbody tr', { hasText: tag });
  await catalogRow.locator('button').last().click();
  await page.getByTestId('confirm-accept').click();
});
