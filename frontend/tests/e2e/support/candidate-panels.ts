import { expect, type Page } from '@playwright/test';

/** The candidate page panels that can be put in edit mode (KTL-29). */
export type CandidatePanel =
  'main' | 'competencies' | 'education' | 'experience' | 'notes' | 'documents';

/** A candidate page URL: `/app/candidates/<uuid>`, with no `/edit` suffix since KTL-29. */
export const CANDIDATE_PAGE_URL =
  /\/app\/candidates\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Puts a panel of the candidate page in edit mode. */
export async function editPanel(page: Page, panel: CandidatePanel): Promise<void> {
  await page.getByTestId(`candidate-panel-${panel}-edit`).click();
  await expect(page.getByTestId(`candidate-panel-${panel}-edit`)).toHaveCount(0);
}

/** Saves a form panel and waits until it is read-only again. */
export async function savePanel(page: Page, panel: CandidatePanel): Promise<void> {
  await page.getByTestId(`candidate-panel-${panel}-save`).click();
  await expect(page.getByTestId(`candidate-panel-${panel}-edit`)).toBeVisible();
}

/** Closes an action panel (Notas, Documentos). */
export async function finishPanel(page: Page, panel: CandidatePanel): Promise<void> {
  await page.getByTestId(`candidate-panel-${panel}-done`).click();
  await expect(page.getByTestId(`candidate-panel-${panel}-edit`)).toBeVisible();
}

/** Creates a candidate from the create page and returns its id, on its candidate page. */
export async function createCandidate(
  page: Page,
  firstName: string,
  lastName = 'Candidato',
): Promise<string> {
  await page.goto('/app/candidates/new');
  await page.fill('input[name="firstName"]', firstName);
  await page.fill('input[name="lastName"]', lastName);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
  return page.url().split('/').at(-1)!;
}
