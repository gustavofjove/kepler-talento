import { expect, test } from '@playwright/test';

const referenceId = '11111111-1111-4111-8111-111111111111';

test.describe('KTL-5 same-origin API reference flow', () => {
  test('loads the synthetic candidate through the React service boundary', async ({ page }) => {
    await page.goto('/platform/reference');

    await page.getByRole('button', { name: 'Probar API' }).click();

    await expect(page.getByTestId('reference-candidate-result')).toHaveText('Candidata Sintética');
  });

  test('preserves correlation on success and redacted not-found problems', async ({ request }) => {
    const success = await request.get(`/api/reference/candidates/${referenceId}`, {
      headers: { 'X-Correlation-ID': 'playwright-reference-success' },
    });
    expect(success.status()).toBe(200);
    expect(success.headers()['x-correlation-id']).toBe('playwright-reference-success');

    const missing = await request.get(
      '/api/reference/candidates/22222222-2222-4222-8222-222222222222',
      { headers: { 'X-Correlation-ID': 'playwright-reference-missing' } },
    );
    expect(missing.status()).toBe(404);
    expect(missing.headers()['x-correlation-id']).toBe('playwright-reference-missing');
    await expect(missing.json()).resolves.toMatchObject({
      status: 404,
      code: 'candidate.not_found',
      correlationId: 'playwright-reference-missing',
    });
  });

  test('does not expose deferred business or physical document routes', async ({ request }) => {
    expect((await request.get('/api/candidates')).status()).toBe(404);
    expect((await request.get('/api/documents/quarantine/example')).status()).toBe(404);
    expect((await request.get('/api/documents/available/example')).status()).toBe(404);
  });
});
