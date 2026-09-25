import { expect, test, type Page } from './fixtures';
import { authFile } from './global-setup';
import { authorizationHeaders } from './support/auth';
import { CANDIDATE_PAGE_URL, editPanel, finishPanel } from './support/candidate-panels';

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
  // KTL-29: creation opens the candidate page; documents are managed in its Documentos panel.
  await expect(page).toHaveURL(CANDIDATE_PAGE_URL);
  await editPanel(page, 'documents');
  return page.url().split('/').at(-1)!;
}

async function expectNoHorizontalScroll(page: Page): Promise<void> {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    ),
  ).toBe(false);
}

test.describe('Candidate documents flow', () => {
  test('uploads, scans and downloads the original PDF bytes', async ({ page }) => {
    test.setTimeout(60_000);
    const cspViolations: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error' && message.text().includes('Content Security Policy')) {
        cspViolations.push(message.text());
      }
    });
    const candidateId = await createCandidate(page, 'CV');
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

    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 900 });
      await expectNoHorizontalScroll(page);
    }

    // Read-only, the panel keeps download and the preview but not upload.
    await finishPanel(page, 'documents');
    await page.goto(`/app/candidates/${candidateId}`);
    await expect(page.getByTestId('cv-preview-viewer')).toHaveAttribute('data', /^blob:/);
    expect(cspViolations).toEqual([]);
    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 900 });
      await expect(page.getByTestId('candidate-cv-preview')).toBeVisible();
      await expectNoHorizontalScroll(page);
    }
    const detailDocuments = page.getByTestId('candidate-documents');
    await expect(detailDocuments.getByTestId('candidate-document')).toHaveCount(1);
    await expect(detailDocuments.getByTestId('document-file')).toHaveCount(0);
    await expect(detailDocuments.getByTestId('document-upload')).toHaveCount(0);

    await editPanel(page, 'documents');
    const refreshedDocuments = page.getByTestId('candidate-documents');
    await refreshedDocuments.getByTestId('document-file').setInputFiles({
      name: 'candidate-cv.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('Curriculum vitae', 'utf-8'),
    });
    await refreshedDocuments.getByTestId('document-is-primary').uncheck();
    await refreshedDocuments.getByTestId('document-upload').click();
    await expect(refreshedDocuments.getByTestId('document-availability').first()).toHaveText(
      'Disponible',
      { timeout: 25_000 },
    );
    await finishPanel(page, 'documents');
    await page.getByTestId('preview-document-select').selectOption({ label: 'candidate-cv.txt' });
    await expect(page.getByTestId('cv-preview-unsupported')).toBeVisible();
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
    await expect(documents.getByTestId('document-upload-error')).toHaveText(
      'El archivo supera el máximo permitido de 20 MB.',
    );

    const edgeResponse = await page.request.post(`/api/candidates/${candidateId}/documents`, {
      headers: authorizationHeaders(page),
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
