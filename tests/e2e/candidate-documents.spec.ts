import { expect, test, type Page } from '@playwright/test';
import { authFile } from './global-setup';

test.use({ storageState: authFile('rrhh_admin') });

const PDF_BYTES = Buffer.from(
  '%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF',
  'utf-8',
);

async function createCandidate(page: Page, prefix: string): Promise<string> {
  await page.goto('/app/candidates/new');
  await page.fill('input[name="firstName"]', `${prefix}${Date.now()}`);
  await page.fill('input[name="lastName"]', 'Documentos');
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL(
    /\/app\/candidates\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  );
  return page.url().split('/').pop()!;
}

test.describe('Candidate documents flow', () => {
  test('uploads, scans and downloads the original PDF bytes', async ({ page }) => {
    await createCandidate(page, 'CV');
    const documents = page.getByTestId('candidate-documents');

    await documents.getByTestId('document-file').setInputFiles({
      name: 'candidate-cv.pdf',
      mimeType: 'application/pdf',
      buffer: PDF_BYTES,
    });
    await documents.getByTestId('document-upload').click();

    await expect(
      page.getByText('Archivo aceptado. El análisis de seguridad está en curso.'),
    ).toBeVisible();
    await expect(documents.getByTestId('document-availability')).toHaveText('En análisis');
    await expect(documents.getByRole('button', { name: 'Descargar' })).toHaveCount(0);

    await expect(documents.getByTestId('document-availability')).toHaveText('Disponible', {
      timeout: 25_000,
    });
    const downloadPromise = page.waitForEvent('download');
    await documents.getByRole('button', { name: 'Descargar' }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe('candidate-cv.pdf');
    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream) chunks.push(Buffer.from(chunk));
    expect(Buffer.concat(chunks)).toEqual(PDF_BYTES);
  });

  test('shows the Spanish 20 MB refusal and the edge forwards a 21 MB multipart body', async ({
    page,
  }) => {
    const candidateId = await createCandidate(page, 'Grande');
    const documents = page.getByTestId('candidate-documents');
    const oversized = Buffer.alloc(21_000_000, 0x20);
    oversized.write('%PDF-1.4');

    await documents.getByTestId('document-file').setInputFiles({
      name: 'demasiado-grande.pdf',
      mimeType: 'application/pdf',
      buffer: oversized,
    });
    await documents.getByTestId('document-upload').click();
    await expect(page.getByText('El archivo supera el máximo permitido de 20 MB.')).toBeVisible();

    const edgeResponse = await page.request.post(`/api/candidates/${candidateId}/documents`, {
      multipart: {
        file: {
          name: 'demasiado-grande.pdf',
          mimeType: 'application/pdf',
          buffer: oversized,
        },
        documentType: 'CV',
        isPrimary: 'true',
      },
    });
    expect(edgeResponse.status()).toBe(400);
    expect(await edgeResponse.text()).toContain('El archivo supera el máximo permitido de 20 MB.');
  });
});
