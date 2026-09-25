import { expect, test } from './fixtures';
import { authorizationHeaders, signInAs } from './support/auth';
import { CANDIDATE_PAGE_URL, editPanel } from './support/candidate-panels';

const PDF_BYTES = Buffer.from('%PDF-1.4\n%%EOF', 'utf-8');

test.describe('Secure access', () => {
  test('redirects an unauthenticated user from a protected route to /login', async ({ page }) => {
    await page.goto('/app/candidates');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('lets an rrhh_admin log in and reach the protected app shell', async ({ page }) => {
    await signInAs(page, 'rrhh_admin');
    await expect(page).toHaveURL(/\/app$/);
  });

  test('blocks a readonly user from the candidate creation route', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'readonly');

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app$/);
    await context.close();
  });

  test('allows an rrhh_admin to reach the candidate creation route', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'rrhh_admin');

    await page.goto('/app/candidates/new');

    await expect(page).toHaveURL(/\/app\/candidates\/new$/);
    await context.close();
  });

  test('does not expose the removed application-owned MFA route', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await signInAs(page, 'rrhh_admin');

    await page.goto('/mfa');
    await expect(page).toHaveURL(/\/app$/);

    await context.close();
  });

  test('does not restore the removed MFA flow for an unauthenticated caller', async ({ page }) => {
    await page.goto('/mfa');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('fails closed for document preview and direct content access', async ({
    browser,
    baseURL,
  }) => {
    const admin = await browser.newContext({ baseURL });
    const adminPage = await admin.newPage();
    await signInAs(adminPage, 'rrhh_admin');
    await adminPage.goto('/app/candidates/new');
    await adminPage.fill('input[name="firstName"]', `Preview${Date.now()}`);
    await adminPage.fill('input[name="lastName"]', 'Security');
    await adminPage.click('button[type="submit"]');
    await expect(adminPage).toHaveURL(CANDIDATE_PAGE_URL);
    const candidateId = adminPage.url().split('/').at(-1)!;
    await editPanel(adminPage, 'documents');
    const documents = adminPage.getByTestId('candidate-documents');
    await documents.getByTestId('document-file').setInputFiles({
      name: 'secure-preview.pdf',
      mimeType: 'application/pdf',
      buffer: PDF_BYTES,
    });
    await documents.getByTestId('document-upload').click();
    await expect(documents.getByTestId('document-availability')).toHaveText('Disponible', {
      timeout: 25_000,
    });
    const aggregate = await adminPage.request.get(`/api/candidates/${candidateId}`, {
      headers: authorizationHeaders(adminPage),
    });
    expect(aggregate.status()).toBe(200);
    const documentId = ((await aggregate.json()) as { documents: Array<{ id: string }> })
      .documents[0]!.id;
    await admin.close();

    const readonly = await browser.newContext({ baseURL });
    const readonlyPage = await readonly.newPage();
    await signInAs(readonlyPage, 'readonly');
    await readonlyPage.goto(`/app/candidates/${candidateId}`);
    await expect(readonlyPage.getByTestId('candidate-cv-preview')).toHaveCount(0);
    // KTL-29: a reader gets no «Editar» on any panel and no form at all.
    await expect(readonlyPage.getByTestId('candidate-documents')).toBeVisible();
    await expect(readonlyPage.locator('[data-testid^="candidate-panel-"]')).toHaveCount(0);
    await expect(readonlyPage.locator('main form')).toHaveCount(0);
    await expect(readonlyPage.getByTestId('document-file')).toHaveCount(0);
    // KTL-24: every relation section renders its chips only, with no picker input.
    for (const family of ['language', 'program', 'skill', 'tag']) {
      await expect(readonlyPage.getByTestId(`candidate-${family}-picker`)).toBeVisible();
      await expect(readonlyPage.getByTestId(`candidate-${family}-input`)).toHaveCount(0);
      await expect(readonlyPage.getByTestId(`candidate-${family}-add`)).toHaveCount(0);
    }
    await expect(readonlyPage.getByRole('combobox')).toHaveCount(0);
    // The former edit address leads a reader to the same read-only page, never to an editor.
    await readonlyPage.goto(`/app/candidates/${candidateId}/edit`);
    await expect(readonlyPage).toHaveURL(new RegExp(`/app/candidates/${candidateId}$`));
    await expect(readonlyPage.getByTestId('candidate-documents')).toBeVisible();
    await expect(readonlyPage.locator('[data-testid^="candidate-panel-"]')).toHaveCount(0);
    await expect(readonlyPage.locator('main form')).toHaveCount(0);
    const unauthorized = await readonlyPage.request.get(
      `/api/candidates/${candidateId}/documents/${documentId}/content`,
      { headers: authorizationHeaders(readonlyPage) },
    );
    expect(unauthorized.status()).toBe(404);

    const unauthenticated = await readonlyPage.request.get(
      `/api/candidates/${candidateId}/documents/${documentId}/content`,
    );
    expect(unauthenticated.status()).toBe(401);

    // Hiding the controls is not the control: direct writes still fail closed.
    const readonlyHeaders = authorizationHeaders(readonlyPage);
    const writes = [
      ['put', `/api/candidates/${candidateId}/skills`, { skills: [], version: 1 }],
      ['put', `/api/candidates/${candidateId}/languages`, { languages: [], version: 1 }],
      ['put', `/api/candidates/${candidateId}/programs`, { programs: [], version: 1 }],
      ['put', `/api/candidates/${candidateId}/tags`, { tags: [], version: 1 }],
      ['post', `/api/candidates/${candidateId}/notes`, { body: 'Nota no autorizada' }],
      ['post', `/api/candidates/${candidateId}/documents`, undefined],
    ] as const;
    for (const [method, url, data] of writes) {
      const refused = await readonlyPage.request[method](url, { headers: readonlyHeaders, data });
      expect(refused.status(), `${method} ${url} as readonly`).toBe(403);
      const anonymous = await readonlyPage.request[method](url, { data });
      expect(anonymous.status(), `${method} ${url} unauthenticated`).toBe(401);
    }
    await readonly.close();
  });
});
